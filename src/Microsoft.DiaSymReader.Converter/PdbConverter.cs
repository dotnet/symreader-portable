// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.Debugging;
using System.Linq;
using System.Runtime.CompilerServices;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis;

namespace Microsoft.DiaSymReader.Tools
{
    public sealed class PdbConverter
    {
        /// <summary>
        /// Converts Windows PDB stream to Portable PDB.
        /// </summary>
        /// <param name="peStream">PE image stream (.dll or .exe)</param>
        /// <param name="sourcePdbStream">Source stream of Windows PDB data. Must be readable.</param>
        /// <param name="targetPdbStream">Target stream of Portable PDB data. Must be writable.</param>
        /// <exception cref="ArgumentNullException"><paramref name="peStream"/>, <paramref name="sourcePdbStream"/>, or <paramref name="targetPdbStream"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="sourcePdbStream"/> does not support reading.</exception>
        /// <exception cref="ArgumentException"><paramref name="targetPdbStream"/> does not support writing.</exception>
        /// <exception cref="BadImageFormatException">The format of the PE image or the source PDB image is invalid.</exception>
        /// <exception cref="InvalidDataException">Unexpected data found in the PE image or the source PDB image.</exception>
        public static void Convert(Stream peStream, Stream sourcePdbStream, Stream targetPdbStream)
        {
            ValidateArguments(peStream, sourcePdbStream, targetPdbStream);
            
            var metadataBuilder = new MetadataBuilder();
            ImmutableArray<int> typeSystemRowCounts;
            var debugEntryPointToken = default(MethodDefinitionHandle);
            var pdbId = default(BlobContentId);

            try
            {
                using (var peReader = new PEReader(peStream))
                {
                    pdbId = ReadPdbId(peReader);

                    var symReader = SymReaderFactory.CreateWindowsPdbReader(sourcePdbStream, peReader);

                    var metadataReader = peReader.GetMetadataReader();
                    var metadataModel = new MetadataModel(metadataReader);

                    typeSystemRowCounts = metadataModel.GetRowCounts();
                    debugEntryPointToken = ReadEntryPointHandle(symReader);

                    // documents:
                    var documentIndex = new Dictionary<string, DocumentHandle>(StringComparer.Ordinal);
                    var documents = symReader.GetDocuments();
                    metadataBuilder.SetCapacity(TableIndex.Document, documents.Length);

                    bool vbSemantics = false;

                    foreach (var document in documents)
                    {
                        string name = document.GetName();
                        Guid language = document.GetLanguage();

                        // TODO:
                        // won't work for IL-merged assmemblies
                        vbSemantics |= language == SymReaderHelpers.VisualBasicLanguageGuid;

                        var rid = metadataBuilder.AddDocument(
                            name: metadataBuilder.GetOrAddDocumentName(name),
                            hashAlgorithm: metadataBuilder.GetOrAddGuid(document.GetHashAlgorithm()),
                            hash: metadataBuilder.GetOrAddBlob(document.GetChecksum()),
                            language: metadataBuilder.GetOrAddGuid(language));

                        documentIndex.Add(name, rid);
                    }

                    var lastLocalVariableHandle = default(LocalVariableHandle);
                    var lastLocalConstantHandle = default(LocalConstantHandle);

                    var importStringsByMethod = new Dictionary<int, ImmutableArray<string>>();
                    var importScopesByMethod = new Dictionary<int, ImportScopeHandle>();

                    // Maps import scope content to import scope handles
                    var importScopeIndex = new Dictionary<ImportScopeInfo, ImportScopeHandle>();
                    var importScopes = new List<ImportScopeInfo>();

                    // reserve slot for module import scope:
                    importScopes.Add(default(ImportScopeInfo));

                    var externAliasImports = new List<ImportInfo>();
                    var externAliasStringSet = new HashSet<string>(StringComparer.Ordinal);

                    string vbDefaultNamespace = null;
                    var vbProjectLevelImports = new List<ImportInfo>();

                    // first pass:
                    foreach (var methodHandle in metadataReader.MethodDefinitions)
                    {
                        int methodToken = MetadataTokens.GetToken(methodHandle);
                        ImmutableArray<ImmutableArray<ImportInfo>> importGroups;

                        if (vbSemantics)
                        {
                            var importStrings = CustomDebugInfoReader.GetVisualBasicImportStrings(
                                methodToken,
                                symReader,
                                getMethodImportStrings: (token, sr) => GetImportStrings(token, importStringsByMethod, sr));

                            if (importStrings.IsEmpty)
                            {
                                // no debug info
                                continue;
                            }

                            var vbFileLevelImports = ArrayBuilder<ImportInfo>.GetInstance();
                            foreach (var importString in importStrings)
                            {
                                ImportInfo import;
                                if (TryParseImportString(importString, out import, vbSemantics: true))
                                {
                                    if (import.Kind == ImportTargetKind.DefaultNamespace)
                                    {
                                        vbDefaultNamespace = import.Target;
                                    }
                                    else if (import.Scope == VBImportScopeKind.Project)
                                    {
                                        vbProjectLevelImports.Add(import);
                                    }
                                    else
                                    {
                                        vbFileLevelImports.Add(import);
                                    }
                                }
                            }

                            importGroups = ImmutableArray.Create(vbFileLevelImports.ToImmutableAndFree());
                        }
                        else
                        {
                            ImmutableArray<string> localExternAliasStrings;
                            var importStringGroups = CustomDebugInfoReader.GetCSharpGroupedImportStrings(
                                methodToken,
                                symReader,
                                getMethodCustomDebugInfo: (token, sr) => sr.GetCustomDebugInfo(token, methodVersion: 1),
                                getMethodImportStrings: (token, sr) => GetImportStrings(token, importStringsByMethod, sr),
                                externAliasStrings: out localExternAliasStrings);

                            if (importStringGroups.IsDefault)
                            {
                                // no debug info
                                continue;
                            }

                            if (!localExternAliasStrings.IsDefault)
                            {
                                foreach (var externAlias in localExternAliasStrings)
                                {
                                    ImportInfo import;
                                    if (externAliasStringSet.Add(externAlias) &&
                                        TryParseImportString(externAlias, out import, vbSemantics: false))
                                    {
                                        externAliasImports.Add(import);
                                    }
                                }
                            }

                            importGroups = ImmutableArray.CreateRange(importStringGroups.Select(g => ParseImportStrings(g, vbSemantics: false)));
                        }

                        var importScopeHandle = DefineImportScope(importGroups, importScopeIndex, importScopes);
                        importScopesByMethod.Add(methodToken, importScopeHandle);
                    }

                    // import scopes:
                    metadataBuilder.AddImportScope(
                        parentScope: default(ImportScopeHandle),
                        imports: SerializeModuleImportScope(metadataBuilder, externAliasImports, vbProjectLevelImports, vbDefaultNamespace, metadataModel));

                    for (int i = 1; i < importScopes.Count; i++)
                    {
                        metadataBuilder.AddImportScope(
                            parentScope: importScopes[i].Parent,
                            imports: SerializeImportsBlob(metadataBuilder, importScopes[i].Imports, metadataModel));
                    }

                    var dynamicNames = new Dictionary<string, DynamicLocalInfo>();
                    var dynamicSlots = new Dictionary<int, DynamicLocalInfo>();

                    // methods:
                    metadataBuilder.SetCapacity(TableIndex.MethodDebugInformation, metadataReader.MethodDefinitions.Count);
                    foreach (var methodHandle in metadataReader.MethodDefinitions)
                    {
                        var methodDef = metadataReader.GetMethodDefinition(methodHandle);
                        int methodToken = MetadataTokens.GetToken(methodHandle);

                        var symMethod = symReader.GetMethod(methodToken);
                        if (symMethod == null)
                        {
                            metadataBuilder.AddMethodDebugInformation(default(DocumentHandle), sequencePoints: default(BlobHandle));
                            continue;
                        }

                        // method debug info:
                        int localSignatureRowId;
                        if (methodDef.RelativeVirtualAddress != 0)
                        {
                            var methodBody = peReader.GetMethodBody(methodDef.RelativeVirtualAddress);
                            localSignatureRowId = methodBody.LocalSignature.IsNil ? 0 : MetadataTokens.GetRowNumber(methodBody.LocalSignature);
                        }
                        else
                        {
                            localSignatureRowId = 0;
                        }

                        var symSequencePoints = symMethod.GetSequencePoints().ToImmutableArray();

                        DocumentHandle singleDocumentHandle;
                        BlobHandle sequencePointsBlob = SerializeSequencePoints(metadataBuilder, localSignatureRowId, symSequencePoints, documentIndex, out singleDocumentHandle);

                        metadataBuilder.AddMethodDebugInformation(
                            document: singleDocumentHandle,
                            sequencePoints: sequencePointsBlob);

                        // state machine and async info:
                        var symAsyncMethod = symMethod.AsAsyncMethod();
                        if (symAsyncMethod != null)
                        {
                            var kickoffToken = MetadataTokens.Handle(symAsyncMethod.GetKickoffMethod());
                            metadataBuilder.AddStateMachineMethod(
                                moveNextMethod: methodHandle,
                                kickoffMethod: (MethodDefinitionHandle)kickoffToken);

                            metadataBuilder.AddCustomDebugInformation(
                                parent: methodHandle,
                                kind: metadataBuilder.GetOrAddGuid(PortableCustomDebugInfoKinds.AsyncMethodSteppingInformationBlob),
                                value: SerializeAsyncMethodSteppingInfo(metadataBuilder, symAsyncMethod, MetadataTokens.GetRowNumber(methodHandle)));
                        }

                        // custom debug information:
                        var dynamicLocals = default(ImmutableArray<DynamicLocalInfo>);

                        byte[] customDebugInfoBytes = symReader.GetCustomDebugInfo(methodToken, methodVersion: 1);
                        if (customDebugInfoBytes != null)
                        {
                            foreach (var record in CustomDebugInfoReader.GetCustomDebugInfoRecords(customDebugInfoBytes))
                            {
                                switch (record.Kind)
                                {
                                    case CustomDebugInfoKind.DynamicLocals:
                                        dynamicLocals = CustomDebugInfoReader.DecodeDynamicLocalsRecord(record.Data);
                                        break;

                                    case CustomDebugInfoKind.StateMachineHoistedLocalScopes:
                                        metadataBuilder.AddCustomDebugInformation(
                                            parent: methodHandle,
                                            kind: metadataBuilder.GetOrAddGuid(PortableCustomDebugInfoKinds.EncLocalSlotMap),
                                            value: SerializeStateMachineHoistedLocalsBlob(metadataBuilder, CustomDebugInfoReader.DecodeStateMachineHoistedLocalScopesRecord(record.Data)));
                                        break;

                                    case CustomDebugInfoKind.EditAndContinueLocalSlotMap:
                                        metadataBuilder.AddCustomDebugInformation(
                                            parent: methodHandle,
                                            kind: metadataBuilder.GetOrAddGuid(PortableCustomDebugInfoKinds.EncLocalSlotMap),
                                            value: metadataBuilder.GetOrAddBlob(record.Data));
                                        break;

                                    case CustomDebugInfoKind.EditAndContinueLambdaMap:
                                        metadataBuilder.AddCustomDebugInformation(
                                            parent: methodHandle,
                                            kind: metadataBuilder.GetOrAddGuid(PortableCustomDebugInfoKinds.EncLambdaAndClosureMap),
                                            value: metadataBuilder.GetOrAddBlob(record.Data));
                                        break;
                                }
                            }
                        }

                        var rootScope = symMethod.GetRootScope();
                        if (rootScope.GetNamespaces().Length == 0 || rootScope.GetLocals().Length == 0 || rootScope.GetConstants().Length == 0)
                        {
                            dynamicNames.Clear();
                            dynamicSlots.Clear();

                            foreach (var dynamicLocal in dynamicLocals)
                            {
                                if (dynamicLocal.SlotId == 0)
                                {
                                    // All dynamic constants have slot id == 0, 
                                    // but a variable can also have slot id == 0
                                    if (!dynamicNames.ContainsKey(dynamicLocal.Name))
                                    {
                                        dynamicNames.Add(dynamicLocal.Name, dynamicLocal);
                                    }
                                    else
                                    {
                                        // TODO: warning
                                    }
                                }
                                else if (!dynamicSlots.ContainsKey(dynamicLocal.SlotId))
                                {
                                    dynamicSlots.Add(dynamicLocal.SlotId, dynamicLocal);
                                }
                                else
                                {
                                    // TODO: warning
                                }
                            }

                            foreach (ISymUnmanagedScope scope in rootScope.GetChildren())
                            {
                                SerializeScope(
                                    metadataBuilder,
                                    metadataModel, 
                                    methodHandle, 
                                    importScopesByMethod[methodToken], 
                                    scope, 
                                    dynamicSlots, 
                                    dynamicNames, 
                                    vbSemantics,
                                    ref lastLocalVariableHandle, 
                                    ref lastLocalConstantHandle);
                            }
                        }
                        else
                        {
                            // TODO: warning: 
                            // "Root scope must be empty (method 0x{0:x8})", MetadataTokens.GetToken(methodHandle))
                        }
                    }
                }
            }
            catch (COMException e)
            {
                // TODO: loc
                throw new BadImageFormatException("Invalid PDB format: " + e.Message, e);
            }

            var serializer = new PortablePdbBuilder(metadataBuilder, typeSystemRowCounts, debugEntryPointToken, idProvider: _ => pdbId);
            BlobBuilder blobBuilder = new BlobBuilder();
            serializer.Serialize(blobBuilder);
            blobBuilder.WriteContentTo(targetPdbStream);
        }

