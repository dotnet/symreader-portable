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

namespace Microsoft.DiaSymReader.Tools
{
    public sealed class PdbConverter
    {
        public static void Convert(Stream peStream, Stream sourcePdbStream, Stream targetPdbStream)
        {
            if (peStream == null)
            {
                throw new ArgumentNullException(nameof(peStream));
            }

            if (sourcePdbStream == null)
            {
                throw new ArgumentNullException(nameof(sourcePdbStream));
            }

            if (targetPdbStream == null)
            {
                throw new ArgumentNullException(nameof(targetPdbStream));
            }

            var metadataBuilder = new MetadataBuilder();
            ImmutableArray<int> typeSystemRowCounts;
            var debugEntryPointToken = default(MethodDefinitionHandle);
            Guid pdbGuid = Guid.Empty;
            uint pdbStamp = 0;

            using (var peReader = new PEReader(peStream))
            {
                foreach (var entry in peReader.ReadDebugDirectory())
                {
                    if (entry.Type == DebugDirectoryEntryType.CodeView)
                    {
                        pdbGuid = peReader.ReadCodeViewDebugDirectoryData(entry).Guid;
                        pdbStamp = entry.Stamp;
                        break;
                    }
                }

                var symReader = SymReaderFactory.CreateWindowsPdbReader(sourcePdbStream, peReader);

                var metadataReader = peReader.GetMetadataReader();
                typeSystemRowCounts = GetRowCounts(metadataReader);
                debugEntryPointToken = (MethodDefinitionHandle)MetadataTokens.EntityHandle(symReader.GetUserEntryPoint());

                // documents:
                var documentIndex = new Dictionary<string, DocumentHandle>();
                var documents = symReader.GetDocuments();
                metadataBuilder.SetCapacity(TableIndex.Document, documents.Length);

                foreach (var document in documents)
                {
                    string name = document.GetName();

                    var rid = metadataBuilder.AddDocument(
                        name: GetOrAddDocumentName(metadataBuilder, name),
                        hashAlgorithm: metadataBuilder.GetOrAddGuid(document.GetHashAlgorithm()),
                        hash: metadataBuilder.GetOrAddBlob(document.GetChecksum()),
                        language: metadataBuilder.GetOrAddGuid(document.GetLanguage()));

                    documentIndex.Add(name, rid);
                }

                var lastLocalVariableRid = default(LocalVariableHandle);
                var lastLocalConstantRid = default(LocalConstantHandle);

                // methods:
                metadataBuilder.SetCapacity(TableIndex.MethodDebugInformation, metadataReader.MethodDefinitions.Count);
                foreach (var methodHandle in metadataReader.MethodDefinitions)
                {
                    var methodDef = metadataReader.GetMethodDefinition(methodHandle);

                    int localSignatureRowId;
                    if (methodDef.RelativeVirtualAddress != 0)
                    {
                        var methodBody = peReader.GetMethodBody(methodDef.RelativeVirtualAddress);
                        localSignatureRowId = methodBody.LocalSignature.IsNil ? 0 : MetadataTokens.GetToken(methodBody.LocalSignature);
                    }
                    else
                    {
                        localSignatureRowId = 0;
                    }

                    var symMethod = symReader.GetMethod(MetadataTokens.GetToken(methodHandle));
                    if (symMethod == null)
                    {
                        metadataBuilder.AddMethodDebugInformation(default(DocumentHandle), sequencePoints: default(BlobHandle));
                        continue;
                    }

                    var symSequencePoints = symMethod.GetSequencePoints().ToImmutableArray();

                    DocumentHandle singleDocumentRowId;
                    BlobHandle sequencePointsBlob = SerializeSequencePoints(metadataBuilder, localSignatureRowId, symSequencePoints, documentIndex, out singleDocumentRowId);

                    metadataBuilder.AddMethodDebugInformation(
                        document: singleDocumentRowId,
                        sequencePoints: sequencePointsBlob);

                    // async info:
                    var symAsyncMethod = symMethod.AsAsyncMethod();
                    if (symAsyncMethod != null)
                    {
                        // TODO
                    }

                    // enc debug info:
                    // TODO


                    // TODO: import scopes
                    var importScopeHandle = default(ImportScopeHandle);

                    // scopes:
                    var rootScope = symMethod.GetRootScope();

                    // TODO: error
                    Debug.Assert(rootScope.GetNamespaces().Length == 0 && rootScope.GetLocals().Length == 0 && rootScope.GetConstants().Length == 0);

                    foreach (ISymUnmanagedScope scope in rootScope.GetChildren())
                    {
                        SerializeScope(metadataBuilder, methodHandle, importScopeHandle, scope, ref lastLocalVariableRid, ref lastLocalConstantRid);
                    }
                }
            }

            var serializer = new PortablePdbBuilder(metadataBuilder, typeSystemRowCounts, debugEntryPointToken, idProvider: _ => new BlobContentId(pdbGuid, pdbStamp));
            BlobBuilder blobBuilder = new BlobBuilder();
            serializer.Serialize(blobBuilder);
            blobBuilder.WriteContentTo(targetPdbStream);
        }

