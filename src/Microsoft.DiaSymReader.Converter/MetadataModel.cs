// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Collections;
using Roslyn.Utilities;

namespace Microsoft.DiaSymReader.Tools
{
    internal sealed class MetadataModel
    {
        public MetadataReader Reader { get; }

        private readonly Lazy<ValueTuple<Dictionary<string, AssemblyReferenceHandle>, string[]>> _lazyAssemblyRefMap;

        private readonly Lazy<Dictionary<byte[], TypeSpecificationHandle>> _lazyTypeSpecificationMap;

        private readonly Lazy<Dictionary<string, EntityHandle>> _lazyTypeMap;

        public MetadataModel(MetadataReader reader)
        {
            Reader = reader;

            _lazyAssemblyRefMap = new Lazy<ValueTuple<Dictionary<string, AssemblyReferenceHandle>, string[]>>(BuildAssemblyRefMap);
            _lazyTypeSpecificationMap = new Lazy<Dictionary<byte[], TypeSpecificationHandle>>(BuildTypeSpecificationMap);
            _lazyTypeMap = new Lazy<Dictionary<string, EntityHandle>>(BuildTypeMap);
        }

        public ImmutableArray<int> GetRowCounts()
        {
            var builder = ImmutableArray.CreateBuilder<int>(MetadataTokens.TableCount);
            for (int i = 0; i < MetadataTokens.TableCount; i++)
            {
                builder.Add(Reader.GetTableRowCount((TableIndex)i));
            }

            return builder.MoveToImmutable();
        }

        private ValueTuple<Dictionary<string, AssemblyReferenceHandle>, string[]> BuildAssemblyRefMap()
        {
            var assemblyRefsByName = new Dictionary<string, AssemblyReferenceHandle>(StringComparer.OrdinalIgnoreCase);
            var assemblyNames = new string[Reader.AssemblyReferences.Count];

            foreach (var handle in Reader.AssemblyReferences)
            {
                var displayName = MetadataHelpers.GetAssemblyDisplayName(Reader, Reader.GetAssemblyReference(handle));

                assemblyNames[MetadataTokens.GetRowNumber(handle)] = displayName;
                assemblyRefsByName.Add(displayName, handle);
            }

            return ValueTuple.Create(assemblyRefsByName, assemblyNames);
        }

        public bool TryResolveAssemblyReference(string displayName, out AssemblyReferenceHandle handle) => 
            _lazyAssemblyRefMap.Value.Item1.TryGetValue(displayName, out handle);

        public string GetAssemblyDisplayName(AssemblyReferenceHandle? handle) =>
            GetAssemblyDisplayName(_lazyAssemblyRefMap.Value.Item2, handle);

        private static string GetAssemblyDisplayName(string[] displayNames, AssemblyReferenceHandle? handle) =>
            handle.HasValue ? displayNames[MetadataTokens.GetRowNumber(handle.Value)] : CoreLibPlaceholder;

        private Dictionary<byte[], TypeSpecificationHandle> BuildTypeSpecificationMap()
        {
            var result = new Dictionary<byte[], TypeSpecificationHandle>(ByteSequenceComparer.Instance);

            for (int rowId = 1; rowId <= Reader.GetTableRowCount(TableIndex.TypeSpec); rowId++)
            {
                var handle = MetadataTokens.TypeSpecificationHandle(rowId);
                var typeSpec = Reader.GetTypeSpecification(handle);

                result[Reader.GetBlobBytes(typeSpec.Signature)] = handle;
            }

            return result;
        }

        public bool TryResolveTypeSpecification(byte[] spec, out TypeSpecificationHandle typeSpec) => 
            _lazyTypeSpecificationMap.Value.TryGetValue(spec, out typeSpec);

        private static readonly Regex s_primitiveTypePattern = new Regex(
            @"((System.Void|System.Boolean|System.Char|System.SByte|System.Byte|System.Int16|System.UInt16|System.Int32|System.UInt32|System.Int64|System.UInt64|System.Single|System.Double|System.String|System.TypedReference|System.IntPtr|System.UIntPtr)[][*&,]*), ([^,]+), Version=[0-9]+.[0-9]+.[0-9]+.[0-9]+, Culture=neutral, PublicKeyToken=[a-zA-Z0-9]+",
            RegexOptions.Compiled);

        private const string CoreLibPlaceholder = "{CORLIB}";

        internal bool TryResolveType(string assemblyQualifiedName, out EntityHandle type)
        {
            if (_lazyTypeMap.Value.TryGetValue(assemblyQualifiedName, out type))
            {
                return true;
            }

            string normalized = s_primitiveTypePattern.Replace(assemblyQualifiedName, "$1, " + CoreLibPlaceholder);
            return _lazyTypeMap.Value.TryGetValue(normalized, out type);
        }

