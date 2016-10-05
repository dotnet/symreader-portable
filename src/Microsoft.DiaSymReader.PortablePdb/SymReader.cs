// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.DiaSymReader.PortablePdb
{
    // TODO:
    // ISymUnmanagedReaderSymbolSearchInfo?
    // ISymUnmanagedSourceServerModule?

    [ComVisible(false)]
    public sealed class SymReader : ISymUnmanagedReader4, ISymUnmanagedDispose
    {
        private readonly PortablePdbReader _pdbReader;
        private readonly Lazy<DocumentMap> _lazyDocumentMap;
        private readonly Lazy<bool> _lazyVbSemantics;
        private readonly Lazy<MethodMap> _lazyMethodMap;

        private int _version;

        // Takes ownership of <paramref name="pdbReader"/>.
        private SymReader(PortablePdbReader pdbReader)
        {
            Debug.Assert(pdbReader != null);

            _pdbReader = pdbReader;
            _version = 1;

            _lazyDocumentMap = new Lazy<DocumentMap>(() => new DocumentMap(MetadataReader));
            _lazyVbSemantics = new Lazy<bool>(() => IsVisualBasicAssembly());
            _lazyMethodMap = new Lazy<MethodMap>(() => new MethodMap(MetadataReader));
        }

        internal static SymReader CreateFromFile(string path, LazyMetadataImport metadataImport)
        {
            var pdbStream = PortableShim.FileStream.CreateReadShareDelete(path);
            var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);

            return new SymReader(new PortablePdbReader(provider, metadataImport));
        }

        internal static ISymUnmanagedReader CreateFromStream(IStream stream, LazyMetadataImport metadataImport)
        {
            var interopStream = new ReadOnlyInteropStream(stream);
            var header = new byte[2 * sizeof(int)];
            int bytesRead = interopStream.TryReadAll(header, 0, header.Length);

            MetadataReaderProvider provider;

            // detect Embedded Portable PDB signature:
            if (bytesRead == header.Length && header[0] == 'M' && header[1] == 'P' && header[2] == 'D' && header[3] == 'B')
            {
                int size = BitConverter.ToInt32(header, startIndex: sizeof(int));

                // TODO: We could avoid allocating managed memory here if FromPortablePdbImage accepted non-seekable stream in prefetch mode.
                // The implemenation in S.R.M. allocates native memory. 
                byte[] decompressed;
                try
                {
                    decompressed = new byte[size];
                }
                catch
                {
                    throw new BadImageFormatException();
                }

                var deflate = new DeflateStream(interopStream, CompressionMode.Decompress, leaveOpen: true);
                if (size > 0)
                {
                    int actualLength;

                    try
                    {
                        actualLength = deflate.TryReadAll(decompressed, 0, decompressed.Length);
                    }
                    catch (InvalidDataException e)
                    {
                        throw new BadImageFormatException(e.Message, e.InnerException);
                    }

                    if (actualLength != decompressed.Length)
                    {
                        throw new BadImageFormatException();
                    }
                }

                // Check that there is no more compressed data left, 
                // in case the decompressed size specified in the header is smaller 
                // than the actual decompressed size of the data.
                if (deflate.ReadByte() != -1)
                {
                    throw new BadImageFormatException();
                }

                provider = MetadataReaderProvider.FromPortablePdbImage(ImmutableByteArrayInterop.DangerousCreateFromUnderlyingArray(ref decompressed));
            }
            else
            {
                interopStream.Position = 0;
                provider = MetadataReaderProvider.FromPortablePdbStream(interopStream);
            }

            return new SymReader(new PortablePdbReader(provider, metadataImport));
        }

        internal MetadataReader MetadataReader => _pdbReader.MetadataReader;
        internal PortablePdbReader PdbReader => _pdbReader;
        internal Lazy<bool> VbSemantics => _lazyVbSemantics;

        public int Destroy()
        {
            if (_pdbReader.IsDisposed)
            {
                return HResult.S_OK;
            }

            _pdbReader.Dispose();
            return HResult.S_FALSE;
        }

        private bool IsVisualBasicAssembly()
        {
            var reader = MetadataReader;

            foreach (var cdiHandle in reader.GetCustomDebugInformation(Handle.ModuleDefinition))
            {
                if (reader.GetGuid(reader.GetCustomDebugInformation(cdiHandle).Kind) == MetadataUtilities.VbDefaultNamespaceId)
                {
                    return true;
                }
            }

            return false;
        }

        internal MethodMap GetMethodMap()
        {
            if (_pdbReader.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(SymReader));
            }

            return _lazyMethodMap.Value;
        }

        internal SymDocument AsSymDocument(ISymUnmanagedDocument document)
        {
            var symDocument = document as SymDocument;
            return (symDocument?.SymReader == this) ? symDocument : null;
        }

        public int GetDocument(
            [MarshalAs(UnmanagedType.LPWStr)]string url,
            Guid language,
            Guid languageVendor,
            Guid documentType,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedDocument document)
        {
            DocumentHandle documentHandle;

            // SymReader: language, vendor and type parameters are ignored.

            if (_pdbReader.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(SymReader));
            }

            if (_lazyDocumentMap.Value.TryGetDocument(url, out documentHandle))
            {
                document = new SymDocument(this, documentHandle);
                return HResult.S_OK;
            }

            document = null;
            return HResult.S_FALSE;
        }

        public int GetDocuments(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedDocument[] documents)
        {
            count = MetadataReader.Documents.Count;

            if (bufferLength == 0)
            {
                return HResult.S_OK;
            }

            int i = 0;
            foreach (var documentHandle in MetadataReader.Documents)
            {
                if (i >= bufferLength)
                {
                    break;
                }

                documents[i++] = new SymDocument(this, documentHandle);
            }

            return HResult.S_OK;
        }

        public int GetDocumentVersion(ISymUnmanagedDocument document, out int version, out bool isCurrent)
        {
            // SymReader always returns the same values
            version = 1;
            isCurrent = true;
            return HResult.E_NOTIMPL;
        }

        public int GetGlobalVariables(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedVariable[] variables)
        {
            // SymReader doesn't support.
            count = 0;
            return HResult.E_NOTIMPL;
        }

        public int GetMethod(int methodToken, [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedMethod method)
        {
            return GetMethodByVersion(methodToken, _version, out method);
        }

        public int GetMethodByVersion(
            int methodToken,
            int version,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedMethod method)
        {
            if (version != _version)
            {
                method = null;
                return HResult.E_INVALIDARG;
            }

            var handle = MetadataTokens.Handle(methodToken);
            if (handle.Kind != HandleKind.MethodDefinition)
            {
                method = null;
                return HResult.E_INVALIDARG;
            }

            var methodDebugHandle = ((MethodDefinitionHandle)handle).ToDebugInformationHandle();

            var methodBody = MetadataReader.GetMethodDebugInformation(methodDebugHandle);
            if (methodBody.SequencePointsBlob.IsNil)
            {
                // no debug info for the method
                method = null;
                return HResult.E_FAIL;
            }

            method = new SymMethod(this, methodDebugHandle);
            return HResult.S_OK;
        }

        public int GetMethodByVersionPreRemap(int methodToken, int version, [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedMethod method)
        {
            // TODO:
            throw new NotSupportedException();
        }

        public int GetMethodFromDocumentPosition(
            ISymUnmanagedDocument document,
            int line,
            int column,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedMethod method)
        {
            var symDocument = AsSymDocument(document);
            if (symDocument == null)
            {
                method = null;
                return HResult.E_INVALIDARG;
            }

            var methodBodyHandles = GetMethodMap().GetMethodsContainingLine(symDocument.Handle, line);
            if (methodBodyHandles == null)
            {
                method = null;
                return HResult.E_FAIL;
            }

            var comparer = HandleComparer.Default;
            var candidate = default(MethodDebugInformationHandle);
            foreach (var methodDebugHandle in methodBodyHandles)
            {
                if (candidate.IsNil || comparer.Compare(methodDebugHandle, candidate) < 0)
                {
                    candidate = methodDebugHandle;
                }
            }

            if (candidate.IsNil)
            {
                method = null;
                return HResult.E_FAIL;
            }

            method = new SymMethod(this, candidate);
            return HResult.S_OK;
        }

        public int GetMethodsFromDocumentPosition(
            ISymUnmanagedDocument document,
            int line,
            int column,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3), Out]ISymUnmanagedMethod[] methods)
        {
            var symDocument = AsSymDocument(document);
            if (symDocument == null)
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            var methodBodyHandles = GetMethodMap().GetMethodsContainingLine(symDocument.Handle, line);
            if (methodBodyHandles == null)
            {
                count = 0;
                return HResult.E_FAIL;
            }

            if (bufferLength > 0)
            {
                int i = 0;
                foreach (var methodDebugHandle in methodBodyHandles)
                {
                    if (i == bufferLength)
                    {
                        break;
                    }

                    methods[i++] = new SymMethod(this, methodDebugHandle);
                }

                count = i;

                if (i > 1)
                {
                    Array.Sort(methods, 0, i, SymMethod.ByHandleComparer.Default);
                }
            }
            else
            {
                count = methodBodyHandles.Count();
            }

            return HResult.S_OK;
        }

        public int GetMethodsInDocument(
            ISymUnmanagedDocument document,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1), Out]ISymUnmanagedMethod[] methods)
        {
            var symDocument = AsSymDocument(document);
            if (symDocument == null)
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            var extentsByMethod = GetMethodMap().GetMethodExtents(symDocument.Handle);
            if (bufferLength > 0)
            {
                int actualCount = Math.Min(extentsByMethod.Length, bufferLength);
                for (int i = 0; i < actualCount; i++)
                {
                    methods[i] = new SymMethod(this, extentsByMethod[i].Method);
                }

                count = actualCount;
            }
            else
            {
                count = extentsByMethod.Length;
            }

            count = 0;
            return HResult.S_OK;
        }

        internal int GetMethodSourceExtentInDocument(ISymUnmanagedDocument document, SymMethod method, out int startLine, out int endLine)
        {
            var symDocument = AsSymDocument(document);
            if (symDocument == null)
            {
                startLine = endLine = 0;
                return HResult.E_INVALIDARG;
            }

            var map = GetMethodMap();
            if (!map.TryGetMethodSourceExtent(symDocument.Handle, method.DebugHandle, out startLine, out endLine))
            {
                startLine = endLine = 0;
                return HResult.E_FAIL;
            }

            return HResult.S_OK;
        }

        public int GetMethodVersion(ISymUnmanagedMethod method, out int version)
        {
            version = _version;
            return HResult.S_OK;
        }

        public int GetNamespaces(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedNamespace[] namespaces)
        {
            // SymReader doesn't support
            count = 0;
            return HResult.E_NOTIMPL;
        }

        public int GetSymAttribute(int methodToken,
            [MarshalAs(UnmanagedType.LPWStr)]string name,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2), Out]byte[] customDebugInformation)
        {
            return GetSymAttributeByVersion(methodToken, 1, name, bufferLength, out count, customDebugInformation);
        }

        public int GetSymAttributeByVersion(
            int methodToken,
            int version,
            [MarshalAs(UnmanagedType.LPWStr)]string name,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3), Out]byte[] customDebugInformation)
        {
            if ((bufferLength != 0) != (customDebugInformation != null))
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            if (version != _version)
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            count = 0;
            return HResult.S_FALSE;
        }

        public int GetSymAttributePreRemap(
            int methodToken,
            [MarshalAs(UnmanagedType.LPWStr)]string name,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2), Out]byte[] customDebugInformation)
        {
            // TODO:
            throw new NotSupportedException();
        }

        public int GetSymAttributeByVersionPreRemap(
            int methodToken,
            int version,
            [MarshalAs(UnmanagedType.LPWStr)]string name,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3), Out]byte[] customDebugInformation)
        {
            // TODO:
            throw new NotSupportedException();
        }

        public int GetSymbolStoreFileName(int bufferLength, out int count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]char[] name)
        {
            // TODO:
            throw new NotImplementedException();
        }

        public int GetUserEntryPoint(out int methodToken)
        {
            var handle = MetadataReader.DebugMetadataHeader.EntryPoint;
            if (!handle.IsNil)
            {
                methodToken = MetadataTokens.GetToken(handle);
                return HResult.S_OK;
            }

            methodToken = 0;
            return HResult.E_FAIL;
        }

        public int GetVariables(
            int methodToken,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1), Out]ISymUnmanagedVariable[] variables)
        {
            // SymReader doesn't support non-local variables.
            count = 0;
            return HResult.E_NOTIMPL;
        }

        public int Initialize(
            [MarshalAs(UnmanagedType.Interface)]object metadataImporter,
            [MarshalAs(UnmanagedType.LPWStr)]string fileName,
            [MarshalAs(UnmanagedType.LPWStr)]string searchPath,
            IStream stream)
        {
            return HResult.S_OK;
        }

        public int ReplaceSymbolStore([MarshalAs(UnmanagedType.LPWStr)]string fileName, IStream stream)
        {
            // TODO:
            throw new NotImplementedException();
        }

        public int UpdateSymbolStore([MarshalAs(UnmanagedType.LPWStr)]string fileName, IStream stream)
        {
            // TODO:
            throw new NotImplementedException();
        }

        /// <summary>
        /// Checkes whether the id stored in the PDB matches the PDB ID stored in the PE/COFF Debug Directory.
        /// </summary>
        [PreserveSig]
        public int MatchesModule(Guid guid, uint stamp, int age, [MarshalAs(UnmanagedType.Bool)]out bool result)
        {
            result = _pdbReader.MatchesModule(guid, stamp, age);
            return HResult.S_OK;
        }

        /// <summary>
        /// Returns a pointer to Portable Debug Metadata. Only available for Portable PDBs.
        /// </summary>
        /// <param name="metadata">
        /// A pointer to memory where Portable Debug Metadata start. The memory is owned by the SymReader and 
        /// valid until <see cref="ISymUnmanagedDispose.Destroy"/> is invoked. 
        /// 
        /// Null if the PDB is not portable.
        /// </param>
        /// <param name="size">Size of the metadata block.</param>
        [PreserveSig]
        public unsafe int GetPortableDebugMetadata(out byte* metadata, out int size)
        {
            var reader = _pdbReader.MetadataReader;
            metadata = reader.MetadataPointer;
            size = reader.MetadataLength;
            return HResult.S_OK;
        }

        /// <summary>
        /// Returns a pointer to Source Server data stored in the PDB.
        /// </summary>
        /// <param name="data">
        /// A pointer to memory where Source Server data start. The memory is owned by the SymReader and 
        /// valid until <see cref="ISymUnmanagedDispose.Destroy"/> is invoked. 
        /// 
        /// Null if the PDB doesn't contain Source Server data.
        /// </param>
        /// <param name="size">Size of the data in bytes.</param>
        /// <returns>
        /// S_OK if the PDB contains Source Server data, S_FALSE if it doesn't.
        /// </returns>
        [PreserveSig]
        public unsafe int GetSourceServerData(out byte* data, out int size)
        {
            var reader = _pdbReader.MetadataReader;

            BlobHandle handle = MetadataUtilities.GetCustomDebugInformation(reader, EntityHandle.ModuleDefinition, MetadataUtilities.SourceLinkId);
            if (!handle.IsNil)
            {
                var blobReader = reader.GetBlobReader(handle);
                data = blobReader.StartPointer;
                size = blobReader.Length;
                return HResult.S_OK;
            }

            data = null;
            size = 0;
            return HResult.S_FALSE;
        }
    }
}