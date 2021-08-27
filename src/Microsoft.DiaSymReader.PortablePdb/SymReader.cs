// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Roslyn.Utilities;

namespace Microsoft.DiaSymReader.PortablePdb
{
    // TODO:
    // ISymUnmanagedReaderSymbolSearchInfo?

    [ComVisible(false)]
    public sealed class SymReader : ISymUnmanagedReader5, ISymUnmanagedDispose, ISymUnmanagedEncUpdate
    {
        private readonly Lazy<bool> _lazyVbSemantics;
        private readonly LazyMetadataImport _metadataImport;

        private List<PortablePdbReader> _pdbReaders;

        private readonly Lazy<DocumentMap> _lazyDocumentMap;
        private readonly Lazy<MethodMap> _methodMap;
        private readonly Lazy<MethodExtents> _lazyMethodExtents;
        private Dictionary<MethodId, MethodLineDeltas>? _lazyMethodLineDeltas;

        /// Takes ownership of <paramref name="pdbReader"/> and <paramref name="metadataImport"/>.
        internal SymReader(PortablePdbReader pdbReader, LazyMetadataImport metadataImport)
        {
            Debug.Assert(pdbReader != null);
            Debug.Assert(metadataImport != null);

            pdbReader.SymReader = this;
            _pdbReaders = new List<PortablePdbReader>() { pdbReader };

            _metadataImport = metadataImport;
            _lazyDocumentMap = new Lazy<DocumentMap>(() => new DocumentMap(_pdbReaders[0].MetadataReader));
            _methodMap = new Lazy<MethodMap>(() => new MethodMap(_pdbReaders[0]));
            _lazyMethodExtents = new Lazy<MethodExtents>(() => new MethodExtents(_pdbReaders[0]));
            _lazyVbSemantics = new Lazy<bool>(() => IsVisualBasicAssembly());
        }

        internal DocumentMap GetDocumentMap() => _lazyDocumentMap.Value;
        internal MethodMap GetMethodMap() => _methodMap.Value;
        internal MethodExtents GetMethodExtents() => _lazyMethodExtents.Value;

        internal static SymReader CreateFromFile(string path, LazyMetadataImport metadataImport)
        {
            return new SymReader(new PortablePdbReader(CreateProviderFromFile(path), version: 1, previousDocumentCount: 0), metadataImport);
        }
		
        internal static MetadataReaderProvider CreateProviderFromFile(string path)
        {
            var pdbStream = PortableShim.FileStream.CreateReadShareDelete(path);
            return MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        }
		
		internal static ISymUnmanagedReader CreateFromStream(IStream stream, LazyMetadataImport metadataImport)
        {
		    return new SymReader(new PortablePdbReader(CreateProviderFromStream(stream), version: 1, previousDocumentCount: 0), metadataImport);
		}
		
		internal static MetadataReaderProvider CreateProviderFromStream(IStream stream)
        {
            var interopStream = new ReadOnlyInteropStream(stream);
            var header = new byte[2 * sizeof(int)];
            int bytesRead = interopStream.TryReadAll(header, 0, header.Length);

            MetadataReaderProvider provider;

            // detect Embedded Portable PDB signature:
            if (bytesRead == header.Length && header[0] == 'M' && header[1] == 'P' && header[2] == 'D' && header[3] == 'B')
            {
                int size = BitConverter.ToInt32(header, startIndex: sizeof(int));

                // TODO: https://github.com/dotnet/symreader-portable/issues/47
                // We could avoid allocating managed memory here if FromPortablePdbImage accepted non-seekable stream in prefetch mode.
                // The implementation in S.R.M. allocates native memory. 
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

                provider = MetadataReaderProvider.FromPortablePdbImage(ImmutableByteArrayInterop.DangerousCreateFromUnderlyingArray(ref decompressed!));
            }
            else
            {
                interopStream.Position = 0;
                provider = MetadataReaderProvider.FromPortablePdbStream(interopStream);
            }

            return provider;
        }

        internal Lazy<bool> VbSemantics => _lazyVbSemantics;

        internal bool IsDisposed => _pdbReaders == null;

        internal int Version => GetReaders().Count;

        internal bool IsValidVersion(int version) => version >= 1 && version <= Version;

