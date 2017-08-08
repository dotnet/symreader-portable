// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
                GetTypeDefProps(MetadataTokens.GetToken(typeDefOrRef), out qualifiedName, out TypeAttributes attributes, out int baseType);
            }
            else if (typeDefOrRef.Kind == HandleKind.TypeReference)
            {
                GetTypeRefProps(MetadataTokens.GetToken(typeDefOrRef), out int resolutionScope, out qualifiedName);
            }
            else
            {
                qualifiedName = null;
            }

            return qualifiedName;
        }

        public abstract void GetTypeDefProps(int typeDefinition, out string qualifiedName, out TypeAttributes attributes, out int baseType);
        public abstract void GetTypeRefProps(int typeReference, out int resolutionScope, out string qualifiedName);

        public abstract unsafe int GetSigFromToken(int token, out byte* signaturePtr, out int signatureLength);

        private sealed class Legacy : MetadataImport
        {
            private readonly IMetadataImport _import;

            public Legacy(IMetadataImport import)
            {
                _import = import;
            }

            public override void GetTypeDefProps(int typeDefinition, out string qualifiedName, out TypeAttributes attributes, out int baseType)
            {
                _import.GetTypeDefProps(typeDefinition, null, 0, out int bufferLength, out attributes, out baseType);

                var buffer = new StringBuilder(bufferLength);
                _import.GetTypeDefProps(typeDefinition, buffer, buffer.Capacity, out bufferLength, out attributes, out baseType);
                qualifiedName = buffer.ToString();
            }

            public override void GetTypeRefProps(int typeReference, out int resolutionScope, out string qualifiedName)
            {
                _import.GetTypeRefProps(typeReference, out resolutionScope, null, 0, out int bufferLength);

                var buffer = new StringBuilder(bufferLength);
                _import.GetTypeRefProps(typeReference, out resolutionScope, buffer, buffer.Capacity, out bufferLength);
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

            public unsafe override void GetTypeDefProps(int typeDefinition, out string qualifiedName, out TypeAttributes attributes, out int baseType)
            {
                TypeAttributes attr;
                int bt;

                int bufferLength;
                Marshal.ThrowExceptionForHR(_import.GetTypeDefProps(typeDefinition, null, 0, &bufferLength, &attr, &bt));

                if (bufferLength > 0)
                {
                    string buffer = new string('\0', bufferLength);
                    fixed (char* bufferPtr = buffer)
                    {
                        Marshal.ThrowExceptionForHR(_import.GetTypeDefProps(typeDefinition, bufferPtr, bufferLength, null, null, null));
                    }

                    qualifiedName = buffer;
                }
                else
                {
                    qualifiedName = "";
                }

                attributes = attr;
                baseType = bt;
            }

            public unsafe override void GetTypeRefProps(int typeReference, out int resolutionScope, out string qualifiedName)
            {
                int rs;

                int bufferLength;
                Marshal.ThrowExceptionForHR(_import.GetTypeRefProps(typeReference, &rs, null, 0, &bufferLength));

                if (bufferLength > 0)
                {
                    string buffer = new string('\0', bufferLength);
                    fixed (char* bufferPtr = buffer)
                    {
                        Marshal.ThrowExceptionForHR(_import.GetTypeRefProps(typeReference, null, bufferPtr, bufferLength, null));
                    }

                    qualifiedName = buffer;
                }
                else
                {
                    qualifiedName = "";
                }

                resolutionScope = rs;
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