        private Dictionary<string, EntityHandle> BuildTypeMap()
        {
            var nameToHandle = new Dictionary<string, EntityHandle>();
            var handleToName = new Dictionary<EntityHandle, AQName>();

            foreach (var handle in Reader.TypeDefinitions)
            {
                var typeDef = Reader.GetTypeDefinition(handle);

                string name = Reader.GetString(typeDef.Name);
                var visibility = (typeDef.Attributes & TypeAttributes.VisibilityMask);

                string qualifiedName;

                TypeDefinitionHandle declaringType;
                if (visibility != TypeAttributes.Public && 
                    visibility != TypeAttributes.NotPublic &&
                    !(declaringType = typeDef.GetDeclaringType()).IsNil)
                {
                    // Spec: 
                    //   Finally, the TypeDef table has a special ordering constraint: 
                    //   the definition of an enclosing class shall precede the definition of all classes it encloses.
                    // 
                    // Hence we alrady have calculated the name of the declaring type.
                    qualifiedName = MakeNestedTypeName(handleToName[declaringType].TypeName, name);
                }
                else
                {
                    qualifiedName = MakeNamespaceTypeName(Reader.GetString(typeDef.Namespace), name);
                }

                nameToHandle.Add(qualifiedName, handle);
                handleToName.Add(handle, new AQName(qualifiedName, default(AssemblyReferenceHandle)));
            }

            foreach (var handle in Reader.TypeReferences)
            {
                var typeRef = Reader.GetTypeReference(handle);

                if (typeRef.ResolutionScope.Kind == HandleKind.TypeReference)
                {
                    // defer nested types
                    continue;
                }

                string qualifiedName = MakeNamespaceTypeName(Reader.GetString(typeRef.Namespace), Reader.GetString(typeRef.Name));

                var assemblyRef = (typeRef.ResolutionScope.Kind == HandleKind.AssemblyReference) ?
                    (AssemblyReferenceHandle)typeRef.ResolutionScope :
                    default(AssemblyReferenceHandle);

                var assemblyQualifiedName = MakeAssemblyQualifiedName(qualifiedName, GetAssemblyDisplayName(assemblyRef));

                nameToHandle[assemblyQualifiedName] = handle;
                handleToName[handle] = new AQName(qualifiedName, assemblyRef);
            }

            foreach (var handle in Reader.TypeReferences)
            {
                CalculateQualifiedTypeNameRecursive(handle, nameToHandle, handleToName);
            }

            for (int rowId = 0; rowId <= Reader.GetTableRowCount(TableIndex.TypeSpec); rowId++)
            {
                var handle = MetadataTokens.TypeSpecificationHandle(rowId);
                var signature = Reader.GetTypeSpecification(handle).Signature;
                var sigReader = Reader.GetBlobReader(signature);

                AQName assemblyQualifiedName;
                if (TryCalculateQualifiedTypeSpecificationName(sigReader, handleToName, out assemblyQualifiedName))
                {
                    nameToHandle[assemblyQualifiedName.TypeName] = handle;
                    handleToName[handle] = assemblyQualifiedName;
                }
            }

            return nameToHandle;
        }

        private AQName CalculateQualifiedTypeNameRecursive(
            TypeReferenceHandle handle,
            Dictionary<string, EntityHandle> nameToHandle,
            Dictionary<EntityHandle, AQName> handleToName)
        {
            AQName name;
            if (handleToName.TryGetValue(handle, out name))
            {
                return name;
            }

            var typeRef = Reader.GetTypeReference(handle);

            // we have alread calculated all other names earlier:
            Debug.Assert(typeRef.ResolutionScope.Kind == HandleKind.TypeReference);

            var declaringTypeAssemblyQualifiedName = CalculateQualifiedTypeNameRecursive((TypeReferenceHandle)typeRef.ResolutionScope, nameToHandle, handleToName);
            var qualifiedName = MakeNestedTypeName(declaringTypeAssemblyQualifiedName.TypeName, Reader.GetString(typeRef.Name));
            var assemblyQualifiedName = MakeAssemblyQualifiedName(qualifiedName, GetAssemblyDisplayName(declaringTypeAssemblyQualifiedName.AssemblyRef));

            var result = new AQName(qualifiedName, declaringTypeAssemblyQualifiedName.AssemblyRef);
            handleToName[handle] = result;
            nameToHandle[assemblyQualifiedName] = handle;
            return result;
        }

        private struct AQName
        {
            public static readonly AQName Empty = new AQName("", null);

            public readonly string TypeName;
            public readonly AssemblyReferenceHandle? AssemblyRef;

            public AQName(string qualifiedName, AssemblyReferenceHandle? assemblyRef)
            {
                TypeName = qualifiedName;
                AssemblyRef = assemblyRef;
            }

            public AQName WithTypeName(string typeName) => new AQName(typeName, AssemblyRef);
        }

        private bool TryCalculateQualifiedTypeSpecificationName(BlobReader sigReader, Dictionary<EntityHandle, AQName> handleToName, out AQName name)
        {
            var builder = new ImportTypeSpecNameBuilder(handleToName, _lazyAssemblyRefMap.Value.Item2);
            var decoder = new SignatureDecoder<AQName>(builder);
            name = decoder.DecodeType(ref sigReader);
            return builder.IsSupported;
        }