        private static BlobHandle SerializeStateMachineHoistedLocalsBlob(MetadataBuilder metadataBuilder, ImmutableArray<StateMachineHoistedLocalScope> scopes)
        {
            var builder = new BlobBuilder();

            foreach (var scope in scopes)
            {
                builder.WriteInt32(scope.StartOffset);
                builder.WriteInt32(scope.EndOffset - scope.StartOffset);
            }

            return metadataBuilder.GetOrAddBlob(builder);
        }

        private static BlobHandle SerializeAsyncMethodSteppingInfo(MetadataBuilder metadataBuilder, ISymUnmanagedAsyncMethod symAsyncMethod, int moveNextMethodRowId)
        {
            var builder = new BlobBuilder();

            builder.WriteUInt32((uint)((long)symAsyncMethod.GetCatchHandlerILOffset() + 1));

            foreach (var stepInfo in symAsyncMethod.GetAsyncStepInfos())
            {
                builder.WriteInt32(stepInfo.YieldOffset);
                builder.WriteInt32(stepInfo.ResumeOffset);
                builder.WriteCompressedInteger(moveNextMethodRowId);
            }

            return metadataBuilder.GetOrAddBlob(builder);
        }

        private static bool TryParseImportString(string importString, out ImportInfo import, bool vbSemantics)
        {
            ImportTargetKind kind;
            string target;
            string alias;
            string externAlias = null;
            var scope = VBImportScopeKind.Unspecified;

            if (vbSemantics ? 
                CustomDebugInfoReader.TryParseVisualBasicImportString(importString, out alias, out target, out kind, out scope) :
                CustomDebugInfoReader.TryParseCSharpImportString(importString, out alias, out externAlias, out target, out kind))
            {
                import = new ImportInfo(kind, target, alias, externAlias, scope);
                return true;
            }

            // TODO: report warning
            import = default(ImportInfo);
            return false;
        }