        private static readonly char[] s_separator1 = { '/' };
        private static readonly char[] s_separator2 = { '\\' };

        private static BlobHandle GetOrAddDocumentName(MetadataBuilder builder, string name)
        {
            Debug.Assert(name != null);

            var writer = new BlobBuilder();

            int c1 = Count(name, s_separator1[0]);
            int c2 = Count(name, s_separator2[0]);
            char[] separator = (c1 >= c2) ? s_separator1 : s_separator2;

            writer.WriteByte((byte)separator[0]);

            // TODO: avoid allocations
            foreach (var part in name.Split(separator))
            {
                BlobHandle partIndex = builder.GetOrAddBlob(ImmutableArray.Create(Encoding.UTF8.GetBytes(part)));
                writer.WriteCompressedInteger(MetadataTokens.GetHeapOffset(partIndex));
            }

            return builder.GetOrAddBlob(writer);
        }

        private static int Count(string str, char c)
        {
            int count = 0;
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == c)
                {
                    count++;
                }
            }

            return count;
        }

        private static ImmutableArray<int> GetRowCounts(MetadataReader reader)
        {
            var builder = ImmutableArray.CreateBuilder<int>(MetadataTokens.TableCount);
            for (int i = 0; i < MetadataTokens.TableCount; i++)
            {
                builder.Add(reader.GetTableRowCount((TableIndex)i));
            }

            return builder.MoveToImmutable();
        }

        private static void SerializeScope(
            MetadataBuilder metadataBuilder, 
            MethodDefinitionHandle methodHandle,
            ImportScopeHandle importScopeHandle,
            ISymUnmanagedScope scope,
            ref LocalVariableHandle lastLocalVariableHandle, 
            ref LocalConstantHandle lastLocalConstantHandle)
        {
            // TODO: VB inclusive range end
            int start = scope.GetStartOffset();
            int end = scope.GetEndOffset();

            metadataBuilder.AddLocalScope(
                method: methodHandle,
                importScope: importScopeHandle,
                variableList: NextHandle(lastLocalVariableHandle),
                constantList: NextHandle(lastLocalConstantHandle),
                startOffset: start,
                length: end - start);

            foreach (var symLocal in scope.GetLocals())
            {
                lastLocalVariableHandle = metadataBuilder.AddLocalVariable(
                    attributes: (LocalVariableAttributes)symLocal.GetAttributes(),
                    index: symLocal.GetSlot(),
                    name: metadataBuilder.GetOrAddString(symLocal.GetName()));

                // TODO: dynamic info
            }

            // TODO
            //foreach (var symConstant in scope.GetConstants())
            //{
            //    lastLocalConstantRid = tables.AddLocalConstant(
            //        name: heaps.GetStringIndex(symConstant.GetName()),
            //        signature: new BlobIdx(0)); // TODO

            //    // TODO: dynamic info
            //}

            int previousChildScopeEnd = start;
            foreach (ISymUnmanagedScope child in scope.GetChildren())
            {
                int childScopeStart = child.GetStartOffset();
                int childScopeEnd = child.GetEndOffset();

                // scopes are properly nested:
                if (childScopeStart < previousChildScopeEnd || childScopeEnd > end)
                {
                    throw new BadImageFormatException($"Invalid scope IL offset range: [{childScopeStart}, {childScopeEnd}), method 0x{MetadataTokens.GetToken(methodHandle):x}.");
                }

                previousChildScopeEnd = childScopeEnd;

                SerializeScope(metadataBuilder, methodHandle, importScopeHandle, child, ref lastLocalVariableHandle, ref lastLocalConstantHandle);
            }
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
            out DocumentHandle singleDocumentRowId)
        {
            if (sequencePoints.Length == 0)
            {
                singleDocumentRowId = default(DocumentHandle);
                return default(BlobHandle);
            }

            var writer = new BlobBuilder();

            int previousNonHiddenStartLine = -1;
            int previousNonHiddenStartColumn = -1;

            // header:
            writer.WriteCompressedInteger(localSignatureRowId);

            DocumentHandle previousDocument = TryGetSingleDocument(sequencePoints, documentIndex);
            singleDocumentRowId = previousDocument;

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