        private sealed class ImportTypeSpecNameBuilder : ISignatureTypeProvider<AQName>
        {
            private readonly Dictionary<EntityHandle, AQName> _handleToName;
            private readonly string[] _assemblyRefDisplayNames;
            private bool _unsupported;

            public ImportTypeSpecNameBuilder(Dictionary<EntityHandle, AQName> handleToName, string[] assemblyRefDisplayNames)
            {
                _handleToName = handleToName;
                _assemblyRefDisplayNames = assemblyRefDisplayNames;
            }

            public bool IsSupported => !_unsupported;

            private string GetFullName(AQName name)
            {
                return MakeAssemblyQualifiedName(name.TypeName, GetAssemblyDisplayName(_assemblyRefDisplayNames, name.AssemblyRef));
            }

            private static string GetPrimitiveTypeName(PrimitiveTypeCode typeCode)
            {
                switch (typeCode)
                {
                    case PrimitiveTypeCode.Void: return "System.Void";
                    case PrimitiveTypeCode.Boolean: return "System.Boolean";
                    case PrimitiveTypeCode.Char: return "System.Char";
                    case PrimitiveTypeCode.SByte: return "System.SByte";
                    case PrimitiveTypeCode.Byte: return "System.Byte";
                    case PrimitiveTypeCode.Int16: return "System.Int16";
                    case PrimitiveTypeCode.UInt16: return "System.UInt16";
                    case PrimitiveTypeCode.Int32: return "System.Int32";
                    case PrimitiveTypeCode.UInt32: return "System.UInt32";
                    case PrimitiveTypeCode.Int64: return "System.Int64";
                    case PrimitiveTypeCode.UInt64: return "System.UInt64";
                    case PrimitiveTypeCode.Single: return "System.Single";
                    case PrimitiveTypeCode.Double: return "System.Double";
                    case PrimitiveTypeCode.String: return "System.String";
                    case PrimitiveTypeCode.TypedReference: return "System.TypedReference";
                    case PrimitiveTypeCode.IntPtr: return "System.IntPtr";
                    case PrimitiveTypeCode.UIntPtr: return "System.UIntPtr";
                    default:
                        throw ExceptionUtilities.UnexpectedValue(typeCode);
                }
            }

            public AQName GetSZArrayType(AQName elementType)
            {
                return elementType.WithTypeName(elementType.TypeName + "[]");
            }

            public AQName GetArrayType(AQName elementType, ArrayShape shape)
            {
                var pooled = PooledStringBuilder.GetInstance();
                var sb = pooled.Builder;

                sb.Append(elementType.TypeName);
                sb.Append('[');
                if (shape.Rank == 1)
                {
                    sb.Append('*');
                }

                sb.Append(',', shape.Rank - 1);
                sb.Append(']');

                return elementType.WithTypeName(pooled.ToStringAndFree());
            }

            public AQName GetByReferenceType(AQName elementType)
            {
                return elementType.WithTypeName(elementType.TypeName + "&");
            }

            public AQName GetPointerType(AQName elementType)
            {
                return elementType.WithTypeName(elementType.TypeName + "*");
            }

            public AQName GetGenericInstance(AQName genericType, ImmutableArray<AQName> typeArguments)
            {
                var pooled = PooledStringBuilder.GetInstance();
                var sb = pooled.Builder;
                return genericType.WithTypeName(genericType.TypeName + "[" + string.Join(",", typeArguments.Select(GetFullName)) + "]");
            }

            public AQName GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                return _handleToName[handle];
            }

            public AQName GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                return _handleToName[handle];
            }

            public AQName GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return new AQName(GetPrimitiveTypeName(typeCode), null);
            }

            public AQName GetFunctionPointerType(MethodSignature<AQName> signature)
            {
                _unsupported = true;
                return AQName.Empty;
            }

            public AQName GetGenericMethodParameter(int index)
            {
                _unsupported = true;
                return AQName.Empty;
            }

            public AQName GetGenericTypeParameter(int index)
            {
                _unsupported = true;
                return AQName.Empty;
            }

            public AQName GetModifiedType(MetadataReader reader, bool isRequired, AQName modifier, AQName unmodifiedType)
            {
                _unsupported = true;
                return AQName.Empty;
            }

            public AQName GetPinnedType(AQName elementType)
            {
                _unsupported = true;
                return AQName.Empty;
            }

            public AQName GetTypeFromSpecification(MetadataReader reader, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                _unsupported = true;
                return AQName.Empty;
            }
        }

        private static string MakeAssemblyQualifiedName(string qualifiedName, string assemblyName) => qualifiedName + ", " + assemblyName;
        private static string MakeNestedTypeName(string declaringTypeName, string name) => declaringTypeName + "+" + name;
        private static string MakeNamespaceTypeName(string @namespace, string name) => @namespace.Length > 0 ? @namespace + "." + name : name;
    }
}
