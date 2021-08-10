// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal abstract class MetadataImport
    {
        public static MetadataImport FromObject(object obj)
            => obj is IMetadataImport legacyImporter ? (MetadataImport)new Legacy(legacyImporter) :
               obj is DiaSymReader.IMetadataImport dsrImporter ? new Internal(dsrImporter) :
               null;

        public string GetQualifiedTypeName(Handle typeDefOrRef)
        {
            string qualifiedName;
            if (typeDefOrRef.Kind == HandleKind.TypeDefinition)
            {
                GetTypeDefProps(MetadataTokens.GetToken(typeDefOrRef), out qualifiedName);
            }
            else if (typeDefOrRef.Kind == HandleKind.TypeReference)
            {
                GetTypeRefProps(MetadataTokens.GetToken(typeDefOrRef), out qualifiedName);
            }
            else
            {
                qualifiedName = null;
            }

            return qualifiedName;
        }

        public abstract void GetTypeDefProps(int typeDefinition, out string qualifiedName);
        public abstract void GetTypeRefProps(int typeReference, out string qualifiedName);

        public abstract unsafe int GetSigFromToken(int token, out byte* signaturePtr, out int signatureLength);

        private sealed class Legacy : MetadataImport
        {
            private readonly IMetadataImport _import;

            public Legacy(IMetadataImport import)
            {
                _import = import;
            }

            public override void GetTypeDefProps(int typeDefinition, out string qualifiedName)
            {
                _import.GetTypeDefProps(typeDefinition, null, 0, out int nameLength, out _, out _);

                var buffer = new StringBuilder(nameLength + 1);
                _import.GetTypeDefProps(typeDefinition, buffer, buffer.Capacity, out nameLength, out _, out _);
                qualifiedName = buffer.ToString();
            }

            public override void GetTypeRefProps(int typeReference, out string qualifiedName)
            {
                _import.GetTypeRefProps(typeReference, out _, null, 0, out int nameLength);

                var buffer = new StringBuilder(nameLength + 1);
                _import.GetTypeRefProps(typeReference, out _, buffer, buffer.Capacity, out nameLength);
                qualifiedName = buffer.ToString();
            }

            public override unsafe int GetSigFromToken(int token, out byte* signaturePtr, out int signatureLength)
                => _import.GetSigFromToken(token, out signaturePtr, out signatureLength);
        }

        private sealed class Internal : MetadataImport
        {
            private readonly DiaSymReader.IMetadataImport _import;

            public Internal(DiaSymReader.IMetadataImport import)
            {
                _import = import;
            }

            public unsafe override void GetTypeDefProps(int typeDefinition, out string qualifiedName)
            {
                TypeAttributes attr;
                int bt;

                int nameLength;
                Marshal.ThrowExceptionForHR(_import.GetTypeDefProps(typeDefinition, null, 0, &nameLength, &attr, &bt));

                if (nameLength > 0)
                {
                    string buffer = new string('\0', nameLength);
                    fixed (char* bufferPtr = buffer)
                    {
                        Marshal.ThrowExceptionForHR(_import.GetTypeDefProps(typeDefinition, bufferPtr, buffer.Length + 1, null, null, null));
                    }

                    qualifiedName = buffer;
                }
                else
                {
                    qualifiedName = "";
                }
            }

            public unsafe override void GetTypeRefProps(int typeReference, out string qualifiedName)
            {
                int rs;

                int nameLength;
                Marshal.ThrowExceptionForHR(_import.GetTypeRefProps(typeReference, &rs, null, 0, &nameLength));

                if (nameLength > 0)
                {
                    string buffer = new string('\0', nameLength);
                    fixed (char* bufferPtr = buffer)
                    {
                        Marshal.ThrowExceptionForHR(_import.GetTypeRefProps(typeReference, null, bufferPtr, buffer.Length + 1, null));
                    }

                    qualifiedName = buffer;
                }
                else
                {
                    qualifiedName = "";
                }
            }

            public override unsafe int GetSigFromToken(int token, out byte* signaturePtr, out int signatureLength)
            {
                byte* sigPtr;
                int sigLen;
                int hr = _import.GetSigFromToken(token, &sigPtr, &sigLen);
                signaturePtr = sigPtr;
                signatureLength = sigLen;
                return hr;
            }
        }
    }
}
