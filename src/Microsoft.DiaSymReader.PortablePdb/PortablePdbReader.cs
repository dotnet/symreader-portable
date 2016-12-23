// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal sealed class PortablePdbReader : IDisposable
    {
        private readonly MetadataReader _metadataReader;

        // null when disposed
        private MetadataReaderProvider _metadataReaderProvider;

        /// <summary>
        /// The method takes ownership of the <paramref name="provider"/> upon entry and disposes it in case of a failure to construct the reader.
        /// </summary>
        internal PortablePdbReader(MetadataReaderProvider provider)
        {
            Debug.Assert(provider != null);

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
        }

        internal bool MatchesModule(Guid guid, uint stamp, int age)
        {
            if (age != 1)
            {
                return false;
            }

            var id = new BlobContentId(MetadataReader.DebugMetadataHeader.Id);
            return id.Guid == guid && id.Stamp == stamp;
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
    }
}