        private static ImmutableArray<string> GetImportStrings(int token, Dictionary<int, ImmutableArray<string>> cache, ISymUnmanagedReader3 reader)
        {
            ImmutableArray<string> result;
            if (!cache.TryGetValue(token, out result))
            {
                result = SymReaderHelpers.GetImportStrings(reader, token, methodVersion: 1);
                cache.Add(token, result);
            }

            return result;
        }

        private static BlobContentId ReadPdbId(PEReader peReader)
        {
            foreach (var entry in peReader.ReadDebugDirectory())
            {
                if (entry.Type == DebugDirectoryEntryType.CodeView)
                {
                    // TODO: const
                    if (entry.MajorVersion == 0x504D)
                    {
                        // TODO: loc
                        throw new InvalidDataException("Specified PE was built with Portable PDB.");
                    }

                    return new BlobContentId(peReader.ReadCodeViewDebugDirectoryData(entry).Guid, entry.Stamp);
                }
            }

            // TODO: loc
            throw new InvalidDataException("Specified PE file doesn't have any PDB associated with it.");
        }

        private static MethodDefinitionHandle ReadEntryPointHandle(ISymUnmanagedReader symReader)
        {
            var handle = MetadataTokens.EntityHandle(symReader.GetUserEntryPoint());
            if (handle.IsNil)
            {
                return default(MethodDefinitionHandle);
            }

            if (handle.Kind != HandleKind.MethodDefinition)
            {
                // TODO: loc
                throw new BadImageFormatException("Invalid user entry point in the source PDB");
            }

            return (MethodDefinitionHandle)handle;
        }

