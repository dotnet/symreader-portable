// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.DiaSymReader.PortablePdb.UnitTests
{
    using static SymTestHelpers;

    public class EncTests
    {
        [Theory, ClassData(typeof(PdbTestData))]
        public void UpdateSymbolStore2_Errors(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            var symUpdate = (ISymUnmanagedEncUpdate)symReader;
            var deltaPdb = SymUnmanagedStreamFactory.CreateStream(new MemoryStream(TestResources.EncMethodExtents.Diffs(1, portable).Pdb));

            var deltas1 = new SymUnmanagedLineDelta[] { new SymUnmanagedLineDelta(0x06000001, 1) };

            Assert.Equal(HResult.E_INVALIDARG, symUpdate.UpdateSymbolStore2(deltaPdb, null, 0));

            if (portable)
            {
                // diasymreader AVs
                Assert.Equal(HResult.E_INVALIDARG, symUpdate.UpdateSymbolStore2(deltaPdb, deltas1, -1));
            }

            Assert.Equal(HResult.E_INVALIDARG, symUpdate.UpdateSymbolStore2(null, deltas1, 1));
            Assert.Equal(HResult.S_OK, symUpdate.UpdateSymbolStore2(deltaPdb, deltas1, 2));

            // invalid token:
            var deltas = new SymUnmanagedLineDelta[] { new SymUnmanagedLineDelta(0x07000001, 1) };
            Assert.Equal(HResult.S_OK, symUpdate.UpdateSymbolStore2(deltaPdb, deltas, 1));

            // invalid token:
            deltas = new SymUnmanagedLineDelta[] { new SymUnmanagedLineDelta(0x06000fff, 1) };
            Assert.Equal(HResult.S_OK, symUpdate.UpdateSymbolStore2(deltaPdb, deltas, 1));

            // method w/o debug info:
            deltas = new SymUnmanagedLineDelta[] { new SymUnmanagedLineDelta(0x06000005, 1) };
            Assert.Equal(HResult.S_OK, symUpdate.UpdateSymbolStore2(deltaPdb, deltas, 1));
        }

        // TODO: test multiple files with the same name (enc update of document map)

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetDocuments(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            var allDocs0 = symReader.GetDocuments();
            AssertEx.Equal(new[] { @"C:\Enc1.cs", @"C:\F\A.cs", @"C:\F\B.cs" }, allDocs0.Select(d => d.GetName()));

            var docMain0 = symReader.GetDocument("Enc1.cs");
            var docA0 = symReader.GetDocument("A.cs");
            var docB0 = symReader.GetDocument("B.cs");

            Assert.Equal(@"C:\Enc1.cs", docMain0.GetName());
            Assert.Equal(@"C:\F\A.cs", docA0.GetName());
            Assert.Equal(@"C:\F\B.cs", docB0.GetName());

            AssertEx.Equal(new byte[] { 0xB5, 0xEC, 0xD7, 0x1B, 0x32, 0x5E, 0x86, 0x76, 0xD5, 0x98, 0xE5, 0xA6, 0x20, 0xA5, 0xA6, 0x68, 0x8A, 0x34, 0x98, 0x81 }, docMain0.GetChecksum());

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable));

            var allDocs1 = symReader.GetDocuments();
            AssertEx.Equal(new[] { @"C:\Enc1.cs", @"C:\F\A.cs", @"C:\F\B.cs", @"C:\F\C.cs" }, allDocs1.Select(d => d.GetName()));

            var docMain1 = symReader.GetDocument("Enc1.cs");
            var docA1 = symReader.GetDocument("A.cs");
            var docB1 = symReader.GetDocument("B.cs");
            var docC1 = symReader.GetDocument("C.cs");

            Assert.Equal(@"C:\Enc1.cs", docMain1.GetName());
            Assert.Equal(@"C:\F\A.cs", docA1.GetName());
            Assert.Equal(@"C:\F\B.cs", docB1.GetName());
            Assert.Equal(@"C:\F\C.cs", docC1.GetName());

            var alg = default(Guid);
            int count;
            Assert.Equal(HResult.S_FALSE, docMain1.GetChecksum(0, out count, null));
            Assert.Equal(0, count);
            Assert.Equal(HResult.S_FALSE, docMain1.GetChecksumAlgorithmId(ref alg));
            Assert.Equal(default(Guid), alg);

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable));

            var allDocs2 = symReader.GetDocuments();
            AssertEx.Equal(new[] { @"C:\Enc1.cs", @"C:\F\A.cs", @"C:\F\B.cs", @"C:\F\C.cs", @"C:\F\E.cs" }, allDocs2.Select(d => d.GetName()));

            var docMain2 = symReader.GetDocument("Enc1.cs");
            var docA2 = symReader.GetDocument("A.cs");
            var docB2 = symReader.GetDocument("B.cs");
            var docC2 = symReader.GetDocument("C.cs");
            var docE2 = symReader.GetDocument("E.cs");

            Assert.Equal(HResult.S_FALSE, docMain2.GetChecksum(0, out count, null));
            Assert.Equal(0, count);
            Assert.Equal(HResult.S_FALSE, docMain2.GetChecksumAlgorithmId(ref alg));
            Assert.Equal(default(Guid), alg);

            Assert.Equal(@"C:\Enc1.cs", docMain2.GetName());
            Assert.Equal(@"C:\F\A.cs", docA2.GetName());
            Assert.Equal(@"C:\F\B.cs", docB2.GetName());
            Assert.Equal(@"C:\F\C.cs", docC2.GetName());
            Assert.Equal(@"C:\F\E.cs", docE2.GetName());
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetMethod(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            Assert.Equal(HResult.E_FAIL, symReader.GetMethod(0x01000001, out var _));
            Assert.Equal(HResult.E_FAIL, symReader.GetMethod(0x06000fff, out var __));

            // method w/o debug info:
            Assert.Equal(HResult.E_FAIL, symReader.GetMethod(0x06000005, out var ___));
            Assert.Equal(HResult.E_FAIL, symReader.GetMethodByVersion(0x06000005, 1, out var ____));

            var methodA0 = symReader.GetMethod(0x06000001);
            Assert.Equal(1, symReader.GetMethodVersion(methodA0));
            Assert.Equal(6, methodA0.GetSequencePoints().First().StartLine);

            // gen 1:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable));

            var methodA1 = symReader.GetMethod(0x06000001);
            Assert.Equal(1, symReader.GetMethodVersion(methodA1));
            Assert.Equal(6, methodA1.GetSequencePoints().First().StartLine);

            // gen 2:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable));

            var methodA2 = symReader.GetMethod(0x06000001);
            Assert.Equal(3, symReader.GetMethodVersion(methodA2));
            Assert.Equal(6, methodA2.GetSequencePoints().First().StartLine);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetMethodByVersion(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersion(0x06000001, 1, out var methodA0));
            Assert.Equal(0x06000001, methodA0.GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(methodA0));
            Assert.Equal(6, methodA0.GetSequencePoints().First().StartLine);

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersion(0x06000002, 1, out var methodF0));
            Assert.Equal(0x06000002, methodF0.GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(methodF0));
            Assert.Equal(9, methodF0.GetSequencePoints().First().StartLine);

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodByVersion(0x06000100, 1, out var m0));
            Assert.Equal(HResult.E_INVALIDARG, symReader.GetMethodByVersion(0x06000001, 2, out var m1));
            Assert.Equal(HResult.E_INVALIDARG, symReader.GetMethodByVersion(0x06000002, 2, out var m2));

            // gen 1:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable));

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodByVersion(0x06000001, 2, out var methodA1));
            Assert.Null(methodA1);

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersion(0x06000002, 2, out var methodF1));
            Assert.Equal(0x06000002, methodF1.GetToken());
            Assert.Equal(2, symReader.GetMethodVersion(methodF1));
            Assert.Equal(9, methodF1.GetSequencePoints().First().StartLine);

            // gen 2:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable));

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersion(0x06000001, 3, out var methodA2));
            Assert.Equal(0x06000001, methodA2.GetToken());
            Assert.Equal(3, symReader.GetMethodVersion(methodA2));
            Assert.Equal(6, methodA2.GetSequencePoints().First().StartLine);

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersion(0x06000002, 3, out var methodF2));
            Assert.Equal(0x06000002, methodF2.GetToken());
            Assert.Equal(3, symReader.GetMethodVersion(methodF2));
            Assert.Equal(9, methodF2.GetSequencePoints().First().StartLine);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetMethodByVersionPreRemap(bool portable)
        {
            // there seems to be no difference between GetMethodByVersion and GetMethodByVersionPreRemap

            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersionPreRemap(0x06000001, 1, out var methodA0));
            Assert.Equal(0x06000001, methodA0.GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(methodA0));
            Assert.Equal(6, methodA0.GetSequencePoints().First().StartLine);

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersionPreRemap(0x06000002, 1, out var methodF0));
            Assert.Equal(0x06000002, methodF0.GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(methodF0));
            Assert.Equal(9, methodF0.GetSequencePoints().First().StartLine);

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodByVersionPreRemap(0x06000100, 1, out var m0));
            Assert.Equal(HResult.E_INVALIDARG, symReader.GetMethodByVersionPreRemap(0x06000001, 2, out var m1));
            Assert.Equal(HResult.E_INVALIDARG, symReader.GetMethodByVersionPreRemap(0x06000002, 2, out var m2));

            // gen 1:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable));

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodByVersionPreRemap(0x06000001, 2, out var methodA1));
            Assert.Null(methodA1);

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersionPreRemap(0x06000002, 2, out var methodF1));
            Assert.Equal(0x06000002, methodF1.GetToken());
            Assert.Equal(2, symReader.GetMethodVersion(methodF1));
            Assert.Equal(9, methodF1.GetSequencePoints().First().StartLine);

            // gen 2:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable));

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersionPreRemap(0x06000001, 3, out var methodA2));
            Assert.Equal(0x06000001, methodA2.GetToken());
            Assert.Equal(3, symReader.GetMethodVersion(methodA2));
            Assert.Equal(6, methodA2.GetSequencePoints().First().StartLine);

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersionPreRemap(0x06000002, 3, out var methodF2));
            Assert.Equal(0x06000002, methodF2.GetToken());
            Assert.Equal(3, symReader.GetMethodVersion(methodF2));
            Assert.Equal(9, methodF2.GetSequencePoints().First().StartLine);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetMethodFromDocumentPosition1(bool portable)
        {
            ISymUnmanagedMethod method;
            int version;
         
            // Gen 0:
            // Method F (0x06000002): nil (Document) #4c
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var docA0 = symReader.GetDocument("A.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docA0, 10, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
            Assert.Equal(HResult.S_OK, symReader.GetMethodVersion(method, out version));
            Assert.Equal(1, version);

            var docB0 = symReader.GetDocument("B.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docB0, 20, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
            Assert.Equal(HResult.S_OK, symReader.GetMethodVersion(method, out version));
            Assert.Equal(1, version);

            // Gen 1:
            // Method F (0x06000001): nil (Document) #45
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (10, 9) - (10, 29) 0x30000003 (Document) C.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable));

            var docA1 = symReader.GetDocument("A.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docA1, 10, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
            Assert.Equal(HResult.S_OK, symReader.GetMethodVersion(method, out version));
            Assert.Equal(2, version);

            var docB1 = symReader.GetDocument("B.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docB1, 20, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
            Assert.Equal(HResult.S_OK, symReader.GetMethodVersion(method, out version));
            Assert.Equal(1, version);

            var docC1 = symReader.GetDocument("C.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docC1, 10, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
            Assert.Equal(HResult.S_OK, symReader.GetMethodVersion(method, out version));
            Assert.Equal(2, version);

            // Gen 2:
            // Method F (0x06000001): nil (Document) #45
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) B.cs
            //   IL_0007: (10, 9) - (10, 29) 0x30000003 (Document) E.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable));

            var docA2 = symReader.GetDocument("A.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docA2, 10, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
            Assert.Equal(HResult.S_OK, symReader.GetMethodVersion(method, out version));
            Assert.Equal(2, version);

            var docB2 = symReader.GetDocument("B.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docB2, 10, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
            Assert.Equal(HResult.S_OK, symReader.GetMethodVersion(method, out version));
            Assert.Equal(3, version);

            var docC2 = symReader.GetDocument("C.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docC2, 10, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
            Assert.Equal(HResult.S_OK, symReader.GetMethodVersion(method, out version));
            Assert.Equal(2, version);

            var docE2 = symReader.GetDocument("E.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docE2, 10, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
            Assert.Equal(HResult.S_OK, symReader.GetMethodVersion(method, out version));
            Assert.Equal(3, version);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetMethodFromDocumentPosition_WithLineDeltas(bool portable)
        {
            ISymUnmanagedMethod method;

            // Gen 0: 0x06000003
            // {
            //     IL_0000: (17, 16) - (17, 17) 0x30000001(Document)
            // }

            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            var doc = symReader.GetDocument("Enc1.cs");
            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(doc, 17, 1, out method));
            Assert.Equal(0x06000003, method.GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(method));

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodFromDocumentPosition(doc, 36, 1, out method));
            Assert.Null(method);

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodFromDocumentPosition(doc, 37, 1, out method));
            Assert.Null(method);

            // Gen 1:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable), new[]
            {
                new SymUnmanagedLineDelta(0x06000003, 20)
            });

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodFromDocumentPosition(doc, 17, 1, out method));
            Assert.Null(method);

            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(doc, 37, 1, out method));
            Assert.Equal(0x06000003, method.GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(method));

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodFromDocumentPosition(doc, 36, 1, out method));
            Assert.Null(method);

            // Gen 2:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable), new[]
            {
                new SymUnmanagedLineDelta(0x06000003, -1)
            });

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodFromDocumentPosition(doc, 17, 1, out method));
            Assert.Null(method);

            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(doc, 36, 1, out method));
            Assert.Equal(0x06000003, method.GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(method));

            Assert.Equal(HResult.E_FAIL, symReader.GetMethodFromDocumentPosition(doc, 37, 1, out method));
            Assert.Null(method);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetMethodsFromDocumentPosition1(bool portable)
        {
            // Gen 0:
            // G (0x06000004) 0x30000001 (Document) #70
            // {
            //   IL_0000: (20, 5) - (20, 6) 
            //   IL_0001: (21, 9) - (21, 43)
            //   IL_0021: (23, 9) - (27, 17)
            //   IL_0041: (28, 5) - (28, 6) 
            // }
            // Lambda H1 <G>b__3_0 (0x06000008)
            // {
            //   IL_0000: (21, 35) - (21, 36)
            // }
            // Lambda H2 <G>b__3_1 (0x06000009)
            // {
            //   IL_0000: (24, 9) - (24, 10)
            //   IL_0001: (25, 13) - (25, 47)
            //   IL_0021: (27, 9) - (27, 10)
            // }
            // Lambda H3 <G>b__3_2 (0x0600000A)
            // {
            //   IL_0000: (25, 39) - (25, 40)
            // }

            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var docEnc = symReader.GetDocument("Enc1.cs");
            var methods = GetMethodsFromDocumentPosition(symReader, docEnc, 25, 1);

            Assert.Equal(0x06000004, methods[0].GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(methods[0]));

            Assert.Equal(0x06000009, methods[1].GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(methods[1]));

            Assert.Equal(0x0600000A, methods[2].GetToken());
            Assert.Equal(1, symReader.GetMethodVersion(methods[2]));

            Assert.Equal(3, methods.Length);

            // Gen 1:
            // G (0x06000004)
            // {
            //   IL_0000: (18, 5) - (18, 6)
            //   IL_0001: (19, 9) - (19, 43)
            //   IL_0021: (21, 9) - (25, 17)
            //   IL_0041: (26, 5) - (26, 6) 
            // }
            // Lambda H1 <G>b__3_0 (0x06000008)
            // {
            //   IL_0000: (19, 35) - (19, 36)
            // }
            // Lambda H2 <G>b__3_1 (0x06000009)
            // {
            //   IL_0000: (22, 9) - (22, 10) 
            //   IL_0001: (23, 13) - (23, 47)
            //   IL_0021: (24, 13) - (24, 47)
            //   IL_0041: (25, 9) - (25, 10) 
            // }
            // Lambda H3 <G>b__3_2 (0x0600000A)
            // {
            //   IL_0000: (23, 39) - (23, 40)
            // }
            // Lambda H4: <G>b__3_3#1 (0x0600000B)
            // {
            //   IL_0000: (24, 39) - (24, 40)
            // }

            if (!portable)
            {
                // diasymreader seems to be buggy
                return;
            }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable));

            // in H3
            methods = GetMethodsFromDocumentPosition(symReader, docEnc, 23, 1);

            Assert.Equal(0x06000004, methods[0].GetToken());
            Assert.Equal(2, symReader.GetMethodVersion(methods[0]));

            Assert.Equal(0x06000009, methods[1].GetToken());
            Assert.Equal(2, symReader.GetMethodVersion(methods[1]));

            Assert.Equal(0x0600000A, methods[2].GetToken());
            Assert.Equal(2, symReader.GetMethodVersion(methods[2]));

            Assert.Equal(3, methods.Length);

            // in H4
            methods = GetMethodsFromDocumentPosition(symReader, docEnc, 24, 1);

            Assert.Equal(0x06000004, methods[0].GetToken());
            Assert.Equal(2, symReader.GetMethodVersion(methods[0]));

            Assert.Equal(0x06000009, methods[1].GetToken());
            Assert.Equal(2, symReader.GetMethodVersion(methods[1]));

            Assert.Equal(0x0600000B, methods[2].GetToken());
            Assert.Equal(2, symReader.GetMethodVersion(methods[2]));

            Assert.Equal(3, methods.Length);

            // Gen 2:
            // G (0x06000004)
            // {
            //   IL_0000: (18, 5) - (18, 6)
            //   IL_0001: (21, 9) - (25, 17)
            //   IL_0022: (26, 5) - (26, 6)
            // }
            // Lambda H2 <G>b__3_1 (0x06000009)
            // {
            //   IL_0000: (22, 9) - (22, 10)
            //   IL_0001: (24, 13) - (24, 47)
            //   IL_0021: (25, 9) - (25, 10)
            // }
            // Lambda H4: <G>b__3_3#1 (0x0600000B)
            // {
            //   IL_0000: (24, 39) - (24, 40)
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable), new[] 
            {
                new SymUnmanagedLineDelta(0x06000004, 5),
                new SymUnmanagedLineDelta(0x06000009, 5),
                new SymUnmanagedLineDelta(0x0600000B, 5),
            });

            // in H4: note that the line deltas were overwritten by a v3 update to G and contained lambdas:
            methods = GetMethodsFromDocumentPosition(symReader, docEnc, 24, 1);

            Assert.Equal(0x06000004, methods[0].GetToken());
            Assert.Equal(3, symReader.GetMethodVersion(methods[0]));

            Assert.Equal(0x06000009, methods[1].GetToken());
            Assert.Equal(3, symReader.GetMethodVersion(methods[1]));

            Assert.Equal(0x0600000B, methods[2].GetToken());
            Assert.Equal(3, symReader.GetMethodVersion(methods[2]));

            Assert.Equal(3, methods.Length);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetMethodsFromDocumentPosition_WithLineDeltas(bool portable)
        {
            // Gen 0: 0x06000003
            // {
            //     IL_0000: (17, 16) - (17, 17) 0x30000001(Document)
            // }

            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            var doc = symReader.GetDocument("Enc1.cs");
            Assert.Equal(0x06000003, GetMethodsFromDocumentPosition(symReader, doc, 17, 1).Single().GetToken());
            Assert.Equal(0, GetMethodsFromDocumentPosition(symReader, doc, 37, 1).Length);
            Assert.Equal(0, GetMethodsFromDocumentPosition(symReader, doc, 36, 1).Length);

            // Gen 1:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable), new[]
            {
                new SymUnmanagedLineDelta(0x06000003, 20)
            });

            Assert.Equal(0, GetMethodsFromDocumentPosition(symReader, doc, 17, 1).Length);
            Assert.Equal(0x06000003, GetMethodsFromDocumentPosition(symReader, doc, 37, 1).Single().GetToken());
            Assert.Equal(0, GetMethodsFromDocumentPosition(symReader, doc, 36, 1).Length);

            // Gen 2:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable), new[]
            {
                new SymUnmanagedLineDelta(0x06000003, -1)
            });

            Assert.Equal(0, GetMethodsFromDocumentPosition(symReader, doc, 17, 1).Length);
            Assert.Equal(0, GetMethodsFromDocumentPosition(symReader, doc, 37, 1).Length);
            Assert.Equal(0x06000003, GetMethodsFromDocumentPosition(symReader, doc, 36, 1).Single().GetToken());
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void UpdateMethodLines_Errors(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            // 1: 
            // {
            //   IL_0000: (6, 16) - (6, 17) 0x30000001 (Document)
            // }
            // 2:
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document)
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document)
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document)
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document)
            // }

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x01000001, null, 0));
            Assert.Equal(HResult.E_FAIL, symEncUpdate.UpdateMethodLines(0, new[] { 1 }, 1));
            Assert.Equal(HResult.E_FAIL, symEncUpdate.UpdateMethodLines(0x01000001, new[] { 1 }, 1));
            Assert.Equal(HResult.E_FAIL, symEncUpdate.UpdateMethodLines(0x0600ffff, new[] { 1 }, 1));
            Assert.Equal(HResult.E_FAIL, symEncUpdate.UpdateMethodLines(0x06000001, new[] { 1 }, 5));
            Assert.Equal(HResult.E_FAIL, symEncUpdate.UpdateMethodLines(0x06000001, new[] { 1 }, -1));

            Assert.Equal(HResult.E_FAIL, symEncUpdate.UpdateMethodLines(0x06000002, new[] { 1,2,3,4 }, 3));
            Assert.Equal(HResult.E_FAIL, symEncUpdate.UpdateMethodLines(0x06000002, new[] { 1,2,3,4 }, 5));

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000002, new[] { 1 }, 4));

            // metod w/o debug info:
            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000005, new[] { 1 }, 0));
            Assert.Equal(HResult.E_FAIL, symEncUpdate.UpdateMethodLines(0x06000005, new[] { 1 }, 1));
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void UpdateMethodLines_GetMethodFromDocumentPosition1(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            // Method F (0x06000002): nil (Document) #4c
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000002, new[] { -1, 3, 2, 1 }, 4));

            var docEnc = symReader.GetDocument("Enc1.cs");
            var docA = symReader.GetDocument("A.cs");
            var docB = symReader.GetDocument("B.cs");

            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docEnc, 8, 1, out var method));
            Assert.Equal(0x06000002, method.GetToken());

            if (portable)
            {
                Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docA, 10 + 3, 1, out method));
                Assert.Equal(0x06000002, method.GetToken());

                Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docB, 20 + 2, 1, out method));
                Assert.Equal(0x06000002, method.GetToken());
            }
            else
            {
                // seems like a bug in diasymreader (the sequence points are actually updated) 
                Assert.Equal(HResult.E_FAIL, symReader.GetMethodFromDocumentPosition(docA, 10 + 3, 1, out method));
                Assert.Equal(HResult.E_FAIL, symReader.GetMethodFromDocumentPosition(docB, 20 + 2, 1, out method));
            }

            Assert.Equal(HResult.S_OK, symReader.GetMethodFromDocumentPosition(docEnc, 15 + 1, 1, out method));
            Assert.Equal(0x06000002, method.GetToken());
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void UpdateMethodLines_GetSourceExtentInDocument1(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            // Method F (0x06000002): nil (Document) #4c
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000002, new[] { -1, 3, 2, 1 }, 4));

            ValidateMethodExtent(symReader, 0x06000002, "Enc1.cs", 8, 16);

            if (portable)
            {
                ValidateMethodExtent(symReader, 0x06000002, "A.cs", 13, 13);
                ValidateMethodExtent(symReader, 0x06000002, "B.cs", 22, 22);
            }
            else
            {
                // diasymreader bug
                ValidateMethodExtent(symReader, 0x06000002, "A.cs", 10, 10);
                ValidateMethodExtent(symReader, 0x06000002, "B.cs", 20, 20);
            }
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void UpdateMethodLines_FindClosestLine1(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            // Method A (0x06000001)
            //{
            //   IL_0000: (6, 16) - (6, 17) 0x30000001(Document) Enc1.cs
            //}
            // Method F (0x06000002)
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }
            // Method E (0x06000003)
            // {
            //   IL_0000: (17, 16) - (17, 17) 0x30000001(Document) Enc1.cs
            // }

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000002, new[] { -1, 3, 2, 1 }, 4));

            var docEnc = symReader.GetDocument("Enc1.cs");
            var docA = symReader.GetDocument("A.cs");
            var docB = symReader.GetDocument("B.cs");

            int closestLine;
            Assert.Equal(HResult.S_OK, docEnc.FindClosestLine(1, out closestLine));
            Assert.Equal(6, closestLine);

            if (portable)
            {
                Assert.Equal(HResult.S_OK, docEnc.FindClosestLine(7, out closestLine));
                Assert.Equal(8, closestLine);

                Assert.Equal(HResult.S_OK, docEnc.FindClosestLine(9, out closestLine));
                Assert.Equal(16, closestLine);
            }
            else
            {
                // diasymreader bug
                Assert.Equal(HResult.S_OK, docEnc.FindClosestLine(7, out closestLine));
                Assert.Equal(9, closestLine);

                Assert.Equal(HResult.S_OK, docEnc.FindClosestLine(10, out closestLine));
                Assert.Equal(15, closestLine);
            }

            Assert.Equal(HResult.S_OK, docEnc.FindClosestLine(17, out closestLine));
            Assert.Equal(17, closestLine);

            if (portable)
            {
                Assert.Equal(HResult.S_OK, docA.FindClosestLine(1, out closestLine));
                Assert.Equal(13, closestLine);

                Assert.Equal(HResult.S_OK, docB.FindClosestLine(1, out closestLine));
                Assert.Equal(22, closestLine);
            }
            else
            {
                // diasymreader bug
                Assert.Equal(HResult.S_OK, docA.FindClosestLine(1, out closestLine));
                Assert.Equal(10, closestLine);

                Assert.Equal(HResult.S_OK, docB.FindClosestLine(1, out closestLine));
                Assert.Equal(20, closestLine);
            }
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void SequencePoints1(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            // Gen 0:
            // Method F (0x06000002): nil (Document) #4c
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            // Gen 1:
            // Method F (0x06000002): nil (Document) #45
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (10, 9) - (10, 29) 0x30000003 (Document) C.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable));

            var docEnc = symReader.GetDocument("Enc1.cs");
            var docA = symReader.GetDocument("A.cs");
            var docB = symReader.GetDocument("B.cs");
            var method = symReader.GetMethod(0x06000002);

            Assert.Equal(2, symReader.GetMethodVersion(method));

            VerifySequencePoints(method,
                @"(9, 5) - (9, 6) 'C:\Enc1.cs'",
                @"(10, 9) - (10, 29) 'C:\F\A.cs'",
                @"(10, 9) - (10, 29) 'C:\F\C.cs'",
                @"(15, 5) - (15, 6) 'C:\Enc1.cs'");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void SequencePoints2(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            // Method F (0x06000002): nil (Document) #4c
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000002, new[] { -1, 3, 2, 1 }, 4));

            var docEnc = symReader.GetDocument("Enc1.cs");
            var docA = symReader.GetDocument("A.cs");
            var docB = symReader.GetDocument("B.cs");
            var method = symReader.GetMethod(0x06000002);

            VerifySequencePoints(method,
                @"(8, 5) - (8, 6) 'C:\Enc1.cs'",
                @"(13, 9) - (13, 29) 'C:\F\A.cs'",
                @"(22, 9) - (22, 29) 'C:\F\B.cs'",
                @"(16, 5) - (16, 6) 'C:\Enc1.cs'");

            int ilOffset;
            int count;
            var ranges = new int[2];

            if (portable)
            {
                Assert.Equal(HResult.S_OK, method.GetOffset(docEnc, 8, 1, out ilOffset));
                Assert.Equal(0, ilOffset);

                Assert.Equal(HResult.S_OK, method.GetOffset(docA, 13, 1, out ilOffset));
                Assert.Equal(1, ilOffset);

                Assert.Equal(HResult.S_OK, method.GetOffset(docB, 22, 1, out ilOffset));
                Assert.Equal(7, ilOffset);

                Assert.Equal(HResult.S_OK, method.GetOffset(docEnc, 16, 1, out ilOffset));
                Assert.Equal(0xD, ilOffset);

                Assert.Equal(HResult.S_OK, method.GetRanges(docEnc, 8, 1, ranges.Length, out count, ranges));
                Assert.Equal(0, ranges[0]);
                Assert.Equal(1, ranges[1]);

                Assert.Equal(HResult.S_OK, method.GetRanges(docA, 13, 1, ranges.Length, out count, ranges));
                Assert.Equal(1, ranges[0]);
                Assert.Equal(7, ranges[1]);

                Assert.Equal(HResult.S_OK, method.GetRanges(docB, 22, 1, ranges.Length, out count, ranges));
                Assert.Equal(7, ranges[0]);
                Assert.Equal(0xd, ranges[1]);

                Assert.Equal(HResult.S_OK, method.GetRanges(docEnc, 16, 1, ranges.Length, out count, ranges));
                Assert.Equal(0xd, ranges[0]);
                Assert.Equal(0xe, ranges[1]);
            }
            else
            {
                // diasymreader bug, it seems to ignore applied line deltas:
                Assert.Equal(HResult.S_OK, method.GetOffset(docEnc, 9, 1, out ilOffset));
                Assert.Equal(0, ilOffset);

                Assert.Equal(HResult.E_FAIL, method.GetOffset(docEnc, 8, 1, out ilOffset));
                Assert.Equal(HResult.E_FAIL, method.GetOffset(docA, 13, 1, out ilOffset));
                Assert.Equal(HResult.E_FAIL, method.GetOffset(docB, 22, 1, out ilOffset));
                Assert.Equal(HResult.E_FAIL, method.GetOffset(docEnc, 16, 1, out ilOffset));

                // diasymreader bug, it seems to ignore applied line deltas:
                Assert.Equal(HResult.S_OK, method.GetRanges(docEnc, 9, 1, ranges.Length, out count, ranges));
                Assert.Equal(0, ranges[0]);
                Assert.Equal(1, ranges[1]);

                Assert.Equal(HResult.S_OK, method.GetRanges(docEnc, 8, 1, ranges.Length, out count, ranges));
                Assert.Equal(0, count);

                Assert.Equal(HResult.S_OK, method.GetRanges(docA, 13, 1, ranges.Length, out count, ranges));
                Assert.Equal(0, count);

                Assert.Equal(HResult.S_OK, method.GetRanges(docB, 22, 1, ranges.Length, out count, ranges));
                Assert.Equal(0, count);

                Assert.Equal(HResult.S_OK, method.GetRanges(docEnc, 16, 1, ranges.Length, out count, ranges));
                Assert.Equal(0, count);
            }
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void SequencePoints3(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            var docEnc = symReader.GetDocument("Enc1.cs");
            var docA = symReader.GetDocument("A.cs");
            var docB = symReader.GetDocument("B.cs");

            // Gen 0:
            // Method A (0x06000001):
            // {
            //   IL_0000: (6, 16) - (6, 17) 0x30000001(Document)
            // }
            // Method F (0x06000002):
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }
            // Method E (0x06000003):
            // {
            //   IL_0000: (17, 16) - (17, 17) 0x30000001
            // }

            var methodA0 = symReader.GetMethod(0x06000001);
            var methodF0 = symReader.GetMethod(0x06000002);
            var methodE0 = symReader.GetMethod(0x06000003);
            Assert.Equal(1, symReader.GetMethodVersion(methodA0));
            Assert.Equal(1, symReader.GetMethodVersion(methodF0));
            Assert.Equal(1, symReader.GetMethodVersion(methodE0));

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000001, new[] { -1 }, 1));
            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000002, new[] { 0, 3, 2 }, 4));
            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000003, new[] { 100 }, 1));

            VerifySequencePoints(methodA0,
                @"(5, 16) - (5, 17) 'C:\Enc1.cs'");

            VerifySequencePoints(methodF0,
                @"(9, 5) - (9, 6) 'C:\Enc1.cs'",
                @"(13, 9) - (13, 29) 'C:\F\A.cs'",
                @"(22, 9) - (22, 29) 'C:\F\B.cs'",
                @"(15, 5) - (15, 6) 'C:\Enc1.cs'");

            VerifySequencePoints(methodE0,
                @"(117, 16) - (117, 17) 'C:\Enc1.cs'");

            // Gen 1:
            // Method F (0x06000002): nil (Document) #45
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (10, 9) - (10, 29) 0x30000003 (Document) C.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable), new[]
            {
                new SymUnmanagedLineDelta(0x06000003, 1000)
            });

            var methodA1 = symReader.GetMethod(0x06000001);
            var methodF1 = symReader.GetMethod(0x06000002);
            var methodE1 = symReader.GetMethod(0x06000003);
            Assert.Equal(1, symReader.GetMethodVersion(methodA1));
            Assert.Equal(2, symReader.GetMethodVersion(methodF1));
            Assert.Equal(1, symReader.GetMethodVersion(methodE1));

            // line updates for A preserved:
            VerifySequencePoints(methodA1,
                @"(5, 16) - (5, 17) 'C:\Enc1.cs'");

            // method body update overwrites any previously set line deltas:
            VerifySequencePoints(methodF1,
                @"(9, 5) - (9, 6) 'C:\Enc1.cs'",
                @"(10, 9) - (10, 29) 'C:\F\A.cs'",
                @"(10, 9) - (10, 29) 'C:\F\C.cs'",
                @"(15, 5) - (15, 6) 'C:\Enc1.cs'");

            // line updates for E combined with previous:
            VerifySequencePoints(methodE1,
                @"(1117, 16) - (1117, 17) 'C:\Enc1.cs'");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void SequencePoints4(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            var docEnc = symReader.GetDocument("Enc1.cs");
            var docA = symReader.GetDocument("A.cs");
            var docB = symReader.GetDocument("B.cs");

            // Gen 0:
            // Method A (0x06000001):
            // {
            //   IL_0000: (6, 16) - (6, 17)
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable), new[] 
            {
                new SymUnmanagedLineDelta(0x06000001, 10)
            });
            
            var methodA0 = symReader.GetMethodByVersion(0x06000001, 1);

            VerifySequencePoints(methodA0,
               @"(16, 16) - (16, 17) 'C:\Enc1.cs'");

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000001, new[] { 50 }, 1));

            VerifySequencePoints(methodA0,
               @"(66, 16) - (66, 17) 'C:\Enc1.cs'");

            // Gen 2:
            // {
            //    IL_0000: (6, 16) - (6, 17)
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable));

            var methodA1 = symReader.GetMethodByVersion(0x06000001, 3);

            // UpdateSymReaderFromResource removes line updates for methods updated in the delta.
            // UpdateMethodLines updates all versions of the method.
            VerifySequencePoints(methodA0,
               @"(6, 16) - (6, 17) 'C:\Enc1.cs'");

            VerifySequencePoints(methodA1,
                @"(6, 16) - (6, 17) 'C:\Enc1.cs'");

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000001, new[] { 100 }, 1));

            VerifySequencePoints(methodA0,
                @"(106, 16) - (106, 17) 'C:\Enc1.cs'");

            VerifySequencePoints(methodA1,
                @"(106, 16) - (106, 17) 'C:\Enc1.cs'");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void SequencePoints_PreRemap(bool portable)
        {
            // there seems to be no difference between GetMethodByVersion and GetMethodByVersionPreRemap

            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            var docEnc = symReader.GetDocument("Enc1.cs");
            var docA = symReader.GetDocument("A.cs");
            var docB = symReader.GetDocument("B.cs");

            // Gen 0:
            // Method A (0x06000001):
            // {
            //   IL_0000: (6, 16) - (6, 17)
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable), new[]
            {
                new SymUnmanagedLineDelta(0x06000001, 10)
            });

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersionPreRemap(0x06000001, 1, out var methodA0));

            VerifySequencePoints(methodA0,
               @"(16, 16) - (16, 17) 'C:\Enc1.cs'");

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000001, new[] { 50 }, 1));

            VerifySequencePoints(methodA0,
               @"(66, 16) - (66, 17) 'C:\Enc1.cs'");

            // Gen 2:
            // {
            //    IL_0000: (6, 16) - (6, 17)
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable));

            Assert.Equal(HResult.S_OK, symReader.GetMethodByVersionPreRemap(0x06000001, 1, out var methodA1));

            // UpdateSymReaderFromResource removes line updates for methods updated in the delta.
            // UpdateMethodLines updates all versions of the method.
            VerifySequencePoints(methodA0,
               @"(6, 16) - (6, 17) 'C:\Enc1.cs'");

            VerifySequencePoints(methodA1,
                @"(6, 16) - (6, 17) 'C:\Enc1.cs'");

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000001, new[] { 100 }, 1));

            VerifySequencePoints(methodA0,
                @"(106, 16) - (106, 17) 'C:\Enc1.cs'");

            VerifySequencePoints(methodA1,
                @"(106, 16) - (106, 17) 'C:\Enc1.cs'");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void SequencePoints_Overflow(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            var docEnc = symReader.GetDocument("Enc1.cs");
            var docA = symReader.GetDocument("A.cs");
            var docB = symReader.GetDocument("B.cs");

            // Gen 0:
            // Method A (0x06000001):
            // {
            //   IL_0000: (6, 16) - (6, 17)
            // }

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable), new[]
            {
                new SymUnmanagedLineDelta(0x06000001, -10)
            });

            var methodA0 = symReader.GetMethodByVersion(0x06000001, 1);

            VerifySequencePoints(methodA0,
               @"(-4, 16) - (-4, 17) 'C:\Enc1.cs'");

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000001, new[] { int.MaxValue }, 1));

            VerifySequencePoints(methodA0,
               @"(2147483643, 16) - (2147483643, 17) 'C:\Enc1.cs'");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void SequencePoints_Hidden(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Documents.DllAndPdb(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            // Method M (0x06000001):
            // {
            //   IL_0000: (7, 5) - (7, 6)      0x30000001(Document) 'C:\Documents.cs'
            //   IL_0001: (10, 9) - (10, 30)   0x30000002(Document) 'C:\a\b\c\d\1.cs'
            //   IL_0008: (20, 9) - (20, 30)   0x30000003(Document) 'C:\a\b\c\D\2.cs'
            //   IL_000F: (30, 9) - (30, 30)   0x30000004(Document) 'C:\a\b\C\d\3.cs'
            //   IL_0016: (40, 9) - (40, 30)   0x30000004(Document) 'C:\a\b\C\d\3.cs'
            //   IL_001D: <hidden>                                  
            //   IL_0023: (50, 9) - (50, 30)   0x30000005(Document) 'C:\a\b\c\d\x.cs'
            //   IL_002A: (60, 9) - (60, 30)   0x30000006(Document) 'C:\A\b\c\x.cs'  
            //   IL_0031: (70, 9) - (70, 30)   0x30000007(Document) 'C:\a\b\x.cs'    
            //   IL_0038: (80, 9) - (80, 30)   0x30000008(Document) 'C:\a\B\3.cs'    
            //   IL_003F: (90, 9) - (90, 30)   0x30000009(Document) 'C:\a\B\c\4.cs'  
            //   IL_0046: (100, 9) - (100, 30) 0x3000000a(Document) 'C:\*\5.cs'      
            //   IL_004D: (110, 9) - (110, 30) 0x3000000b(Document) ':6.cs'          
            //   IL_0054: (120, 9) - (120, 30) 0x3000000c(Document) 'C:\a\b\X.cs'    
            //   IL_005B: (130, 9) - (130, 30) 0x3000000d(Document) 'C:\a\B\x.cs'    
            //   IL_0062: (131, 5) - (131, 6)  0x3000000d(Document) 'C:\a\B\x.cs'    
            // }

            var deltas = new[]
            {
               1000,
               2000,
               3000,
               4000,
               5000,
               6000,
               7000,
               8000,
               9000,
               10000,
               11000,
               12000,
               13000,
               14000,
               15000,
               16000,
            };

            Assert.Equal(HResult.S_OK, symEncUpdate.UpdateMethodLines(0x06000001, deltas, deltas.Length));

            VerifySequencePoints(symReader.GetMethod(0x06000001),
                @"(1007, 5) - (1007, 6) 'C:\Documents.cs'",
                @"(2010, 9) - (2010, 30) 'C:\a\b\c\d\1.cs'",
                @"(3020, 9) - (3020, 30) 'C:\a\b\c\D\2.cs'",
                @"(4030, 9) - (4030, 30) 'C:\a\b\C\d\3.cs'",
                @"(5040, 9) - (5040, 30) 'C:\a\b\C\d\3.cs'",
                @"<hidden>",
                @"(7050, 9) - (7050, 30) 'C:\a\b\c\d\x.cs'",
                @"(8060, 9) - (8060, 30) 'C:\A\b\c\x.cs'",
                @"(9070, 9) - (9070, 30) 'C:\a\b\x.cs'",
                @"(10080, 9) - (10080, 30) 'C:\a\B\3.cs'",
                @"(11090, 9) - (11090, 30) 'C:\a\B\c\4.cs'",
                @"(12100, 9) - (12100, 30) 'C:\*\5.cs'",
                @"(13110, 9) - (13110, 30) ':6.cs'",
                @"(14120, 9) - (14120, 30) 'C:\a\b\X.cs'",
                @"(15130, 9) - (15130, 30) 'C:\a\B\x.cs'",
                @"(16131, 5) - (16131, 6) 'C:\a\B\x.cs'");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetLocalVariables_Errors(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Scopes.DllAndPdb(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            Assert.Equal(HResult.E_FAIL, symEncUpdate.GetLocalVariableCount(0, out int count));
            Assert.Equal(HResult.E_FAIL, symEncUpdate.GetLocalVariableCount(0x01000001, out count));
            Assert.Equal(HResult.E_FAIL, symEncUpdate.GetLocalVariableCount(0x06000100, out count));

            var vars = new ISymUnmanagedVariable[10];
            Assert.Equal(HResult.E_INVALIDARG, symEncUpdate.GetLocalVariables(0x06000003, 0, null, out count));
            Assert.Equal(HResult.S_OK, symEncUpdate.GetLocalVariables(0x06000003, -1, vars, out count));
            Assert.Equal(HResult.S_OK, symEncUpdate.GetLocalVariables(0x06000003, int.MaxValue, vars, out count));
            Assert.Equal(HResult.E_WIN32_NOT_ENOUGH_MEMORY, symEncUpdate.GetLocalVariables(0x06000003, 1, vars, out count));
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetLocalVariables1(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Scopes.DllAndPdb(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            VerifyLocalVariables(symEncUpdate, 0x06000003, "x0", "y0", "x1", "y1", "y2");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetLocalVariables2(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Scopes.DllAndPdb(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            VerifyLocalVariables(symEncUpdate, 0x06000004, "a", "b", "c", "d", "e", "f", "g", "h", "d", "i", "j", "d");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetLocalVariablesUpdate(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            // method w/o debug info:
            Assert.Equal(HResult.E_FAIL, symEncUpdate.GetLocalVariableCount(0x06000005, out int count));

            VerifyLocalVariables(symEncUpdate, 0x06000004, "H1", "H2");
            VerifyLocalVariables(symEncUpdate, 0x06000009, "H3");
            
            // gen 1:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(1, portable));

            VerifyLocalVariables(symEncUpdate, 0x06000004, "H1", "H2");
            VerifyLocalVariables(symEncUpdate, 0x06000009, "H3", "H4");

            // gen 2:

            UpdateSymReaderFromResource(symReader, TestResources.EncMethodExtents.Diffs(2, portable));

            VerifyLocalVariables(symEncUpdate, 0x06000004, "H2");
            VerifyLocalVariables(symEncUpdate, 0x06000009, "H4");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetLocalVariablesUpdate_LegacyUpdate(bool portable)
        {
            string deltaPath1 = Path.GetTempFileName();
            File.WriteAllBytes(deltaPath1, TestResources.EncMethodExtents.Diffs(1, portable).Pdb);

            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));
            var symEncUpdate = (ISymUnmanagedEncUpdate)symReader;

            // method w/o debug info:
            Assert.Equal(HResult.E_FAIL, symEncUpdate.GetLocalVariableCount(0x06000005, out int count));

            VerifyLocalVariables(symEncUpdate, 0x06000004, "H1", "H2");
            VerifyLocalVariables(symEncUpdate, 0x06000009, "H3");

            // gen 1:
            Assert.Equal(HResult.S_OK, symReader.UpdateSymbolStore(deltaPath1, default(IStream)));

            VerifyLocalVariables(symEncUpdate, 0x06000004, "H1", "H2");
            VerifyLocalVariables(symEncUpdate, 0x06000009, "H3", "H4");

            // gen 2:
            var deltaStream2 = SymUnmanagedStreamFactory.CreateStream(new MemoryStream(TestResources.EncMethodExtents.Diffs(2, portable).Pdb));

            Assert.Equal(HResult.S_OK, symReader.UpdateSymbolStore(default(string), deltaStream2));

            VerifyLocalVariables(symEncUpdate, 0x06000004, "H2");
            VerifyLocalVariables(symEncUpdate, 0x06000009, "H4");

            ((ISymUnmanagedDispose)symReader).Destroy();

            File.Delete(deltaPath1);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetFileNameFromOffset1(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            // Method A (0x06000001) 0x30000001(Document) Enc1.cs
            // {
            //   IL_0000: (6, 16) - (6, 17) 0x30000001(Document) Enc1.cs
            // }
            // Method F (0x06000002): nil (Document)
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            var methodA = (ISymEncUnmanagedMethod)symReader.GetMethod(0x06000001);
            var methodF = (ISymEncUnmanagedMethod)symReader.GetMethod(0x06000002);

            AssertEx.Equal(new[]
            {
                @"C:\Enc1.cs",
                @"C:\Enc1.cs",
                @"C:\Enc1.cs",
            }, GetFileNameForEachILOffset(-1, 2, methodA));

            AssertEx.Equal(new[] 
            {
                @"C:\Enc1.cs", //-1
                @"C:\Enc1.cs", // 0
                @"C:\F\A.cs",  // 1
                @"C:\F\A.cs",
                @"C:\F\A.cs",
                @"C:\F\A.cs",
                @"C:\F\A.cs",
                @"C:\F\A.cs",
                @"C:\F\B.cs", // 7
                @"C:\F\B.cs",
                @"C:\F\B.cs",
                @"C:\F\B.cs",
                @"C:\F\B.cs",
                @"C:\F\B.cs",
                @"C:\Enc1.cs", // 0xD
                @"C:\Enc1.cs",
            }, GetFileNameForEachILOffset(-1, 0xf, methodF));
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetLineFromOffset1(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            // Method A (0x06000001) 0x30000001(Document) Enc1.cs
            // {
            //   IL_0000: (6, 16) - (6, 17) 0x30000001(Document) Enc1.cs
            // }
            // Method F (0x06000002): nil (Document)
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            var methodA = (ISymEncUnmanagedMethod)symReader.GetMethod(0x06000001);
            var methodF = (ISymEncUnmanagedMethod)symReader.GetMethod(0x06000002);

            AssertEx.Equal(new[]
            {
                "IL_0000 (6, 16) - (6, 17)",
                "IL_0000 (6, 16) - (6, 17)",
                "IL_0000 (6, 16) - (6, 17)"
            }, GetLineForEachILOffset(-1, 2, methodA));

            AssertEx.Equal(new[]
            {
                "IL_000D (15, 5) - (15, 6)",
                "IL_0000 (9, 5) - (9, 6)",
                "IL_0001 (10, 9) - (10, 29)",
                "IL_0001 (10, 9) - (10, 29)",
                "IL_0001 (10, 9) - (10, 29)",
                "IL_0001 (10, 9) - (10, 29)",
                "IL_0001 (10, 9) - (10, 29)",
                "IL_0001 (10, 9) - (10, 29)",
                "IL_0007 (20, 9) - (20, 29)",
                "IL_0007 (20, 9) - (20, 29)",
                "IL_0007 (20, 9) - (20, 29)",
                "IL_0007 (20, 9) - (20, 29)",
                "IL_0007 (20, 9) - (20, 29)",
                "IL_0007 (20, 9) - (20, 29)",
                "IL_000D (15, 5) - (15, 6)",
                "IL_000D (15, 5) - (15, 6)"
            }, GetLineForEachILOffset(-1, 0xf, methodF));
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetDocumentsForMethod1(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Documents.DllAndPdb(portable));

            // Method M (0x06000001):
            // {
            //   IL_0000: (7, 5) - (7, 6)      0x30000001(Document) 'C:\Documents.cs'
            //   IL_0001: (10, 9) - (10, 30)   0x30000002(Document) 'C:\a\b\c\d\1.cs'
            //   IL_0008: (20, 9) - (20, 30)   0x30000003(Document) 'C:\a\b\c\D\2.cs'
            //   IL_000F: (30, 9) - (30, 30)   0x30000004(Document) 'C:\a\b\C\d\3.cs'
            //   IL_0016: (40, 9) - (40, 30)   0x30000004(Document) 'C:\a\b\C\d\3.cs'
            //   IL_001D: <hidden>                                  
            //   IL_0023: (50, 9) - (50, 30)   0x30000005(Document) 'C:\a\b\c\d\x.cs'
            //   IL_002A: (60, 9) - (60, 30)   0x30000006(Document) 'C:\A\b\c\x.cs'  
            //   IL_0031: (70, 9) - (70, 30)   0x30000007(Document) 'C:\a\b\x.cs'    
            //   IL_0038: (80, 9) - (80, 30)   0x30000008(Document) 'C:\a\B\3.cs'    
            //   IL_003F: (90, 9) - (90, 30)   0x30000009(Document) 'C:\a\B\c\4.cs'  
            //   IL_0046: (100, 9) - (100, 30) 0x3000000a(Document) 'C:\*\5.cs'      
            //   IL_004D: (110, 9) - (110, 30) 0x3000000b(Document) ':6.cs'          
            //   IL_0054: (120, 9) - (120, 30) 0x3000000c(Document) 'C:\a\b\X.cs'    
            //   IL_005B: (130, 9) - (130, 30) 0x3000000d(Document) 'C:\a\B\x.cs'    
            //   IL_0062: (131, 5) - (131, 6)  0x3000000d(Document) 'C:\a\B\x.cs'    
            // }

            var method = (ISymEncUnmanagedMethod)symReader.GetMethod(0x06000001);
            var documents = new ISymUnmanagedDocument[20];

            if (portable)
            {
                // diasymreader bug: one document is missing non-deterministically

                Assert.Equal(HResult.S_OK, method.GetDocumentsForMethodCount(out int count));
                Assert.Equal(13, count);

                Assert.Equal(HResult.S_OK, method.GetDocumentsForMethod(count, out int actualCount, documents));
                Assert.Equal(count, actualCount);

                AssertEx.Equal(new[]
                {
                    @"C:\Documents.cs",
                    @"C:\a\b\c\d\1.cs",
                    @"C:\a\b\c\D\2.cs",
                    @"C:\a\b\C\d\3.cs",
                    @"C:\a\b\c\d\x.cs",
                    @"C:\A\b\c\x.cs",
                    @"C:\a\b\x.cs",
                    @"C:\a\B\3.cs",
                    @"C:\a\B\c\4.cs",
                    @"C:\*\5.cs",
                    @":6.cs",
                    @"C:\a\b\X.cs",
                    @"C:\a\B\x.cs",
                }, documents.Take(actualCount).Select(d => d.GetName()));
            }
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetDocumentsForMethod2(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.EncMethodExtents.Baseline(portable));

            // Method A (0x06000001) 0x30000001(Document) Enc1.cs
            // {
            //   IL_0000: (6, 16) - (6, 17) 0x30000001(Document) Enc1.cs
            // }
            // Method F (0x06000002): nil (Document)
            // {
            //   IL_0000: (9, 5) - (9, 6) 0x30000001 (Document) Enc1.cs
            //   IL_0001: (10, 9) - (10, 29) 0x30000002 (Document) A.cs
            //   IL_0007: (20, 9) - (20, 29) 0x30000003 (Document) B.cs
            //   IL_000D: (15, 5) - (15, 6) 0x30000001 (Document) Enc1.cs
            // }

            var methodA = (ISymEncUnmanagedMethod)symReader.GetMethod(0x06000001);
            var methodF = (ISymEncUnmanagedMethod)symReader.GetMethod(0x06000002);
            var documents = new ISymUnmanagedDocument[10];

            Assert.Equal(HResult.S_OK, methodA.GetDocumentsForMethodCount(out int count));
            Assert.Equal(1, count);
            Assert.Equal(HResult.S_OK, methodA.GetDocumentsForMethod(count, out int actualCount, documents));

            AssertEx.Equal(new[]
            {
                @"C:\Enc1.cs",
            }, documents.Take(actualCount).Select(d => d.GetName()));

            if (portable)
            {
                // diasymreader bug: one document is missing non-deterministically

                Assert.Equal(HResult.S_OK, methodF.GetDocumentsForMethodCount(out count));
                Assert.Equal(3, count);
                Assert.Equal(HResult.S_OK, methodF.GetDocumentsForMethod(count, out actualCount, documents));

                AssertEx.Equal(new[]
                {
                    @"C:\Enc1.cs",
                    @"C:\F\A.cs",
                    @"C:\F\B.cs",
                }, documents.Take(actualCount).Select(d => d.GetName()));
            }
        }

        [Fact]
        public unsafe void GetPortableDebugMetadata()
        {
            var baselineResources = TestResources.EncMethodExtents.Baseline(portable: true);
            var symReader = (ISymUnmanagedReader5)CreateSymReaderFromResource(baselineResources);

            byte[] bytes;
            byte* metadata;
            int size;

            Assert.Equal(HResult.S_OK, symReader.GetPortableDebugMetadata(out metadata, out size));
            bytes = new byte[size];
            Marshal.Copy((IntPtr)metadata, bytes, 0, bytes.Length);
            AssertEx.Equal(baselineResources.Pdb, bytes);

            // gen 1:

            var resources1 = TestResources.EncMethodExtents.Diffs(1, portable: true);
            UpdateSymReaderFromResource(symReader, resources1);

            Assert.Equal(HResult.S_OK, symReader.GetPortableDebugMetadata(out metadata, out size));
            bytes = new byte[size];
            Marshal.Copy((IntPtr)metadata, bytes, 0, bytes.Length);
            AssertEx.Equal(resources1.Pdb, bytes);

            // gen 2:

            var resources2 = TestResources.EncMethodExtents.Diffs(2, portable: true);
            UpdateSymReaderFromResource(symReader, resources2);

            Assert.Equal(HResult.S_OK, symReader.GetPortableDebugMetadata(out metadata, out size));
            bytes = new byte[size];
            Marshal.Copy((IntPtr)metadata, bytes, 0, bytes.Length);
            AssertEx.Equal(resources2.Pdb, bytes);
        }

        [Fact]
        public unsafe void GetPortableDebugMetadataByVersion()
        {
            var baselineResources = TestResources.EncMethodExtents.Baseline(portable: true);
            var symReader = (ISymUnmanagedReader5)CreateSymReaderFromResource(baselineResources);

            var resources1 = TestResources.EncMethodExtents.Diffs(1, portable: true);
            UpdateSymReaderFromResource(symReader, resources1);

            var resources2 = TestResources.EncMethodExtents.Diffs(2, portable: true);
            UpdateSymReaderFromResource(symReader, resources2);

            byte[] bytes;
            byte* metadata;
            int size;

            Assert.Equal(HResult.S_OK, symReader.GetPortableDebugMetadataByVersion(1, out metadata, out size));
            bytes = new byte[size];
            Marshal.Copy((IntPtr)metadata, bytes, 0, bytes.Length);
            AssertEx.Equal(baselineResources.Pdb, bytes);

            Assert.Equal(HResult.S_OK, symReader.GetPortableDebugMetadataByVersion(2, out metadata, out size));
            bytes = new byte[size];
            Marshal.Copy((IntPtr)metadata, bytes, 0, bytes.Length);
            AssertEx.Equal(resources1.Pdb, bytes);
            
            Assert.Equal(HResult.S_OK, symReader.GetPortableDebugMetadataByVersion(3, out metadata, out size));
            bytes = new byte[size];
            Marshal.Copy((IntPtr)metadata, bytes, 0, bytes.Length);
            AssertEx.Equal(resources2.Pdb, bytes);

            Assert.Equal(HResult.E_INVALIDARG, symReader.GetPortableDebugMetadataByVersion(-1, out metadata, out size));
            Assert.Equal(HResult.E_INVALIDARG, symReader.GetPortableDebugMetadataByVersion(0, out metadata, out size));
            Assert.Equal(HResult.E_INVALIDARG, symReader.GetPortableDebugMetadataByVersion(4, out metadata, out size));
        }
    }
}
