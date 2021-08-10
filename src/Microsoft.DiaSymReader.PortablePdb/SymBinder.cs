// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

[assembly: Guid("CA89ACD1-A1D5-43DE-890A-5FDF50BC1F93")]

namespace Microsoft.DiaSymReader.PortablePdb
{
    [Guid("E4B18DEF-3B78-46AE-8F50-E67E421BDF70")]
    [ComVisible(true)]
    public sealed class SymBinder : ISymUnmanagedBinder4
    {
        [PreserveSig]
        public unsafe int GetReaderForFile(
            [MarshalAs(UnmanagedType.Interface)]object metadataImport,
            [MarshalAs(UnmanagedType.LPWStr)]string fileName,
            [MarshalAs(UnmanagedType.LPWStr)]string searchPath,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedReader reader)
        {
            return GetReaderForFile2(
                metadataImport,
                fileName,
                searchPath,
                SymUnmanagedSearchPolicy.AllowReferencePathAccess,
                out reader);
        }

        /// <summary>
        /// Given a metadata interface and a file name, returns the 
        /// <see cref="ISymUnmanagedReader"/> interface that will read the debugging symbols associated
        /// with the module.
        /// </summary>
        /// <remarks>
        /// This version of the function can search for the PDB in areas other than
        /// right next to the module.
        /// The search policy can be controlled by combining CorSymSearchPolicyAttributes
        /// e.g AllowReferencePathAccess|AllowSymbolServerAccess will look for the pdb next
        /// to the PE file and on a symbol server, but won't query the registry or use the path
        /// in the PE file.
        /// If a searchPath is provided, those directories will always be searched.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="fileName"/> is null or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="metadataImport"/> does not implement IMetadataImport interface.</exception>
        [PreserveSig]
        public int GetReaderForFile2(
            [MarshalAs(UnmanagedType.Interface)]object metadataImport,
            [MarshalAs(UnmanagedType.LPWStr)]string fileName,
            [MarshalAs(UnmanagedType.LPWStr)]string searchPath,
            SymUnmanagedSearchPolicy searchPolicy,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedReader reader)
        {
            reader = null;
            try
            {
                if (string.IsNullOrEmpty(fileName))
                    throw new ArgumentException(null, nameof(fileName));

                var mdImport = MetadataImport.FromObject(metadataImport) ?? 
                    throw new ArgumentException(null, nameof(metadataImport));

                // See DIA: FLocatePdbDefault, FLocateCvFilePathHelper, FLocatePdbSymsrv, FLocateCvFilePathHelper
                //
                // 1) Try open Combine(<PE directory>, <PDB file name>) (unless RestrictReferencePath)
                // 2) Try open PDB path (unless RestrictOriginalPath)
                // 3) Search Paths - semicolon separated paths
                //    a) searchPath parameter
                //    b) registry (unless RestrictRegistry)
                //       Use search paths from registry Software\Microsoft\VisualStudio\MSPDB, value SymbolSearchPath
                //       with environment variables expanded (ExpandEnvironmentStrings)
                //       i) try USER
                //       ii) try MACHINE
                //    c) environment vars
                //        i) _NT_ALT_SYMBOL_PATH
                //       ii) _NT_SYMBOL_PATH
                //       ii) SystemRoot (unless RestrictSystemRoot)
                //
                //    for each search path:
                //       special paths: SRV*<server>, SYMSRV*SYMSRV.DLL*<server> => symbol server (unless RestrictSymsrv)
                //                      CACHE*<cache> => sym cache (unless RestrictSymsrv)
                //
                //       A) try open <path>\symbols\<PE file extension>\<PDB file name>
                //       B) try open <path>\<PE file extension>\<PDB file name>
                //       C) try open <path>\<PDB file name>
                //
                // Each attempt checks if PDB ID matches.
                //
                // Search policy: all is restricted unless explicitly allowed. 
                // After opened store to cache if CACHE* given (only the first cache?)

                if (!TryReadCodeViewData(fileName, out var codeViewData, out uint stamp))
                {
                    return HResult.E_FAIL; // TODO: specific error code (ecToHresult)?
                }

                Guid guid = codeViewData.Guid;
                int age = codeViewData.Age;
                string pdbFileName = Path.GetFileName(codeViewData.Path);
                var lazyImport = new LazyMetadataImport(mdImport);

                // 1) next to the PE file 
                if ((searchPolicy & SymUnmanagedSearchPolicy.AllowReferencePathAccess) != 0)
                {
                    string peDirectory = Path.GetDirectoryName(fileName);
                    string pdbFilePath = Path.Combine(peDirectory, pdbFileName);

                    if (TryCreateReaderForMatchingPdb(pdbFilePath, guid, stamp, age, lazyImport, out reader))
                    {
                        return HResult.S_OK;
                    }
                }

                // 2) PDB path as specified in Debug Directory
                if ((searchPolicy & SymUnmanagedSearchPolicy.AllowOriginalPathAccess) != 0)
                {
                    if (TryCreateReaderForMatchingPdb(codeViewData.Path, guid, stamp, age, lazyImport, out reader))
                    {
                        return HResult.S_OK;
                    }
                }

                // 3) Search Paths
                string peFileExtension = Path.GetExtension(fileName).TrimStart('.');

                foreach (var searchPaths in GetSearchPathsSequence(searchPath, searchPolicy))
                {
                    if (TryFindMatchingPdb(searchPaths, peFileExtension, pdbFileName, guid, stamp, age, lazyImport, searchPolicy, out reader))
                    {
                        return HResult.S_OK;
                    }
                }

                return HResult.E_PDB_NOT_FOUND;
            }
            finally
            {
                InteropUtilities.TransferOwnershipOrRelease(ref metadataImport, reader);
            }
        }

