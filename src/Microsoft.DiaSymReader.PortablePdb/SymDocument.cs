// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace Microsoft.DiaSymReader.PortablePdb
{
    [ComVisible(false)]
    public sealed class SymDocument : ISymUnmanagedDocument
    {
        private static Guid s_CSharpGuid = new Guid("3f5162f8-07c6-11d3-9053-00c04fa302a1");
        private static Guid s_visualBasicGuid = new Guid("3a12d0b8-c26c-11d0-b442-00a0244a1dd2");
        private static Guid s_FSharpGuid = new Guid("ab4f38c9-b6e6-43ba-be3b-58080b2ccce3");
        private static Guid s_sha1Guid = new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460");
        private static Guid s_sha256Guid = new Guid("8829d00f-11b8-4213-878b-770e8597ac16");

        private static Guid s_vendorMicrosoftGuid = new Guid("994b45c4-e6e9-11d2-903f-00c04fa302a1");
        private static Guid s_documentTypeGuid = new Guid("5a869d0b-6611-11d3-bd2a-0000f80849bd");

        internal DocumentHandle Handle { get; }
        internal PortablePdbReader PdbReader { get; }

        internal SymDocument(PortablePdbReader pdbReader, DocumentHandle documentHandle)
        {
            Debug.Assert(pdbReader != null);
            PdbReader = pdbReader;
            Handle = documentHandle;
        }

        internal SymReader SymReader => PdbReader.SymReader;
        internal DocumentId GetId() => PdbReader.GetDocumentId(Handle);

        public int FindClosestLine(int line, out int closestLine)
        {
            // Find a minimal sequence point start line in this document 
            // that is greater than or equal to the given line.

            int result = int.MaxValue;
            var methodMap = SymReader.GetMethodMap();
            var methodExtents = SymReader.GetMethodExtents();

            // Note DiaSymReader searches across all documents with the same file name in CDiaWrapper::FindClosestLineAcrossFileIDs. We don't.
            foreach (var extent in methodExtents.EnumerateContainingOrClosestFollowingMethodExtents(GetId(), line))
            {
                Debug.Assert(extent.MaxLine >= line);

                // extent is further than a sequence point we already found:
                if (extent.MinLine >= result)
                {
                    continue;
                }

                // enumerate method sequence points:
                var info = methodMap.GetInfo(extent.Method);
                var pdbReader = SymReader.GetReader(info.Version);
                var debugInfo = pdbReader.MetadataReader.GetMethodDebugInformation(info.Handle);

                if (!SymReader.TryGetLineDeltas(extent.Method, out var deltas))
                {
                    deltas = default(MethodLineDeltas);
                }

                int sequencePointIndex = 0;
                foreach (var sequencePoint in debugInfo.GetSequencePoints())
                {
                    if (sequencePoint.IsHidden || sequencePoint.Document != Handle)
                    {
                        sequencePointIndex++;
                        continue;
                    }

                    int startLine = sequencePoint.StartLine + deltas.GetDeltaForSequencePoint(sequencePointIndex);
                    if (startLine >= line && startLine < result)
                    {
                        result = startLine;
                    }

                    sequencePointIndex++;
                }
            }

            if (result < int.MaxValue)
            {
                closestLine = result;
                return HResult.S_OK;
            }

            closestLine = 0;
            return HResult.E_FAIL;
        }

        public int GetChecksum(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]byte[] checksum)
        {
            // diasymreader returns no checksum for documents once EnC has been performed:
            if (SymReader.Version > 1)
            {
                count = 0;
                return HResult.S_FALSE;
            }

            var document = PdbReader.MetadataReader.GetDocument(Handle);
            if (document.Hash.IsNil)
            {
                count = 0;
                return HResult.S_FALSE;
            }

            var hash = PdbReader.MetadataReader.GetBlobBytes(document.Hash);
            return InteropUtilities.BytesToBuffer(hash, bufferLength, out count, checksum);
        }

        public int GetChecksumAlgorithmId(ref Guid algorithm)
        {
            // diasymreader returns no checksum for documents once EnC has been performed:
            if (SymReader.Version > 1)
            {
                algorithm = default(Guid);
                return HResult.S_FALSE;
            }

            var document = PdbReader.MetadataReader.GetDocument(Handle);
            algorithm = PdbReader.MetadataReader.GetGuid(document.HashAlgorithm);
            return HResult.S_OK;
        }

        public int GetDocumentType(ref Guid documentType)
        {
            documentType = s_documentTypeGuid;
            return HResult.S_OK;
        }

        public int GetLanguage(ref Guid language)
        {
            var document = PdbReader.MetadataReader.GetDocument(Handle);
            language = PdbReader.MetadataReader.GetGuid(document.Language);
            return HResult.S_OK;
        }

        public int GetLanguageVendor(ref Guid vendor)
        {
            var document = PdbReader.MetadataReader.GetDocument(Handle);
            Guid languageId = PdbReader.MetadataReader.GetGuid(document.Language);
            vendor = s_vendorMicrosoftGuid;
            return HResult.S_OK;
        }

        public int GetSourceLength(out int length)
        {
            length = GetEmbeddedSourceBlobReader().Length;
            return HResult.S_OK;
        }

        public int GetSourceRange(
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4), Out]byte[] source)
        {
            count = 0;

            // This function used to return E_NOTIMPL in all implementations. When filling in the
            // implementation, it was deemed not very useful and impractical to actually decompress
            // and decode text and interpret lines and columns here. A convention was adopted that 
            // (0, 0, >= int.MaxValue, >= int.MaxValue) is a maximal range indicating that all
            // source bytes should be returned. Anything else is rejected as invalid. This matches 
            // the new native behavior.
            if (startLine != 0 || 
                startColumn != 0 ||
                unchecked((uint)endLine) < int.MaxValue ||
                unchecked((uint)endColumn) < int.MaxValue)
            {
                return HResult.E_INVALIDARG;
            }

            if (bufferLength < 0)
            {
                return HResult.E_INVALIDARG;
            }

            if (source == null && bufferLength > 0)
            {
                return HResult.E_INVALIDARG;
            }

            BlobReader reader = GetEmbeddedSourceBlobReader();
            if (reader.Length == 0)
            {
                return HResult.S_FALSE;
            }

            count = Math.Min(bufferLength, reader.Length);
            reader.ReadBytes(count, source, 0);
            return HResult.S_OK;
        }

        public int GetUrl(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]char[] url)
        {
            string name = PdbReader.MetadataReader.GetString(PdbReader.MetadataReader.GetDocument(Handle).Name);
            return InteropUtilities.StringToBuffer(name, bufferLength, out count, url);
        }

        public int HasEmbeddedSource(out bool value)
        {
            value = GetEmbeddedSourceBlobReader().Length > 0;
            return HResult.S_OK;
        }

        private BlobReader GetEmbeddedSourceBlobReader()
        {
            BlobHandle blobHandle = PdbReader.MetadataReader.GetCustomDebugInformation(Handle, MetadataUtilities.EmbeddedSourceId);
            return blobHandle.IsNil ? default(BlobReader) : PdbReader.MetadataReader.GetBlobReader(blobHandle);
        }
    }
}