        private static void ValidateArguments(Stream peStream, Stream sourcePdbStream, Stream targetPdbStream)
        {
            if (peStream == null)
            {
                throw new ArgumentNullException(nameof(peStream));
            }

            if (sourcePdbStream == null)
            {
                throw new ArgumentNullException(nameof(sourcePdbStream));
            }

            if (!sourcePdbStream.CanRead)
            {
                // TODO: localize
                throw new ArgumentException("Stream must be readable", nameof(sourcePdbStream));
            }

            if (targetPdbStream == null)
            {
                throw new ArgumentNullException(nameof(targetPdbStream));
            }

            if (!targetPdbStream.CanWrite)
            {
                // TODO: localize
                throw new ArgumentException("Stream must be writable", nameof(targetPdbStream));
            }
        }

        private static readonly ImportScopeHandle ModuleImportScopeHandle = MetadataTokens.ImportScopeHandle(1);

        private struct ImportScopeInfo : IEquatable<ImportScopeInfo>
        {
            public readonly ImportScopeHandle Parent;
            public readonly ImmutableArray<ImportInfo> Imports;

            public ImportScopeInfo(ImmutableArray<ImportInfo> imports, ImportScopeHandle parent)
            {
                Parent = parent;
                Imports = imports;
            }

            public override bool Equals(object obj) => obj is ImportScopeInfo && Equals((ImportScopeInfo)obj);
            public bool Equals(ImportScopeInfo other) => Parent == other.Parent && Imports.SequenceEqual(other.Imports);
            public override int GetHashCode() => Hash.Combine(Parent.GetHashCode(), Hash.CombineValues(Imports));
        }