        private static IEnumerable<string> GetSearchPathsSequence(string searchPath, SymUnmanagedSearchPolicy searchPolicy)
        {
            // 3a) parameter
            yield return searchPath;

            // 4b) registry
            if ((searchPolicy & SymUnmanagedSearchPolicy.AllowRegistryAccess) != 0)
            {
                // TODO: https://github.com/dotnet/symreader-portable/issues/48
            }

            // 5c) environment variables:
            yield return PortableShim.Environment.GetEnvironmentVariable("_NT_ALT_SYMBOL_PATH");
            yield return PortableShim.Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            yield return PortableShim.Environment.GetEnvironmentVariable("SystemRoot");
        }

        private static IEnumerable<string> GetSearchPathSubdirectories(string searchPath, string peFileExtension)
        {
            yield return Path.Combine(searchPath, "symbols", peFileExtension);
            yield return Path.Combine(searchPath, peFileExtension);

            if (peFileExtension.Length > 0)
            {
                yield return Path.Combine(searchPath);
            }
        }

        private static readonly char[] s_searchPathSeparators = { ';' };

        private bool TryFindMatchingPdb(
            string searchPaths,
            string peFileExtension, // with no leading .
            string pdbFileName,
            Guid guid,
            uint stamp,
            int age,
            LazyMetadataImport metadataImport,
            SymUnmanagedSearchPolicy searchPolicy,
            out ISymUnmanagedReader reader)
        {
            if (searchPaths == null)
            {
                reader = null;
                return false;
            }

            foreach (var searchPath in searchPaths.Split(s_searchPathSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                // TODO: check symsrv policy
                if (searchPath.StartsWith("SRV*", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO:
                    continue;
                }

                if (searchPath.StartsWith("SYMSRV*", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO:
                    continue;
                }

                if (searchPath.StartsWith("CACHE*", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO:
                    continue;
                }

                foreach (var subdir in GetSearchPathSubdirectories(searchPath, peFileExtension))
                {
                    if (TryCreateReaderForMatchingPdb(Path.Combine(subdir, pdbFileName), guid, stamp, age, metadataImport, out reader))
                    {
                        return true;
                    }
                }
            }

            reader = null;
            return false;
        }

        private bool TryCreateReaderForMatchingPdb(
            string pdbFilePath,
            Guid guid,
            uint stamp,
            int age,
            LazyMetadataImport metadataImport,
            out ISymUnmanagedReader reader)
        {
            if (PortableShim.File.Exists(pdbFilePath))
            {
                PortablePdbReader pdbReader;
                try
                {
                    pdbReader = new PortablePdbReader(SymReader.CreateProviderFromFile(pdbFilePath), version: 1, previousDocumentCount: 0);
                }
                catch
                {
                    reader = null;
                    return false;
                }

                try
                {
                    if (pdbReader.MatchesModule(guid, stamp, age))
                    {
                        reader = new SymReader(pdbReader, metadataImport);
                        pdbReader = null;
                        return true;
                    }
                }
                finally
                {
                    pdbReader?.Dispose();
                }
            }

            reader = null;
            return false;
        }

        private bool TryReadCodeViewData(string peFilePath, out CodeViewDebugDirectoryData codeViewData, out uint stamp)
        {
            try
            {
                var peStream = PortableShim.FileStream.CreateReadShareDelete(peFilePath);
                using (var peReader = new PEReader(peStream))
                {
                    foreach (var entry in peReader.ReadDebugDirectory())
                    {
                        if (entry.Type == DebugDirectoryEntryType.CodeView)
                        {
                            codeViewData = peReader.ReadCodeViewDebugDirectoryData(entry);
                            stamp = entry.Stamp;
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            codeViewData = default(CodeViewDebugDirectoryData);
            stamp = 0;
            return false;
        }

        /// <summary>
        /// Creates a new <see cref="ISymUnmanagedReader"/> for the specified PDB stream.
        /// </summary>
        /// <param name="metadataImport">IMetadataImport reading metadata in PE file.</param>
        /// <param name="stream">PDB stream. The implementation supports Portable PDB and Embedded Portable PDB formats.</param>
        /// <param name="reader">The new reader instance.</param>
        /// <returns>
        /// E_INVALIDARG
        ///   <paramref name="metadataImport"/> doesn't implement <see cref="IMetadataImport"/>, or
        ///   <paramref name="stream"/> doesn't implement <see cref="IStream"/>.
        /// Another error code describing failure to read the stream.
        /// </returns>
        [PreserveSig]
        public int GetReaderFromStream(
            [MarshalAs(UnmanagedType.Interface)]object metadataImport,
            [MarshalAs(UnmanagedType.Interface)]object stream,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedReader reader)
        {
            reader = null;

            try
            {
                var comStream = stream as IStream ?? throw new ArgumentNullException(null, nameof(stream));
                var mdImport = MetadataImport.FromObject(metadataImport) ?? throw new ArgumentException(null, nameof(metadataImport));

                reader = SymReader.CreateFromStream(comStream, new LazyMetadataImport(mdImport));
                return HResult.S_OK;
            }
            finally
            {
                InteropUtilities.TransferOwnershipOrRelease(ref metadataImport, reader);
            }
        }

        [PreserveSig]
        public int GetReaderFromCallback(
            [In, MarshalAs(UnmanagedType.Interface)] object metadataImport,
            [MarshalAs(UnmanagedType.LPWStr)]string fileName,
            [MarshalAs(UnmanagedType.LPWStr)]string searchPath,
            SymUnmanagedSearchPolicy searchPolicy,
            [In, MarshalAs(UnmanagedType.Interface)] object callback,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedReader reader)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new <see cref="ISymUnmanagedReader"/> for the specified PDB file.
        /// </summary>
        /// <param name="metadataImportProvider">
        /// Provider of a metadata importer for the corresponding PE file.
        /// The importer is only constructed if the operation performed on the SymReader requires access
        /// to the metadata.
        /// </param>
        /// <param name="pdbFilePath">PDB file path. The implementation supports Portable PDB format.</param>
        /// <param name="reader">The new reader instance.</param>
        /// <returns>
        /// E_INVALIDARG
        ///   <paramref name="metadataImportProvider"/> is null, or
        ///   <paramref name="pdbFilePath"/> is null or empty.
        /// Another error code describing failure to open the file.
        /// </returns>
        [PreserveSig]
        public int GetReaderFromPdbFile(
            [MarshalAs(UnmanagedType.Interface)]IMetadataImportProvider metadataImportProvider,
            [MarshalAs(UnmanagedType.LPWStr)]string pdbFilePath,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedReader reader)
        {
            if (metadataImportProvider == null)
                throw new ArgumentException(null, nameof(metadataImportProvider));

            if (string.IsNullOrEmpty(pdbFilePath))
                throw new ArgumentException(null, nameof(pdbFilePath));

            reader = SymReader.CreateFromFile(pdbFilePath, new LazyMetadataImport(metadataImportProvider));
            return HResult.S_OK;
        }

        /// <summary>
        /// Creates a new <see cref="ISymUnmanagedReader"/> for the specified PDB file.
        /// </summary>
        /// <param name="metadataImportProvider">
        /// Provider of a metadata importer for the corresponding PE file.
        /// The importer is only constructed if the operation performed on the SymReader requires access
        /// to the metadata.
        /// </param>
        /// <param name="stream">PDB stream. The implementation supports Portable PDB and Embedded Portable PDB formats.</param>
        /// <param name="reader">The new reader instance.</param>
        /// <exception cref="ArgumentException"><paramref name="metadataImportProvider"/> is null</exception>
        /// <exception cref="ArgumentException"><paramref name="stream"/> is null</exception>
        [PreserveSig]
        public int GetReaderFromPdbStream(
            [MarshalAs(UnmanagedType.Interface)]IMetadataImportProvider metadataImportProvider,
            [MarshalAs(UnmanagedType.Interface)]object stream,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedReader reader)
        {
            var comStream = stream as IStream ?? 
                throw new ArgumentException(null, nameof(stream));

            if (metadataImportProvider == null)
                throw new ArgumentException(null, nameof(metadataImportProvider));

            reader = SymReader.CreateFromStream(comStream, new LazyMetadataImport(metadataImportProvider));
            return HResult.S_OK;
        }
    }
}