        internal PortablePdbReader GetReader(int version)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(SymReader));
            }

            return _pdbReaders[version - 1];
        }

        internal IReadOnlyList<PortablePdbReader> GetReaders()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(SymReader));
            }

            return _pdbReaders;
        }

        internal MetadataImport GetMetadataImport()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(SymReader));
            }

            return _metadataImport.GetMetadataImport();
        }

        public int Destroy()
        {
            var readers = Interlocked.Exchange(ref _pdbReaders, null!);
            if (readers == null)
            {
                return HResult.S_OK;
            }

            foreach (var pdbReader in readers)
            {
                pdbReader.Dispose();
            }

            _metadataImport.Dispose();

            return HResult.S_FALSE;
        }

        private bool IsVisualBasicAssembly()
        {
            var reader = _pdbReaders.First().MetadataReader;

            foreach (var cdiHandle in reader.GetCustomDebugInformation(Handle.ModuleDefinition))
            {
                if (reader.GetGuid(reader.GetCustomDebugInformation(cdiHandle).Kind) == MetadataUtilities.VbDefaultNamespaceId)
                {
                    return true;
                }
            }

            return false;
        }

        internal SymDocument? AsSymDocument(ISymUnmanagedDocument document)
        {
            var symDocument = document as SymDocument;
            return (symDocument?.SymReader == this) ? symDocument : null;
        }

        internal SymMethod? AsSymMethod(ISymUnmanagedMethod method)
        {
            var symMethod = method as SymMethod;
            return (symMethod?.SymReader == this) ? symMethod : null;
        }

        private void UpdateLineDeltas(MethodId methodId, MethodLineDeltas deltas)
        {
            if (_lazyMethodLineDeltas == null)
            {
                _lazyMethodLineDeltas = new Dictionary<MethodId, MethodLineDeltas>();
            }

            if (_lazyMethodLineDeltas.TryGetValue(methodId, out var existing))
            {
                _lazyMethodLineDeltas[methodId] = existing.Merge(deltas);
            }
            else
            {
                _lazyMethodLineDeltas[methodId] = deltas;
            }
        }

        private void RemoveLineDeltas(MethodId methodId)
        {
            _lazyMethodLineDeltas?.Remove(methodId);
        }

        internal bool TryGetLineDeltas(MethodId methodId, out MethodLineDeltas deltas)
        {
            if (_lazyMethodLineDeltas == null)
            {
                deltas = default;
                return false;
            }

            return _lazyMethodLineDeltas.TryGetValue(methodId, out deltas);
        }

        /// <summary>
        /// Finds document of a specified name.
        /// </summary>
        /// <param name="url">Document name.</param>
        /// <param name="language">Ignored.</param>
        /// <param name="languageVendor">Ignored.</param>
        /// <param name="documentType">Ignored.</param>
        /// <param name="document">Document or null.</param>
        /// <returns>
        /// S_OK if found, S_FALSE if not found.
        /// </returns>
        /// <remarks>
        /// EnC: Returns document in the first generation that defines one with matching name,
        /// even if the document is not referred to by any sequence point anymore 
        /// (e.g. the statement was removed by a subsequent edit).
        /// </remarks>
        public int GetDocument(
            [MarshalAs(UnmanagedType.LPWStr)]string url,
            Guid language,
            Guid languageVendor,
            Guid documentType,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedDocument? document)
        {
            var map = GetDocumentMap();
            if (map.TryGetDocument(url, out var documentId))
            {
                var info = map.GetInfo(documentId);
                document = new SymDocument(GetReader(info.Version), info.Handle);
                return HResult.S_OK;
            }

            document = null;
            return HResult.S_FALSE;
        }

        /// <summary>
        /// Get a list of all documents, including those added via EnC.
        /// </summary>
        public int GetDocuments(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedDocument[] documents)
        {
            DocumentMap? documentMap = null;
            PortablePdbReader? pdbReader = null; 
            if (Version > 1)
            {
                documentMap = GetDocumentMap();
                count = documentMap.DocumentCount;
            }
            else
            {
                pdbReader = GetReader(version: 1);
                count = pdbReader.MetadataReader.Documents.Count;
            }

            if (bufferLength == 0)
            {
                return HResult.S_OK;
            }

            if (documents == null)
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            int i = 0;
            if (documentMap != null)
            {
                foreach (var info in documentMap.Infos)
                {
                    if (i >= bufferLength)
                    {
                        break;
                    }

                    documents[i++] = new SymDocument(GetReader(info.Version), info.Handle);
                }
            }
            else
            {
                Debug.Assert(pdbReader != null);

                foreach (var documentHandle in pdbReader.MetadataReader.Documents)
                {
                    if (i >= bufferLength)
                    {
                        break;
                    }

                    documents[i++] = new SymDocument(pdbReader, documentHandle);
                }
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

        /// <summary>
        /// Get the latest version of a method with specified token.
        /// </summary>
        public int GetMethod(int methodToken, [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedMethod? method)
        {
            int hr = GetMethodImpl(methodToken, out var symMethod);
            method = symMethod;
            return hr;
        }

        private int GetMethodImpl(int methodToken, out SymMethod? method)
        {
            if (TryGetDebuggableMethod(methodToken, out var pdbReader, out var handle))
            {
                method = new SymMethod(pdbReader, handle);
                return HResult.S_OK;
            }

            method = null;
            return HResult.E_FAIL;
        }

        private bool TryGetDebuggableMethod(int methodToken, [NotNullWhen(true)] out PortablePdbReader? pdbReader, out MethodDebugInformationHandle handle)
        {
            if (!MetadataUtilities.IsMethodToken(methodToken))
            {
                pdbReader = null;
                handle = default;
                return false;
            }

            var methodId = MethodId.FromToken(methodToken);

            if (Version == 1)
            {
                pdbReader = GetReader(version: 1);
                if (pdbReader.TryGetMethodHandle(methodId, out handle))
                {
                    return pdbReader.HasDebugInfo(handle);
                }
            }
            else
            {
                var methodMap = GetMethodMap();
                if (methodMap.IsValidMethodRowId(methodId.Value))
                {
                    var info = methodMap.GetInfo(methodId);
                    pdbReader = GetReader(info.Version);
                    handle = info.Handle;
                    return pdbReader.HasDebugInfo(handle);
                }
            }

            pdbReader = null;
            handle = default;
            return false;
        }

        /// <summary>
        /// Get a particular version of a method with specified token.
        /// </summary>
        public int GetMethodByVersion(
            int methodToken,
            int version,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedMethod? method)
        {
            if (!IsValidVersion(version))
            {
                method = null;
                return HResult.E_INVALIDARG;
            }

            if (!MetadataUtilities.IsMethodToken(methodToken))
            {
                method = null;
                return HResult.E_INVALIDARG;
            }

            var pdbReader = GetReader(version);
            if (!pdbReader.TryGetMethodHandle(MethodId.FromToken(methodToken), out var methodDebugHandle))
            {
                method = null;
                return HResult.E_FAIL;
            }

            var debugInfo = pdbReader.MetadataReader.GetMethodDebugInformation(methodDebugHandle);
            if (debugInfo.SequencePointsBlob.IsNil)
            {
                // no debug info for the method
                method = null;
                return HResult.E_FAIL;
            }

            method = new SymMethod(pdbReader, methodDebugHandle);
            return HResult.S_OK;
        }

        public int GetMethodByVersionPreRemap(int methodToken, int version, [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedMethod? method)
        {
            return GetMethodByVersion(methodToken, version, out method);
        }

        public int GetMethodFromDocumentPosition(
            ISymUnmanagedDocument document,
            int line,
            int column,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedMethod? method)
        {
            var symDocument = AsSymDocument(document);
            if (symDocument == null)
            {
                method = null;
                return HResult.E_INVALIDARG;
            }

            var methodExtents = GetMethodExtents();
            var methods = methodExtents.GetMethodsContainingLine(symDocument.GetId(), line);
            if (methods == null)
            {
                method = null;
                return HResult.E_FAIL;
            }

            var candidate = default((MethodId Id, int Version));
            foreach (var (methodId, version) in methods)
            {
                if (candidate.Id.IsDefault || methodId < candidate.Id)
                {
                    candidate = (methodId, version);
                }
            }

            if (candidate.Id.IsDefault)
            {
                method = null;
                return HResult.E_FAIL;
            }

            var pdbReader = GetReader(candidate.Version);
            if (!pdbReader.TryGetMethodHandle(candidate.Id, out var handle))
            {
                method = null;
                return HResult.E_FAIL;
            }

            method = new SymMethod(pdbReader, handle);
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

            var methodExtents = GetMethodExtents();
            var methodVersionedIds = methodExtents.GetMethodsContainingLine(symDocument.GetId(), line);
            if (methodVersionedIds == null)
            {
                count = 0;
                return HResult.E_FAIL;
            }

            if (bufferLength > 0)
            {
                int i = 0;

                // diasymreader doesn't order by version, only by token

                foreach (var (methodId, version) in methodVersionedIds.OrderBy(entry => entry.Id))
                {
                    if (i == bufferLength)
                    {
                        break;
                    }

                    var pdbReader = GetReader(version);
                    if (!pdbReader.TryGetMethodHandle(methodId, out var handle))
                    {
                        // Method extents only refer to existing method versions. 
                        throw ExceptionUtilities.Unreachable;
                    }

                    methods[i++] = new SymMethod(pdbReader, handle);
                }

                count = i;
            }
            else
            {
                count = methodVersionedIds.Count();
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

            if (bufferLength > 0 && (methods == null || methods.Length < bufferLength))
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            var methodExtents = GetMethodExtents();
            var extentsByMethod = methodExtents.GetMethodExtents(symDocument.GetId());
            if (bufferLength > 0)
            {
                var methodMap = GetMethodMap();

                int actualCount = Math.Min(extentsByMethod.Length, bufferLength);
                for (int i = 0; i < actualCount; i++)
                {
                    var info = methodMap.GetInfo(extentsByMethod[i].Method);
                    methods[i] = new SymMethod(GetReader(info.Version), info.Handle);
                }

                count = actualCount;
            }
            else
            {
                count = extentsByMethod.Length;
            }

            return HResult.S_OK;
        }

        public int GetMethodVersion(ISymUnmanagedMethod method, out int version)
        {
            var symMethod = AsSymMethod(method);
            if (symMethod == null)
            {
                version = 0;
                return HResult.E_INVALIDARG;
            }

            version = symMethod.PdbReader.Version;
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

        public int GetSymAttribute(
            int methodToken,
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
            count = 0;

            if ((bufferLength != 0) != (customDebugInformation != null) || !IsValidVersion(version))
            {
                return HResult.E_INVALIDARG;
            }

            return HResult.S_FALSE;
        }

        public int GetSymAttributePreRemap(
            int methodToken,
            [MarshalAs(UnmanagedType.LPWStr)]string name,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2), Out]byte[] customDebugInformation)
        {
            return GetSymAttribute(methodToken, name, bufferLength, out count, customDebugInformation);
        }

        public int GetSymAttributeByVersionPreRemap(
            int methodToken,
            int version,
            [MarshalAs(UnmanagedType.LPWStr)]string name,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3), Out]byte[] customDebugInformation)
        {
            return GetSymAttributeByVersion(methodToken, version, name, bufferLength, out count, customDebugInformation);
        }

        public int GetSymbolStoreFileName(int bufferLength, out int count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]char[] name)
        {
            // TODO: https://github.com/dotnet/symreader-portable/issues/50
            throw new NotImplementedException();
        }

        public int GetUserEntryPoint(out int methodToken)
        {
            // diasymreader enumerates from version 1:
            foreach (var pdbReader in GetReaders())
            {
                var handle = pdbReader.MetadataReader.DebugMetadataHeader.EntryPoint;
                if (!handle.IsNil)
                {
                    methodToken = MetadataTokens.GetToken(handle);
                    return HResult.S_OK;
                }
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
            // TODO: https://github.com/dotnet/symreader-portable/issues/49
            throw new NotImplementedException();
        }

        public int UpdateSymbolStore([MarshalAs(UnmanagedType.LPWStr)]string fileName, IStream stream)
        {
            if (stream != null)
            {
                return UpdateSymbolStoreImpl(stream, fileName: null, EmptyArray<SymUnmanagedLineDelta>.Instance, 0);
            }

            if (string.IsNullOrEmpty(fileName))
            {
                return HResult.E_INVALIDARG;
            }

            return UpdateSymbolStoreImpl(null, fileName, EmptyArray<SymUnmanagedLineDelta>.Instance, 0);            
        }

        /// <summary>
        /// Checks whether the id stored in the PDB matches the PDB ID stored in the PE/COFF Debug Directory.
        /// </summary>
        /// <param name="guid">The GUID portion of the PDB ID from the PE/COFFF debug directory</param>
        /// <param name="stamp">The timestamp portion of the PDB ID from the PE/COFF debug directory. If the caller 
        /// doesn't have access to this value, the caller may also pass zero for this value and -1 for the age.</param>
        /// <param name="age">The age value from the PE/COFF debug directory</param>
        /// <param name="result">True if the PDB matches the provided PDB ID</param>
        [PreserveSig]
        public int MatchesModule(Guid guid, uint stamp, int age, [MarshalAs(UnmanagedType.Bool)]out bool result)
        {
            result = GetReader(version: 1).MatchesModule(guid, stamp, age);
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
        /// <returns>
        /// S_OK if the PDB is portable, S_FALSE if it isn't.
        /// </returns>
        /// <remarks>
        /// If the store was updated via <see cref="UpdateSymbolStore(string, IStream)"/> 
        /// returns the metadata of the latest update.
        /// </remarks>
        [PreserveSig]
        public unsafe int GetPortableDebugMetadata(out byte* metadata, out int size) =>
            GetPortableDebugMetadataByVersion(Version, out metadata, out size);

        /// <summary>
        /// Returns a pointer to Portable Debug Metadata of the specified version (EnC generation). Only available for Portable PDBs.
        /// </summary>
        /// <param name="version">
        /// EnC 1-based version number. Version 1 corresponds to the baseline.
        /// </param>
        /// <param name="metadata">
        /// A pointer to memory where Portable Debug Metadata start. The memory is owned by the SymReader and 
        /// valid until <see cref="ISymUnmanagedDispose.Destroy"/> is invoked. 
        /// 
        /// Null if the PDB is not portable.
        /// </param>
        /// <param name="size">Size of the metadata block.</param>
        [PreserveSig]
        public unsafe int GetPortableDebugMetadataByVersion(int version, out byte* metadata, out int size)
        {
            if (!IsValidVersion(version))
            {
                metadata = null;
                size = 0;
                return HResult.E_INVALIDARG;
            }

            var reader = GetReader(version).MetadataReader;
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
            var reader = GetReader(version: 1).MetadataReader;

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

        #region ISymUnmanagedEncUpdate

        /// <summary>
        /// Applies EnC edit. 
        /// </summary>
        /// <remarks>
        /// Not thread safe. Must not be run in parallel with any other operation on the SymReader or objects it owns.
        /// </remarks>
        [PreserveSig]
        public int UpdateSymbolStore2(
            IStream stream,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]SymUnmanagedLineDelta[] lineDeltas,
            int lineDeltaCount)
        {
            if (stream == null || lineDeltas == null || lineDeltaCount < 0)
            {
                return HResult.E_INVALIDARG;
            }

            return UpdateSymbolStoreImpl(stream, null, lineDeltas, lineDeltaCount);
        }

        private int UpdateSymbolStoreImpl(
            IStream? stream,
            string? fileName,
            SymUnmanagedLineDelta[] lineDeltas,
            int lineDeltaCount)
        {
            Debug.Assert(stream != null ^ fileName != null);
            Debug.Assert(lineDeltas != null);
            Debug.Assert(lineDeltaCount >= 0);

            lineDeltaCount = Math.Min(lineDeltas.Length, lineDeltaCount);
            var methodMap = GetMethodMap();
            var documentMap = GetDocumentMap();
            var methodExtents = GetMethodExtents();

            var lineDeltasByDocument = GroupLineDeltasByDocument(lineDeltas, lineDeltaCount);

            int newVersion = Version + 1;

            var provider = (stream != null) ? CreateProviderFromStream(stream) : CreateProviderFromFile(fileName!);

            var pdbReader = new PortablePdbReader(provider, newVersion, documentMap.DocumentCount);

            documentMap.Update(this, pdbReader.MetadataReader, newVersion, out var documentHandleToIdMap);
            methodMap.Update(this, pdbReader.MetadataReader, newVersion, out var methodHandleToIdMap);
            pdbReader.InitializeHandleToIdMaps(documentHandleToIdMap, methodHandleToIdMap);
            methodExtents.Update(pdbReader, lineDeltasByDocument);

            // remove line deltas of methods updated in this generation:
            for (int i = 0; i < methodHandleToIdMap.Length; i++)
            {
                RemoveLineDeltas(methodHandleToIdMap[i]);
            }

            // apply line deltas of methods moved around in this generation:
            for (int i = 0; i < lineDeltaCount; i++)
            {
                UpdateLineDeltas(MethodId.FromToken(lineDeltas[i].MethodToken), new MethodLineDeltas(lineDeltas[i].Delta, ImmutableArray<int>.Empty));
            }

            _pdbReaders.Add(pdbReader);
            pdbReader.SymReader = this;

            return HResult.S_OK;
        }

        private Dictionary<DocumentId, List<(MethodId, int)>> GroupLineDeltasByDocument(SymUnmanagedLineDelta[] lineDeltas, int lineDeltaCount)
        {
            var deltasByDocument = new Dictionary<DocumentId, List<(MethodId, int)>>();
            for (int i = 0; i < lineDeltaCount; i++)
            {
                int methodToken = lineDeltas[i].MethodToken;
                if (!TryGetDebuggableMethod(methodToken, out var pdbReader, out var handle))
                {
                    continue;
                }

                var methodId = MethodId.FromToken(methodToken);
                var (single, multiple) = MethodExtents.GetMethodBodyDocuments(pdbReader.MetadataReader, handle);
                if (!single.IsNil)
                {
                    AddExtentForDocument(single);
                }
                else
                {
                    // method has debug info:
                    Debug.Assert(multiple != null);

                    foreach (var documentHandle in multiple)
                    {
                        AddExtentForDocument(documentHandle);
                    }
                }

                void AddExtentForDocument(DocumentHandle documentHandle)
                {
                    var documentId = pdbReader.GetDocumentId(documentHandle);

                    if (!deltasByDocument.TryGetValue(documentId, out var extents))
                    {
                        deltasByDocument.Add(documentId, extents = new List<(MethodId, int)>());
                    }

                    extents.Add((methodId, lineDeltas[i].Delta));
                }
            }

            return deltasByDocument;
        }

        /// <summary>
        /// Gets the number of local variables of the latest version of the specified method.
        /// </summary>
        [PreserveSig]
        public int GetLocalVariableCount(int methodToken, out int count)
        {
            if (TryGetDebuggableMethod(methodToken, out var pdbReader, out var handle))
            {
                count = SymMethod.GetLocalVariableCount(pdbReader.MetadataReader, handle);
                return HResult.S_OK;
            }

            count = 0;
            return HResult.E_FAIL;
        }

        /// <summary>
        /// Gets local variables of the latest version of the specified method.
        /// </summary>
        [PreserveSig]
        public int GetLocalVariables(
            int methodToken,
            int bufferLength,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ISymUnmanagedVariable[] variables,
            out int count)
        {
            if (variables == null)
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            int hr = GetMethodImpl(methodToken, out var method);
            if (hr != HResult.S_OK)
            {
                count = 0;
                return hr;
            }

            Debug.Assert(method != null);

            var actualCount = SymMethod.GetLocalVariableCount(method.MetadataReader, method.DebugHandle);
            if (actualCount > unchecked((uint)bufferLength))
            {
                count = 0;
                return HResult.E_WIN32_NOT_ENOUGH_MEMORY;
            }

            method.AddLocalVariables(variables);
            count = actualCount;
            return HResult.S_OK;
        }

        [PreserveSig]
        public int InitializeForEnc()
        {
            GetDocumentMap();
            GetMethodMap();
            GetMethodExtents();

            return HResult.S_OK;
        }

        /// <summary>
        /// Allows updating the line info for a method that has not been recompiled,
        /// but whose lines have moved independently.  A delta for each statement is allowed.
        /// </summary>
        [PreserveSig]
        public int UpdateMethodLines(
            int methodToken,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] int[] deltas,
            int count)
        {
            if (count == 0)
            {
                return HResult.S_OK;
            }

            if (deltas == null)
            {
                return HResult.E_INVALIDARG;
            }

            if (count < 0 || !TryGetDebuggableMethod(methodToken, out var pdbReader, out var handle))
            {
                return HResult.E_FAIL;
            }

            var trimmedDeltas = ImmutableArray.Create(deltas, 0, Math.Min(deltas.Length, count));

            var methodExtents = GetMethodExtents();
            try
            {
                methodExtents.Update(pdbReader, handle, trimmedDeltas, expectedSequencePointCount: count);
            }
            catch (InvalidInputDataException)
            {
                // delta count don't match sequence points 
                return HResult.E_FAIL;
            }

            UpdateLineDeltas(MethodId.FromToken(methodToken), new MethodLineDeltas(0, trimmedDeltas));
            return HResult.S_OK;
        }

        #endregion
    }
}