        private static ImportScopeHandle DefineImportScope(
            ImmutableArray<ImmutableArray<ImportInfo>> importGroups,
            Dictionary<ImportScopeInfo, ImportScopeHandle> importScopeIndex,
            List<ImportScopeInfo> importScopes)
        {
            ImportScopeHandle parentHandle = ModuleImportScopeHandle;
            for (int i = importGroups.Length - 1; i >= 0; i--)
            {
                var info = new ImportScopeInfo(importGroups[i], parentHandle);

                ImportScopeHandle existingScopeHandle;
                if (importScopeIndex.TryGetValue(info, out existingScopeHandle))
                {
                    parentHandle = existingScopeHandle;
                }
                else
                {
                    importScopes.Add(info);
                    parentHandle = MetadataTokens.ImportScopeHandle(importScopes.Count);
                }
            }

            return parentHandle;
        }

        private static ImmutableArray<ImportInfo> ParseImportStrings(ImmutableArray<string> importStrings, bool vbSemantics)
        {
            var builder = ArrayBuilder<ImportInfo>.GetInstance();
            foreach (var importString in importStrings)
            {
                ImportInfo import;
                if (TryParseImportString(importString, out import, vbSemantics))
                {
                    builder.Add(import);
                }
            }

            return builder.ToImmutableAndFree();
        }

        private static BlobHandle SerializeModuleImportScope(
            MetadataBuilder metadataBuilder,
            IEnumerable<ImportInfo> csExternAliasImports,
            IEnumerable<ImportInfo> vbProjectLevelImports,
            string vbDefaultNamespace,
            MetadataModel metadataModel)
        {
            // module-level import scope:
            var builder = new BlobBuilder();
            var encoder = new ImportDefinitionEncoder(metadataBuilder, builder);

            if (vbDefaultNamespace != null)
            {
                SerializeModuleDefaultNamespace(metadataBuilder, vbDefaultNamespace);
            }

            foreach (var import in csExternAliasImports)
            {
                SerializeImport(encoder, import, metadataModel);
            }

            foreach (var import in vbProjectLevelImports)
            {
                SerializeImport(encoder, import, metadataModel);
            }

            return metadataBuilder.GetOrAddBlob(builder);
        }

