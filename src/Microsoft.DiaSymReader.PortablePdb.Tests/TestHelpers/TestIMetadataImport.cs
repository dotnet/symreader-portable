﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
#if NET9_0_OR_GREATER
using System.Runtime.InteropServices.Marshalling;
#endif
using System.Text;

namespace Microsoft.DiaSymReader.PortablePdb.UnitTests
{
    /// <summary>
    /// Used to test <see cref="IMetadataImport"/>.
    /// </summary>
#if NET9_0_OR_GREATER
    [GeneratedComClass]
#endif
    internal sealed partial class TestIMetadataImport : IMetadataImport, IDisposable
    {
        private readonly PEReader _peReader;
        public readonly MetadataReader MetadataReader;

        public TestIMetadataImport(Stream peStream)
        {
            _peReader = new PEReader(peStream);
            MetadataReader = _peReader.GetMetadataReader();
        }

        public void Dispose()
        {
            _peReader.Dispose();
        }

        public unsafe int GetSigFromToken(int tkSignature, out byte* ppvSig, out int pcbSig)
        {
            var signatureHandle = (StandaloneSignatureHandle)MetadataTokens.Handle(tkSignature);
            var bytes = MetadataReader.GetBlobBytes(MetadataReader.GetStandaloneSignature(signatureHandle).Signature);

            var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            ppvSig = (byte*)pinned.AddrOfPinnedObject();
            pcbSig = bytes.Length;
            return HResult.S_OK;
        }

        public void GetTypeDefProps(
            int typeDefinition,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] char[] qualifiedName,
            int qualifiedNameBufferLength,
            out int qualifiedNameLength,
            out TypeAttributes attributes,
            out int baseType)
        {
            var handle = (TypeDefinitionHandle)MetadataTokens.Handle(typeDefinition);
            var typeDef = MetadataReader.GetTypeDefinition(handle);

            if (qualifiedName != null)
            {
                StringBuilder builder = new();
                if (!typeDef.Namespace.IsNil)
                {
                    builder.Append(MetadataReader.GetString(typeDef.Namespace));
                    builder.Append('.');
                }

                builder.Append(MetadataReader.GetString(typeDef.Name));
                qualifiedNameLength = Math.Min(builder.Length, Math.Max(0, qualifiedNameBufferLength - 1));
                builder.CopyTo(0, qualifiedName, 0, qualifiedNameLength);
            }
            else
            {
                qualifiedNameLength =
                    (typeDef.Namespace.IsNil ? 0 : MetadataReader.GetString(typeDef.Namespace).Length + 1) +
                    MetadataReader.GetString(typeDef.Name).Length;
            }

            baseType = MetadataTokens.GetToken(typeDef.BaseType);
            attributes = typeDef.Attributes;
        }

