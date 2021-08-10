// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.DiaSymReader.PortablePdb.UnitTests
{
    internal sealed class SymMetadataProvider : ISymReaderMetadataProvider, IDisposable
    {
        private readonly PEReader _peReader;
        private readonly MetadataReader _reader;

        public SymMetadataProvider(Stream peStream)
        {
            _peReader = new PEReader(peStream);
            _reader = _peReader.GetMetadataReader();
        }

        public void Dispose()
        {
            _peReader.Dispose();
        }

        public unsafe bool TryGetStandaloneSignature(int standaloneSignatureToken, out byte* signature, out int length)
        {
            var sigHandle = (StandaloneSignatureHandle)MetadataTokens.Handle(standaloneSignatureToken);
            if (sigHandle.IsNil)
            {
                signature = null;
                length = 0;
                return false;
            }

            var sig = _reader.GetStandaloneSignature(sigHandle);
            var blobReader = _reader.GetBlobReader(sig.Signature);

            signature = blobReader.StartPointer;
            length = blobReader.Length;
            return true;
        }

        public bool TryGetTypeDefinitionInfo(int typeDefinitionToken, out string namespaceName, out string typeName, out TypeAttributes attributes)
        {
            var handle = (TypeDefinitionHandle)MetadataTokens.Handle(typeDefinitionToken);
            if (handle.IsNil)
            {
                namespaceName = null;
                typeName = null;
                attributes = 0;
                return false;
            }

            var typeDefinition = _reader.GetTypeDefinition(handle);
            namespaceName = _reader.GetString(typeDefinition.Namespace);
            typeName = _reader.GetString(typeDefinition.Name);
            attributes = typeDefinition.Attributes;
            return true;
        }

        public bool TryGetTypeReferenceInfo(int typeReferenceToken, out string namespaceName, out string typeName)
        {
            var handle = (TypeReferenceHandle)MetadataTokens.Handle(typeReferenceToken);
            if (handle.IsNil)
            {
                namespaceName = null;
                typeName = null;
                return false;
            }

            var typeReference = _reader.GetTypeReference(handle);
            namespaceName = _reader.GetString(typeReference.Namespace);
            typeName = _reader.GetString(typeReference.Name);
            return true;
        }
    }
}