        private static void SerializeModuleDefaultNamespace(MetadataBuilder metadataBuilder, string namespaceName)
        {
            metadataBuilder.AddCustomDebugInformation(
                parent: EntityHandle.ModuleDefinition,
                kind: metadataBuilder.GetOrAddGuid(PortableCustomDebugInfoKinds.DefaultNamespace),
                value: metadataBuilder.GetOrAddBlobUTF8(namespaceName));
        }
        
        private static BlobHandle SerializeImportsBlob(MetadataBuilder metadataBuilder, ImmutableArray<ImportInfo> imports, MetadataModel metadataModel)
        {
            var builder = new BlobBuilder();
            var encoder = new ImportDefinitionEncoder(metadataBuilder, builder);

            foreach (var import in imports)
            {
                SerializeImport(encoder, import, metadataModel);
            }

            return metadataBuilder.GetOrAddBlob(builder);
        }

        private struct ImportInfo
        {
            public readonly ImportTargetKind Kind;
            public readonly string Target;
            public readonly string ExternAlias;
            public readonly string Alias;
            public readonly VBImportScopeKind Scope;

            public ImportInfo(ImportTargetKind kind, string target, string alias, string externAlias, VBImportScopeKind scope)
            {
                Kind = kind;
                Target = target;
                Alias = alias;
                ExternAlias = externAlias;
                Scope = scope;
            }
        }

        private static void SerializeImport(ImportDefinitionEncoder encoder, ImportInfo import, MetadataModel metadataModel)
        {
            var assemblyRef = default(AssemblyReferenceHandle);
            EntityHandle type;
            switch (import.Kind)
            {
                case ImportTargetKind.Assembly:
                    // alias: assembly alias 
                    // target: assembly name for module-level extern alias definition, or null for file level extern alias import

                    if (import.Target == null)
                    {
                        // TODO: skip if the alias isn't defined in an ancestor scope?
                        encoder.ImportAssemblyReferenceAlias(import.Alias);
                        break;
                    }

                    if (!metadataModel.TryResolveAssemblyReference(import.Target, out assemblyRef))
                    {
                        // no type from the assembly is used, the AssemblyRef is not present in the metadata
                        break;
                    }

                    encoder.AliasAssemblyReference(assemblyRef, import.Alias);
                    break;

                case ImportTargetKind.Namespace:
                    if (import.ExternAlias != null && !metadataModel.TryResolveAssemblyReference(import.ExternAlias, out assemblyRef))
                    {
                        // no type from the assembly is used, the AssemblyRef is not present in the metadata
                        break;
                    }

                    encoder.Namespace(import.Target, import.Alias, assemblyRef);
                    break;

                case ImportTargetKind.Type:
                    if (!metadataModel.TryResolveType(import.Target, out type))
                    {
                        // the type is not used in the source, the metadata is missing a TypeRef.
                        break;
                    }

                    encoder.Type(type, import.Alias);
                    break;

                case ImportTargetKind.NamespaceOrType:
                    if (metadataModel.TryResolveType(import.Target, out type))
                    {
                        encoder.Type(type, import.Alias);
                    }
                    else
                    {
                        encoder.Namespace(import.Target, import.Alias);
                    }

                    break;

                case ImportTargetKind.XmlNamespace:
                    encoder.XmlNamespace(import.Alias, import.Target);
                    break;

                case ImportTargetKind.DefaultNamespace:
                    // alraedy handled 
                    throw ExceptionUtilities.Unreachable;

                case ImportTargetKind.CurrentNamespace:
                case ImportTargetKind.MethodToken:
                case ImportTargetKind.Defunct:
                    break;
            }
        }