        public void GetTypeRefProps(
            int typeReference,
            out int resolutionScope,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] char[] qualifiedName,
            int qualifiedNameBufferLength,
            out int qualifiedNameLength)
        {
            var handle = (TypeReferenceHandle)MetadataTokens.Handle(typeReference);
            var typeRef = MetadataReader.GetTypeReference(handle);

            if (qualifiedName != null)
            {
                StringBuilder builder = new();
                if (!typeRef.Namespace.IsNil)
                {
                    builder.Append(MetadataReader.GetString(typeRef.Namespace));
                    builder.Append('.');
                }

                builder.Append(MetadataReader.GetString(typeRef.Name));
                qualifiedNameLength = Math.Min(builder.Length, Math.Max(0, qualifiedNameBufferLength - 1));
                builder.CopyTo(0, qualifiedName, 0, qualifiedNameLength);
            }
            else
            {
                qualifiedNameLength =
                    (typeRef.Namespace.IsNil ? 0 : MetadataReader.GetString(typeRef.Namespace).Length + 1) +
                    MetadataReader.GetString(typeRef.Name).Length;
            }

            resolutionScope = MetadataTokens.GetToken(typeRef.ResolutionScope);
        }

        #region Not Implemented

        public void CloseEnum(uint handleEnum)
        {
            throw new NotImplementedException();
        }

        public uint CountEnum(uint handleEnum)
        {
            throw new NotImplementedException();
        }

        public uint EnumCustomAttributes(ref uint handlePointerEnum, uint tk, uint tokenType, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]uint[] arrayCustomAttributes, uint countMax)
        {
            throw new NotImplementedException();
        }

        public unsafe uint EnumEvents(ref uint handlePointerEnum, uint td, uint* arrayEvents, uint countMax)
        {
            throw new NotImplementedException();
        }

        public unsafe uint EnumFields(ref uint handlePointerEnum, uint cl, uint* arrayFields, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumFieldsWithName(ref uint handlePointerEnum, uint cl, string stringName, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]uint[] arrayFields, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumInterfaceImpls(ref uint handlePointerEnum, uint td, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]uint[] arrayImpls, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumMemberRefs(ref uint handlePointerEnum, uint tokenParent, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]uint[] arrayMemberRefs, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumMembers(ref uint handlePointerEnum, uint cl, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]uint[] arrayMembers, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumMembersWithName(ref uint handlePointerEnum, uint cl, string stringName, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]uint[] arrayMembers, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumMethodImpls(ref uint handlePointerEnum, uint td, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]uint[] arrayMethodBody, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]uint[] arrayMethodDecl, uint countMax)
        {
            throw new NotImplementedException();
        }

        public unsafe uint EnumMethods(ref uint handlePointerEnum, uint cl, uint* arrayMethods, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumMethodSemantics(ref uint handlePointerEnum, uint mb, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]uint[] arrayEventProp, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumMethodsWithName(ref uint handlePointerEnum, uint cl, string stringName, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]uint[] arrayMethods, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumModuleRefs(ref uint handlePointerEnum, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]uint[] arrayModuleRefs, uint cmax)
        {
            throw new NotImplementedException();
        }

        public uint EnumParams(ref uint handlePointerEnum, uint mb, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]uint[] arrayParams, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumPermissionSets(ref uint handlePointerEnum, uint tk, uint dwordActions, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]uint[] arrayPermission, uint countMax)
        {
            throw new NotImplementedException();
        }

        public unsafe uint EnumProperties(ref uint handlePointerEnum, uint td, uint* arrayProperties, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumSignatures(ref uint handlePointerEnum, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]uint[] arraySignatures, uint cmax)
        {
            throw new NotImplementedException();
        }

        public uint EnumTypeDefs(ref uint handlePointerEnum, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]uint[] arrayTypeDefs, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumTypeRefs(ref uint handlePointerEnum, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]uint[] arrayTypeRefs, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumTypeSpecs(ref uint handlePointerEnum, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]uint[] arrayTypeSpecs, uint cmax)
        {
            throw new NotImplementedException();
        }

        public uint EnumUnresolvedMethods(ref uint handlePointerEnum, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]uint[] arrayMethods, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint EnumUserStrings(ref uint handlePointerEnum, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]uint[] arrayStrings, uint cmax)
        {
            throw new NotImplementedException();
        }

        public uint FindField(uint td, string stringName, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]byte[] voidPointerSigBlob, uint byteCountSigBlob)
        {
            throw new NotImplementedException();
        }

        public uint FindMember(uint td, string stringName, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]byte[] voidPointerSigBlob, uint byteCountSigBlob)
        {
            throw new NotImplementedException();
        }

        public uint FindMemberRef(uint td, string stringName, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]byte[] voidPointerSigBlob, uint byteCountSigBlob)
        {
            throw new NotImplementedException();
        }

        public uint FindMethod(uint td, string stringName, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]byte[] voidPointerSigBlob, uint byteCountSigBlob)
        {
            throw new NotImplementedException();
        }

        public uint FindTypeDefByName(string stringTypeDef, uint tokenEnclosingClass)
        {
            throw new NotImplementedException();
        }

        public uint FindTypeRef(uint tokenResolutionScope, string stringName)
        {
            throw new NotImplementedException();
        }

        public uint GetClassLayout(uint td, out uint pdwPackSize, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]ulong[] arrayFieldOffset, uint countMax, out uint countPointerFieldOffset)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetCustomAttributeByName(uint tokenObj, string stringName, out void* ppData)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetCustomAttributeProps(uint cv, out uint ptkObj, out uint ptkType, out void* ppBlob)
        {
            throw new NotImplementedException();
        }

        public uint GetEventProps(uint ev, out uint pointerClass, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] char[] stringEvent, uint cchEvent, out uint pchEvent, out uint pdwEventFlags, out uint ptkEventType, out uint pmdAddOn, out uint pmdRemoveOn, out uint pmdFire, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 11)]uint[] rmdOtherMethod, uint countMax)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetFieldMarshal(uint tk, out byte* ppvNativeType)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetFieldProps(uint mb, out uint pointerClass, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] char[] stringField, uint cchField, out uint pchField, out uint pdwAttr, out byte* ppvSigBlob, out uint pcbSigBlob, out uint pdwCPlusTypeFlag, out void* ppValue)
        {
            throw new NotImplementedException();
        }

        public uint GetInterfaceImplProps(uint impl, out uint pointerClass)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetMemberProps(uint mb, out uint pointerClass, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] char[] stringMember, uint cchMember, out uint pchMember, out uint pdwAttr, out byte* ppvSigBlob, out uint pcbSigBlob, out uint pulCodeRVA, out uint pdwImplFlags, out uint pdwCPlusTypeFlag, out void* ppValue)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetMemberRefProps(uint mr, ref uint ptk, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] char[] stringMember, uint cchMember, out uint pchMember, out byte* ppvSigBlob)
        {
            throw new NotImplementedException();
        }

        public uint GetMethodProps(uint mb, out uint pointerClass, IntPtr stringMethod, uint cchMethod, out uint pchMethod, IntPtr pdwAttr, IntPtr ppvSigBlob, IntPtr pcbSigBlob, IntPtr pulCodeRVA)
        {
            throw new NotImplementedException();
        }

        public uint GetMethodSemantics(uint mb, uint tokenEventProp)
        {
            throw new NotImplementedException();
        }

        public uint GetModuleFromScope()
        {
            throw new NotImplementedException();
        }

        public uint GetModuleRefProps(uint mur, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] char[] stringName, uint cchName)
        {
            throw new NotImplementedException();
        }

        public uint GetNameFromToken(uint tk)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetNativeCallConvFromSig(void* voidPointerSig, uint byteCountSig)
        {
            throw new NotImplementedException();
        }

        public uint GetNestedClassProps(uint typeDefNestedClass)
        {
            throw new NotImplementedException();
        }

        public int GetParamForMethodIndex(uint md, uint ulongParamSeq, out uint pointerParam)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetParamProps(uint tk, out uint pmd, out uint pulSequence, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] char[] stringName, uint cchName, out uint pchName, out uint pdwAttr, out uint pdwCPlusTypeFlag, out void* ppValue)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetPermissionSetProps(uint pm, out uint pdwAction, out void* ppvPermission)
        {
            throw new NotImplementedException();
        }

        public uint GetPinvokeMap(uint tk, out uint pdwMappingFlags, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] char[] stringImportName, uint cchImportName, out uint pchImportName)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetPropertyProps(uint prop, out uint pointerClass, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] char[] stringProperty, uint cchProperty, out uint pchProperty, out uint pdwPropFlags, out byte* ppvSig, out uint bytePointerSig, out uint pdwCPlusTypeFlag, out void* ppDefaultValue, out uint pcchDefaultValue, out uint pmdSetter, out uint pmdGetter, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 14)]uint[] rmdOtherMethod, uint countMax)
        {
            throw new NotImplementedException();
        }

        public uint GetRVA(uint tk, out uint pulCodeRVA)
        {
            throw new NotImplementedException();
        }

        public Guid GetScopeProps([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] stringName, uint cchName, out uint pchName)
        {
            throw new NotImplementedException();
        }

        public unsafe uint GetTypeSpecFromToken(uint typespec, out byte* ppvSig)
        {
            throw new NotImplementedException();
        }

        public uint GetUserString(uint stk, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] char[] stringString, uint cchString)
        {
            throw new NotImplementedException();
        }

        public int IsGlobal(uint pd)
        {
            throw new NotImplementedException();
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        public bool IsValidToken(uint tk)
        {
            throw new NotImplementedException();
        }

        public void ResetEnum(uint handleEnum, uint ulongPos)
        {
            throw new NotImplementedException();
        }

        public uint ResolveTypeRef(uint tr, in Guid riid, [MarshalAs(UnmanagedType.Interface)]out object ppIScope)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
