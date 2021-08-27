// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal sealed class PortablePdbReader : IDisposable
    {
        internal SymReader? _symReader;
        internal readonly int Version;

        /// <summary>
        /// Aggregate number of unique documents in all previous generations.
        /// </summary>
        internal readonly int PreviousDocumentCount;

        private readonly MetadataReader _metadataReader;

        // null when disposed
        private MetadataReaderProvider? _metadataReaderProvider;

        private ImmutableArray<DocumentId> _documentHandleToIdMapOpt;
        private ImmutableArray<MethodId> _methodHandleToIdMapOpt;

        /// <summary>
        /// The method takes ownership of the <paramref name="provider"/> upon entry and disposes it in case of a failure to construct the reader.
        /// </summary>
        internal PortablePdbReader(MetadataReaderProvider provider, int version, int previousDocumentCount)
        {
            Debug.Assert(provider != null);
            Debug.Assert(version >= 1);

            try
            {
                _metadataReader = provider.GetMetadataReader();
            }
            finally
            {
                // dispose the provider on error:
                if (_metadataReader == null)
                {
                    provider.Dispose();
                }
            }

            _metadataReaderProvider = provider;
            Version = version;
            PreviousDocumentCount = previousDocumentCount;
        }

        /// <summary>
        /// Maps <see cref="DocumentHandle"/> relative to this reader to the corresponding global <see cref="DocumentId"/>.
        /// Null if handles correspond to ids 1:1 (baseline).
        /// </summary>
        internal DocumentId GetDocumentId(DocumentHandle handle)
        {
            int rowId = MetadataTokens.GetRowNumber(handle);
            return _documentHandleToIdMapOpt.IsDefault ? new DocumentId(rowId) : _documentHandleToIdMapOpt[rowId - 1];
        }

        /// <summary>
        /// Maps <see cref="MethodDebugInformationHandle"/> relative to this reader to the corresponding global <see cref="MethodId"/>.
        /// </summary>
        internal MethodId GetMethodId(MethodDebugInformationHandle handle)
        {
            int rowId = MetadataTokens.GetRowNumber(handle);
            return _methodHandleToIdMapOpt.IsDefault ? new MethodId(rowId) : _methodHandleToIdMapOpt[rowId - 1];
        }
        
        internal bool TryGetMethodHandle(MethodId id, out MethodDebugInformationHandle handle)
        {
            if (id.IsDefault)
            {
                handle = default;
                return false;
            }

            if (_methodHandleToIdMapOpt.IsDefault)
            {
                if (id.Value > _metadataReader.MethodDebugInformation.Count)
                {
                    handle = default;
                    return false;
                }

                handle = MetadataTokens.MethodDebugInformationHandle(id.Value);
                return true;
            }

            int index = _methodHandleToIdMapOpt.BinarySearch(id);
            if (index >= 0)
            {
                handle = MetadataTokens.MethodDebugInformationHandle(index + 1);
                return true;
            }

            handle = default;
            return false;
        }

        internal bool HasDebugInfo(MethodDebugInformationHandle handle)
            => !MetadataReader.GetMethodDebugInformation(handle).SequencePointsBlob.IsNil;

        internal void InitializeHandleToIdMaps(ImmutableArray<DocumentId> documentIds, ImmutableArray<MethodId> methodIds)
        {
            Debug.Assert(_documentHandleToIdMapOpt.IsDefault);
            Debug.Assert(_methodHandleToIdMapOpt.IsDefault);
            Debug.Assert(!documentIds.IsDefault);
            Debug.Assert(!methodIds.IsDefault);

            _documentHandleToIdMapOpt = documentIds;
            _methodHandleToIdMapOpt = methodIds;
        }

        internal SymReader SymReader
        {
            get
            {
                Debug.Assert(_symReader != null);
                return _symReader;
            }
            set
            {
                Debug.Assert(_symReader == null);
                Debug.Assert(value != null);
                _symReader = value;
            }
        }

        internal bool MatchesModule(Guid guid, uint stamp, int age)
        {
            // A valid portable PDB age is always 1.
            // We also allow the special '-1' value along with zero for the stamp to indicate that the caller isn't able to provide the stamp value
            if (age != 1 && age != -1)
            {
                return false;
            }

            var id = new BlobContentId(MetadataReader.DebugMetadataHeader.Id);

            return id.Guid == guid && (id.Stamp == stamp || age == -1);
        }

        internal MetadataReader MetadataReader
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(SymReader));
                }

                return _metadataReader;
            }
        }

        internal bool IsDisposed => _metadataReaderProvider == null;

        public void Dispose()
        {
            _metadataReaderProvider?.Dispose();
            _metadataReaderProvider = null;
        }

        internal int GetMethodSourceExtentInDocument(ISymUnmanagedDocument document, SymMethod method, out int startLine, out int endLine)
        {
            var symDocument = SymReader.AsSymDocument(document);
            if (symDocument == null)
            {
                startLine = endLine = 0;
                return HResult.E_INVALIDARG;
            }

            var methodExtents = SymReader.GetMethodExtents();
            if (!methodExtents.TryGetMethodSourceExtent(symDocument.GetId(), method.GetId(), out startLine, out endLine))
            {
                startLine = endLine = 0;
                return HResult.E_FAIL;
            }

            return HResult.S_OK;
        }
    }
}