        private static void SerializeScope(
            MetadataBuilder metadataBuilder,
            MetadataModel metadataModel,
            MethodDefinitionHandle methodHandle,
            ImportScopeHandle importScopeHandle,
            ISymUnmanagedScope symScope,
            Dictionary<int, DynamicLocalInfo> dynamicSlots,
            Dictionary<string, DynamicLocalInfo> dynamicNames,
            bool vbSemantics,
            ref LocalVariableHandle lastLocalVariableHandle,
            ref LocalConstantHandle lastLocalConstantHandle)
        {
            // VB Windows PDB encode the range as end-inclusive, 
            // all Portable PDBs use end-exclusive encoding.
            int start = symScope.GetStartOffset();
            int end = symScope.GetEndOffset() + (vbSemantics ? 1 : 0);

            metadataBuilder.AddLocalScope(
                method: methodHandle,
                importScope: importScopeHandle,
                variableList: NextHandle(lastLocalVariableHandle),
                constantList: NextHandle(lastLocalConstantHandle),
                startOffset: start,
                length: end - start);

            foreach (var symLocal in symScope.GetLocals())
            {
                int slot = symLocal.GetSlot();
                string name = symLocal.GetName();

                lastLocalVariableHandle = metadataBuilder.AddLocalVariable(
                    attributes: (LocalVariableAttributes)symLocal.GetAttributes(),
                    index: slot,
                    name: metadataBuilder.GetOrAddString(name));

                DynamicLocalInfo dynamicInfo;
                if (slot > 0 && dynamicSlots.TryGetValue(slot, out dynamicInfo) ||
                    slot == 0 && dynamicNames.TryGetValue(name, out dynamicInfo))
                {
                    metadataBuilder.AddCustomDebugInformation(
                        parent: lastLocalVariableHandle,
                        kind: metadataBuilder.GetOrAddGuid(PortableCustomDebugInfoKinds.DynamicLocalVariables),
                        value: SerializeDynamicLocalBlob(metadataBuilder, dynamicInfo));
                }
            }

            foreach (var symConstant in symScope.GetConstants())
            {
                string name = symConstant.GetName();
                object value = symConstant.GetValue();

                lastLocalConstantHandle = metadataBuilder.AddLocalConstant(
                    name: metadataBuilder.GetOrAddString(name),
                    signature: SerializeConstantSignature(metadataBuilder, metadataModel, symConstant.GetSignature(), value));

                DynamicLocalInfo dynamicInfo;
                if (dynamicNames.TryGetValue(name, out dynamicInfo))
                {
                    metadataBuilder.AddCustomDebugInformation(
                        parent: lastLocalConstantHandle,
                        kind: metadataBuilder.GetOrAddGuid(PortableCustomDebugInfoKinds.DynamicLocalVariables),
                        value: SerializeDynamicLocalBlob(metadataBuilder, dynamicInfo));
                }
            }

            int previousChildScopeEnd = start;
            foreach (ISymUnmanagedScope child in symScope.GetChildren())
            {
                int childScopeStart = child.GetStartOffset();
                int childScopeEnd = child.GetEndOffset();

                // scopes are properly nested:
                if (childScopeStart < previousChildScopeEnd || childScopeEnd > end)
                {
                    // TODO: loc/warning
                    throw new BadImageFormatException($"Invalid scope IL offset range: [{childScopeStart}, {childScopeEnd}), method 0x{MetadataTokens.GetToken(methodHandle):x}.");
                }

                previousChildScopeEnd = childScopeEnd;

                SerializeScope(metadataBuilder, metadataModel, methodHandle, importScopeHandle, child, dynamicSlots, dynamicNames, vbSemantics, ref lastLocalVariableHandle, ref lastLocalConstantHandle);
            }
        }

        private unsafe static BlobHandle SerializeConstantSignature(MetadataBuilder metadataBuilder, MetadataModel metadataModel, byte[] signature, object value)
        {
            var builder = new BlobBuilder();
            ConstantSignatureConverter.Convert(builder, metadataModel, signature, value);
            return metadataBuilder.GetOrAddBlob(builder);
        }

        private static BlobHandle SerializeDynamicLocalBlob(MetadataBuilder metadataBuilder, DynamicLocalInfo dynamicInfo)
        {
            Debug.Assert(dynamicInfo.FlagCount > 0);
            Debug.Assert(dynamicInfo.Flags != 0);

            var builder = new BlobBuilder();
            
            int c = dynamicInfo.FlagCount - 1;
            ulong flags = dynamicInfo.Flags;

            // trim trailing 0s:
            while ((flags & (1u << c)) == 0) c--;

            int b = 0;
            int shift = 0;
            for (int i = 0; i <= c; i++)
            {
                bool bit = (flags & (1u << c)) != 0;

                if (bit)
                {
                    b |= 1 << shift;
                }

                if (shift == 7)
                {
                    builder.WriteByte((byte)b);
                    b = 0;
                    shift = 0;
                }
                else
                {
                    shift++;
                }
            }

            if (b != 0)
            {
                builder.WriteByte((byte)b);
            }

            return metadataBuilder.GetOrAddBlob(builder);
        }

