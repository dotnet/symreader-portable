// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.DiaSymReader.PortablePdb.UnitTests
{
    using static SymTestHelpers;

    public class EmbeddedSourceTests
    {
        [Theory, ClassData(typeof(PdbTestData))]
        private void EmbeddedSource(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EmbeddedSource.DllAndPdb(portable));

            foreach (string file in new[] { @"C:\EmbeddedSource.cs", @"C:\EmbeddedSourceSmall.cs" })
            {
                bool hasEmbeddedSource;
                int length, bytesRead;
                ISymUnmanagedDocument doc;
                Assert.Equal(HResult.S_OK, symReader.GetDocument(file, default(Guid), default(Guid), default(Guid), out doc));
                Assert.Equal(HResult.S_OK, doc.HasEmbeddedSource(out hasEmbeddedSource));
                Assert.Equal(HResult.S_OK, doc.GetSourceLength(out length));
                Assert.True(hasEmbeddedSource);

                var blob = new byte[length];
                Assert.Equal(HResult.S_OK, doc.GetSourceRange(0, 0, int.MaxValue, int.MaxValue, length, out bytesRead, blob));
                Assert.Equal(length, bytesRead);

                Assert.True(length >= sizeof(int));

                byte[] expectedContent;
                int uncompressedSize = BitConverter.ToInt32(blob, 0);

                if (uncompressedSize == 0)
                {
                    Assert.Equal(@"C:\EmbeddedSourceSmall.cs", file);
                    expectedContent = TestResources.EmbeddedSource.CSSmall;
                }
                else
                {
                    Assert.Equal(@"C:\EmbeddedSource.cs", file);
                    expectedContent = TestResources.EmbeddedSource.CS;
                }

                AssertEx.Equal(expectedContent, Decode(blob, uncompressedSize));
            }
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void NoEmbeddedSource(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Documents.DllAndPdb(portable));

            ISymUnmanagedDocument doc;
            Assert.Equal(HResult.S_OK, symReader.GetDocument(@"C:\Documents.cs", default(Guid), default(Guid), default(Guid), out doc));

            bool hasEmbeddedSource;
            Assert.Equal(HResult.S_OK, doc.HasEmbeddedSource(out hasEmbeddedSource));
            Assert.False(hasEmbeddedSource);

            int length;
            Assert.Equal(HResult.S_OK, doc.GetSourceLength(out length));
            Assert.Equal(0, length);

            Assert.Equal(HResult.S_FALSE, doc.GetSourceRange(0, 0, -1, -1, 0, out length, null));
            Assert.Equal(HResult.S_FALSE, doc.GetSourceRange(0, 0, int.MaxValue, int.MaxValue, 0, out length, null));
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void BadArgs(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EmbeddedSource.DllAndPdb(portable));

            ISymUnmanagedDocument doc;
            Assert.Equal(HResult.S_OK, symReader.GetDocument(@"C:\EmbeddedSource.cs", default(Guid), default(Guid), default(Guid), out doc));

            int count;
            Assert.Equal(HResult.E_INVALIDARG, doc.GetSourceRange(-1, 0, int.MaxValue, int.MaxValue, 0, out count, null));
            Assert.Equal(HResult.E_INVALIDARG, doc.GetSourceRange(0, -1, int.MaxValue, int.MaxValue, 0, out count, null));
            Assert.Equal(HResult.E_INVALIDARG, doc.GetSourceRange(0, 0, 1, int.MaxValue, 0, out count, null));
            Assert.Equal(HResult.E_INVALIDARG, doc.GetSourceRange(0, 0, int.MaxValue, 1, 0, out count, null));
            Assert.Equal(HResult.E_INVALIDARG, doc.GetSourceRange(0, 0, int.MaxValue, int.MaxValue, 1, out count, null));

            // negative bufferLength test does not apply to native as it uses uint arguments.
            if (portable)
            {
                Assert.Equal(HResult.E_INVALIDARG, doc.GetSourceRange(0, 0, int.MaxValue, int.MaxValue, -1, out count, new byte[1]));
            }
        }

        private byte[] Decode(byte[] blob, int uncompressedSize)
        {
            Assert.True(uncompressedSize >= 0);

            if (uncompressedSize == 0)
            {
                byte[] content = new byte[blob.Length - sizeof(int)];
                Array.Copy(blob, sizeof(int), content, 0, content.Length);
                return content;
            }
            else
            {
                var compressed = new MemoryStream(blob, sizeof(int), blob.Length - sizeof(int));
                using (var decompressor = new DeflateStream(compressed, CompressionMode.Decompress))
                using (var decompressed = new MemoryStream(uncompressedSize))
                {
                    decompressor.CopyTo(decompressed);
                    Assert.Equal(uncompressedSize, decompressed.Length);
                    return decompressed.ToArray();
                }
            }
        }
    }
}
