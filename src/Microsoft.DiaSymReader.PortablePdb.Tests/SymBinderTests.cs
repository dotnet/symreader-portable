﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.DiaSymReader.PortablePdb.UnitTests
{
    using static SymTestHelpers;

    public class SymBinderTests
    {
        private static ISymUnmanagedBinder4 SymBinder => new SymBinder();

        private sealed class NotImplementedMetadataProvider : IMetadataImportProvider
        {
            public static readonly IMetadataImportProvider Instance = new NotImplementedMetadataProvider();

            public object GetMetadataImport()
            {
                throw new NotImplementedException();
            }
        }

        private sealed class TestMetadataProvider : IMetadataImportProvider
        {
            private readonly Func<IMetadataImport> _importProvider;

            public TestMetadataProvider(Func<IMetadataImport> importProvider)
            {
                _importProvider = importProvider;
            }

            public object GetMetadataImport() => _importProvider();
        }

        // TODO: Enable on Linux. https://github.com/dotnet/symreader-portable/issues/110
        [ConditionalFact(typeof(WindowsOnly))]
        public void GetReaderForFile_NextToPE()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.Documents.PortableDll));

            string tempDir = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), Guid.NewGuid().ToString());
            string peFilePath = Path.Combine(tempDir, "Documents.dll");
            string pdbFilePath = Path.Combine(tempDir, "Documents.pdb");

            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(peFilePath, TestResources.Documents.PortableDll);
            File.WriteAllBytes(pdbFilePath, TestResources.Documents.PortablePdb);

            string searchPath = null;

            ISymUnmanagedReader symReader;
            Assert.Equal(HResult.S_OK, SymBinder.GetReaderForFile(importer, peFilePath, searchPath, out symReader));

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            // check that metadata import hasn't been disposed:
            Assert.NotNull(((SymReader)symReader).GetMetadataImport());

            ((ISymUnmanagedDispose)symReader).Destroy();

            Directory.Delete(tempDir, recursive: true);
        }

        // TODO: Enable on Linux. https://github.com/dotnet/symreader-portable/issues/110
        [ConditionalFact(typeof(WindowsOnly))]
        public void GetReaderForFile_SearchPaths()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.Documents.PortableDll));

            string tempDir = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), Guid.NewGuid().ToString());
            string searchDir = Path.Combine(tempDir, "Dir");
            string peFilePath = Path.Combine(tempDir, "Documents.dll");
            string pdbFilePath = Path.Combine(searchDir, "Documents.pdb");

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(searchDir);
            File.WriteAllBytes(peFilePath, TestResources.Documents.PortableDll);
            File.WriteAllBytes(pdbFilePath, TestResources.Documents.PortablePdb);

            string searchPath = searchDir;

            ISymUnmanagedReader symReader;
            Assert.Equal(HResult.S_OK, SymBinder.GetReaderForFile(importer, peFilePath, searchPath, out symReader));

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            // check that metadata import hasn't been disposed:
            Assert.NotNull(((SymReader)symReader).GetMetadataImport());

            ((ISymUnmanagedDispose)symReader).Destroy();

            Directory.Delete(tempDir, recursive: true);
        }

        // TODO: Enable on Linux. https://github.com/dotnet/symreader-portable/issues/110
        [ConditionalFact(typeof(WindowsOnly))]
        public void GetReaderForFile_SearchPaths_SubDir1()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.Documents.PortableDll));

            string tempDir = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), Guid.NewGuid().ToString());
            string searchDir = Path.Combine(tempDir, "Dir");
            string peFilePath = Path.Combine(tempDir, "Documents.dll");
            string pdbFilePath = Path.Combine(searchDir, "dll", "Documents.pdb");

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.GetDirectoryName(pdbFilePath));
            File.WriteAllBytes(peFilePath, TestResources.Documents.PortableDll);
            File.WriteAllBytes(pdbFilePath, TestResources.Documents.PortablePdb);

            string searchPath = searchDir;

            ISymUnmanagedReader symReader;
            Assert.Equal(HResult.S_OK, SymBinder.GetReaderForFile(importer, peFilePath, searchPath, out symReader));

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            // check that metadata import hasn't been disposed:
            Assert.NotNull(((SymReader)symReader).GetMetadataImport());

            ((ISymUnmanagedDispose)symReader).Destroy();

            Directory.Delete(tempDir, recursive: true);
        }

        // TODO: Enable on Linux. https://github.com/dotnet/symreader-portable/issues/110
        [ConditionalFact(typeof(WindowsOnly))]
        public void GetReaderForFile_SearchPaths_SubDir2()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.Documents.PortableDll));

            string tempDir = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), Guid.NewGuid().ToString());
            string searchDir = Path.Combine(tempDir, "Dir");
            string peFilePath = Path.Combine(tempDir, "Documents.dll");
            string pdbFilePath = Path.Combine(searchDir, "symbols", "dll", "Documents.pdb");

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.GetDirectoryName(pdbFilePath));
            File.WriteAllBytes(peFilePath, TestResources.Documents.PortableDll);
            File.WriteAllBytes(pdbFilePath, TestResources.Documents.PortablePdb);

            string searchPath = searchDir;

            ISymUnmanagedReader symReader;
            Assert.Equal(HResult.S_OK, SymBinder.GetReaderForFile(importer, peFilePath, searchPath, out symReader));

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            // check that metadata import hasn't been disposed:
            Assert.NotNull(((SymReader)symReader).GetMetadataImport());

            ((ISymUnmanagedDispose)symReader).Destroy();

            Directory.Delete(tempDir, recursive: true);
        }

        // TODO: test Environment, Registry (need test hooks)

        // TODO: Enable on Linux. https://github.com/dotnet/symreader-portable/issues/110
        [ConditionalFact(typeof(WindowsOnly))]
        public void GetReaderForFile_SkipNative1()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.Documents.PortableDll));

            string tempDir = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), Guid.NewGuid().ToString());
            string searchDir = Path.Combine(tempDir, "Dir");
            string peFilePath = Path.Combine(tempDir, "Documents.dll");
            string pdbFilePath = Path.Combine(searchDir, "Documents.pdb");
            string nativePdbFilePath = Path.Combine(searchDir, "symbols", "dll", "Documents.pdb");

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.GetDirectoryName(pdbFilePath));
            Directory.CreateDirectory(Path.GetDirectoryName(nativePdbFilePath));
            File.WriteAllBytes(peFilePath, TestResources.Documents.PortableDll);
            File.WriteAllBytes(pdbFilePath, TestResources.Documents.PortablePdb);
            File.WriteAllBytes(nativePdbFilePath, TestResources.Documents.WindowsPdb);

            string searchPath = searchDir;

            ISymUnmanagedReader symReader;
            Assert.Equal(HResult.S_OK, SymBinder.GetReaderForFile(importer, peFilePath, searchPath, out symReader));

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            // check that metadata import hasn't been disposed:
            Assert.NotNull(((SymReader)symReader).GetMetadataImport());

            ((ISymUnmanagedDispose)symReader).Destroy();

            Directory.Delete(tempDir, recursive: true);
        }

        // TODO: Enable on Linux. https://github.com/dotnet/symreader-portable/issues/110
        [ConditionalFact(typeof(WindowsOnly))]
        public void GetReaderForFile_SkipNative2()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.Documents.PortableDll));

            string tempDir = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), Guid.NewGuid().ToString());
            string searchDir1 = Path.Combine(tempDir, "Dir1");
            string searchDir2 = Path.Combine(tempDir, "Dir2");
            string peFilePath = Path.Combine(tempDir, "Documents.dll");
            string nativePdbFilePath = Path.Combine(searchDir1, "Documents.pdb");
            string pdbFilePath = Path.Combine(searchDir2, "Documents.pdb");

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.GetDirectoryName(pdbFilePath));
            Directory.CreateDirectory(Path.GetDirectoryName(nativePdbFilePath));
            File.WriteAllBytes(peFilePath, TestResources.Documents.PortableDll);
            File.WriteAllBytes(pdbFilePath, TestResources.Documents.PortablePdb);
            File.WriteAllBytes(nativePdbFilePath, TestResources.Documents.WindowsPdb);

            string searchPath = searchDir1 + ";" + searchDir2;

            ISymUnmanagedReader symReader;
            Assert.Equal(HResult.S_OK, SymBinder.GetReaderForFile(importer, peFilePath, searchPath, out symReader));

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            // check that metadata import hasn't been disposed:
            Assert.NotNull(((SymReader)symReader).GetMetadataImport());

            ((ISymUnmanagedDispose)symReader).Destroy();

            Directory.Delete(tempDir, recursive: true);
        }

        // TODO: Enable on Linux. https://github.com/dotnet/symreader-portable/issues/110
        [ConditionalFact(typeof(WindowsOnly))]
        public void GetReaderForFile_SkipNonMatching()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.Documents.PortableDll));

            string tempDir = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), Guid.NewGuid().ToString());
            string searchDir = Path.Combine(tempDir, "Dir");
            string peFilePath = Path.Combine(tempDir, "Documents.dll");
            string pdbFilePath = Path.Combine(searchDir, "Documents.pdb");
            string anotherPdbFilePath = Path.Combine(searchDir, "symbols", "dll", "Documents.pdb");

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.GetDirectoryName(pdbFilePath));
            Directory.CreateDirectory(Path.GetDirectoryName(anotherPdbFilePath));
            File.WriteAllBytes(peFilePath, TestResources.Documents.PortableDll);
            File.WriteAllBytes(pdbFilePath, TestResources.Documents.PortablePdb);
            File.WriteAllBytes(anotherPdbFilePath, TestResources.Async.PortablePdb);

            string searchPath = searchDir;

            ISymUnmanagedReader symReader;
            Assert.Equal(HResult.S_OK, SymBinder.GetReaderForFile(importer, peFilePath, searchPath, out symReader));

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            // check that metadata import hasn't been disposed:
            Assert.NotNull(((SymReader)symReader).GetMetadataImport());

            ((ISymUnmanagedDispose)symReader).Destroy();

            Directory.Delete(tempDir, recursive: true);
        }

        [Fact]
        public void GetReaderForFile_MatchingNotFound()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.Documents.PortableDll));

            string tempDir = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), Guid.NewGuid().ToString());
            string searchDir = Path.Combine(tempDir, "Dir");
            string peFilePath = Path.Combine(tempDir, "Documents.dll");
            string anotherPdbFilePath = Path.Combine(searchDir, "symbols", "dll", "Documents.pdb");

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.GetDirectoryName(anotherPdbFilePath));
            File.WriteAllBytes(peFilePath, TestResources.Documents.PortableDll);
            File.WriteAllBytes(anotherPdbFilePath, TestResources.Async.PortablePdb);

            string searchPath = searchDir;

            ISymUnmanagedReader symReader;
            Assert.Equal(HResult.E_PDB_NOT_FOUND, SymBinder.GetReaderForFile(importer, peFilePath, searchPath, out symReader));
            Assert.Null(symReader);

            Directory.Delete(tempDir, recursive: true);
        }

        [Fact]
        public void GetReaderFromPdbFile()
        {
            string filePath = Path.GetTempFileName();
            File.WriteAllBytes(filePath, TestResources.Documents.PortablePdb);

            ISymUnmanagedReader symReader;
            Assert.Equal(HResult.S_OK, SymBinder.GetReaderFromPdbFile(NotImplementedMetadataProvider.Instance, filePath, out symReader));

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            Assert.Equal(HResult.S_FALSE, ((ISymUnmanagedDispose)symReader).Destroy());
            Assert.Equal(HResult.S_OK, ((ISymUnmanagedDispose)symReader).Destroy());

            Assert.Throws<ObjectDisposedException>(() => symReader.GetDocuments(0, out actualCount, null));

            File.Delete(filePath);
        }

        [Fact]
        public void GetReaderFromStream()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.Documents.PortableDll));
            var stream = new MemoryStream(TestResources.Documents.PortablePdb);

            ISymUnmanagedReader symReader = SymBinder.GetReaderFromStream(stream, importer);

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            Assert.Equal(HResult.S_FALSE, ((ISymUnmanagedDispose)symReader).Destroy());
            Assert.Equal(HResult.S_OK, ((ISymUnmanagedDispose)symReader).Destroy());

            Assert.Throws<ObjectDisposedException>(() => symReader.GetDocuments(0, out actualCount, null));
        }

        [Fact]
        public void GetReaderFromStream_PortableEmbedded()
        {
            var importer = new TestIMetadataImport(new MemoryStream(TestResources.MiscEmbedded.Dll));
            var peStream = new MemoryStream(TestResources.MiscEmbedded.Dll);
            var peReader = new PEReader(peStream);

            var embeddedEntry = peReader.ReadDebugDirectory().Single(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
            peStream.Position = embeddedEntry.DataPointer;

            ISymUnmanagedReader symReader = SymBinder.GetReaderFromStream(peStream, importer);

            AssertEx.Equal(new[] { @"C:\MiscEmbedded.cs" }, symReader.GetDocuments().Select(d => d.GetName()));

            Assert.Equal(HResult.S_FALSE, ((ISymUnmanagedDispose)symReader).Destroy());
            Assert.Equal(HResult.S_OK, ((ISymUnmanagedDispose)symReader).Destroy());

            Assert.Throws<ObjectDisposedException>(() => symReader.GetDocuments());
        }

        [Fact]
        public void GetReaderFromPdbStream()
        {
            var stream = new MemoryStream(TestResources.Documents.PortablePdb);

            ISymUnmanagedReader symReader = SymBinder.GetReaderFromPdbStream(stream, NotImplementedMetadataProvider.Instance);

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            Assert.Equal(HResult.S_FALSE, ((ISymUnmanagedDispose)symReader).Destroy());
            Assert.Equal(HResult.S_OK, ((ISymUnmanagedDispose)symReader).Destroy());

            Assert.Throws<ObjectDisposedException>(() => symReader.GetDocuments(0, out actualCount, null));
        }

        [Fact]
        public void GetReaderFromPdbStream_PortableEmbedded()
        {
            var peStream = new MemoryStream(TestResources.MiscEmbedded.Dll);
            var peReader = new PEReader(peStream);

            var embeddedEntry = peReader.ReadDebugDirectory().Single(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
            peStream.Position = embeddedEntry.DataPointer;

            ISymUnmanagedReader symReader = SymBinder.GetReaderFromPdbStream(peStream, NotImplementedMetadataProvider.Instance);

            AssertEx.Equal(new[] { @"C:\MiscEmbedded.cs" }, symReader.GetDocuments().Select(d => d.GetName()));

            Assert.Equal(HResult.S_FALSE, ((ISymUnmanagedDispose)symReader).Destroy());
            Assert.Equal(HResult.S_OK, ((ISymUnmanagedDispose)symReader).Destroy());

            Assert.Throws<ObjectDisposedException>(() => symReader.GetDocuments());
        }

        [Fact]
        public void LazyMetadataImport()
        {
            bool importCreated = false;
            ISymUnmanagedReader symReader = SymBinder.GetReaderFromPdbStream(
                new MemoryStream(TestResources.Scopes.PortablePdb),
                new TestMetadataProvider(() =>
                {
                    importCreated = true;
                    return new TestIMetadataImport(new MemoryStream(TestResources.Scopes.PortableDll));
                }));

            int count;

            //
            //  C<S>.F<T>
            //

            ISymUnmanagedMethod mF;
            Assert.Equal(HResult.S_OK, symReader.GetMethod(0x06000002, out mF));

            // root scope:
            ISymUnmanagedScope rootScope;
            Assert.Equal(HResult.S_OK, mF.GetRootScope(out rootScope));

            // child scope:
            var children = GetAndValidateChildScopes(rootScope, expectedCount: 1);

            var child = children[0];
            Assert.Equal(HResult.S_OK, child.GetLocals(0, out count, null));
            Assert.Equal(0, count);

            ISymUnmanagedScope parent;
            Assert.Equal(HResult.S_OK, child.GetParent(out parent));
            Assert.NotSame(rootScope, parent); // a new instance should be created
            ValidateRootScope(parent);
            ValidateRange(parent, 0, 2);

            var constants = GetAndValidateConstants(child, expectedCount: 29);

            Assert.False(importCreated);
            ValidateConstant(constants[28], "D", 123456.78M, new byte[] { 0x11, 0x29 });
            Assert.True(importCreated);
        }
    }
}