        private static LocalVariableHandle NextHandle(LocalVariableHandle handle) =>
            MetadataTokens.LocalVariableHandle(MetadataTokens.GetRowNumber(handle) + 1);

        private static LocalConstantHandle NextHandle(LocalConstantHandle handle) =>
            MetadataTokens.LocalConstantHandle(MetadataTokens.GetRowNumber(handle) + 1);

        private static BlobHandle SerializeSequencePoints(
            MetadataBuilder metadataBuilder,
            int localSignatureRowId,
            ImmutableArray<SymUnmanagedSequencePoint> sequencePoints,
            Dictionary<string, DocumentHandle> documentIndex,
            out DocumentHandle singleDocumentHandle)
        {
            if (sequencePoints.Length == 0)
            {
                singleDocumentHandle = default(DocumentHandle);
                return default(BlobHandle);
            }

            var writer = new BlobBuilder();

            int previousNonHiddenStartLine = -1;
            int previousNonHiddenStartColumn = -1;

            // header:
            writer.WriteCompressedInteger(localSignatureRowId);

            DocumentHandle previousDocument = TryGetSingleDocument(sequencePoints, documentIndex);
            singleDocumentHandle = previousDocument;

            for (int i = 0; i < sequencePoints.Length; i++)
            {
                var currentDocument = documentIndex[sequencePoints[i].Document.GetName()];
                if (previousDocument != currentDocument)
                {
                    // optional document in header or document record:
                    if (!previousDocument.IsNil)
                    {
                        writer.WriteCompressedInteger(0);
                    }

                    writer.WriteCompressedInteger(MetadataTokens.GetHeapOffset(currentDocument));
                    previousDocument = currentDocument;
                }

                // delta IL offset:
                if (i > 0)
                {
                    writer.WriteCompressedInteger((sequencePoints[i].Offset - sequencePoints[i - 1].Offset));
                }
                else
                {
                    writer.WriteCompressedInteger(sequencePoints[i].Offset);
                }

                if (sequencePoints[i].IsHidden)
                {
                    writer.WriteInt16(0);
                    continue;
                }

                // Delta Lines & Columns:
                SerializeDeltaLinesAndColumns(writer, sequencePoints[i]);

                // delta Start Lines & Columns:
                if (previousNonHiddenStartLine < 0)
                {
                    Debug.Assert(previousNonHiddenStartColumn < 0);
                    writer.WriteCompressedInteger(sequencePoints[i].StartLine);
                    writer.WriteCompressedInteger(sequencePoints[i].StartColumn);
                }
                else
                {
                    writer.WriteCompressedSignedInteger(sequencePoints[i].StartLine - previousNonHiddenStartLine);
                    writer.WriteCompressedSignedInteger(sequencePoints[i].StartColumn - previousNonHiddenStartColumn);
                }

                previousNonHiddenStartLine = sequencePoints[i].StartLine;
                previousNonHiddenStartColumn = sequencePoints[i].StartColumn;
            }

            return metadataBuilder.GetOrAddBlob(writer);
        }

        private static DocumentHandle TryGetSingleDocument(ImmutableArray<SymUnmanagedSequencePoint> sequencePoints, Dictionary<string, DocumentHandle> documentIndex)
        {
            DocumentHandle singleDocument = documentIndex[sequencePoints[0].Document.GetName()];
            for (int i = 1; i < sequencePoints.Length; i++)
            {
                if (documentIndex[sequencePoints[i].Document.GetName()] != singleDocument)
                {
                    return default(DocumentHandle);
                }
            }

            return singleDocument;
        }

        private static void SerializeDeltaLinesAndColumns(BlobBuilder writer, SymUnmanagedSequencePoint sequencePoint)
        {
            int deltaLines = sequencePoint.EndLine - sequencePoint.StartLine;
            int deltaColumns = sequencePoint.EndColumn - sequencePoint.StartColumn;

            // only hidden sequence points have zero width
            Debug.Assert(deltaLines != 0 || deltaColumns != 0 || sequencePoint.IsHidden);

            writer.WriteCompressedInteger(deltaLines);

            if (deltaLines == 0)
            {
                writer.WriteCompressedInteger(deltaColumns);
            }
            else
            {
                writer.WriteCompressedSignedInteger(deltaColumns);
            }
        }
    }
}
