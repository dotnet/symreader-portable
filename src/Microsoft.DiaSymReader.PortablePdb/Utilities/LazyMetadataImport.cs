// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal sealed class LazyMetadataImport : IDisposable
    {
        private MetadataImport? _lazyMetadataImport;
        private readonly IMetadataImportProvider? _metadataImportProvider;

        public LazyMetadataImport(MetadataImport metadataImport)
        {
            _lazyMetadataImport = metadataImport;
        }

        public LazyMetadataImport(IMetadataImportProvider metadataImportProvider)
        {
            _metadataImportProvider = metadataImportProvider;
        }

        public MetadataImport GetMetadataImport()
        {
            if (_lazyMetadataImport == null)
            {
                Debug.Assert(_metadataImportProvider != null, "MetadataImport disposed");

                var import = MetadataImport.FromObject(_metadataImportProvider.GetMetadataImport()) ?? 
                    throw new InvalidOperationException();

                Interlocked.CompareExchange(ref _lazyMetadataImport, import, null);
            }

            return _lazyMetadataImport;
        }

        public void Dispose()
        {
            var import = Interlocked.Exchange(ref _lazyMetadataImport, null);
            if (import != null && Marshal.IsComObject(import))
            {
                Marshal.ReleaseComObject(import);
            }
        }
    }
}
