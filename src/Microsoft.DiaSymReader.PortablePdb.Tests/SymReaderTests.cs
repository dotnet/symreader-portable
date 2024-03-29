﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using Roslyn.Test.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.DiaSymReader.PortablePdb.UnitTests
{
    using static SymTestHelpers;

    public class SymReaderTests
    {
        [Fact]
        public unsafe void TestMetadataHeaders1()
        {
            fixed (byte* pdbPtr = TestResources.Documents.PortablePdb)
            {
                var pdbReader = new MetadataReader(pdbPtr, TestResources.Documents.PortablePdb.Length);
                Assert.Equal("PDB v1.0", pdbReader.MetadataVersion);
                Assert.Equal(MetadataKind.Ecma335, pdbReader.MetadataKind);
                Assert.False(pdbReader.IsAssembly);
                Assert.True(pdbReader.DebugMetadataHeader.EntryPoint.IsNil);

                AssertEx.Equal(new byte[]
                {
                    0x85, 0x1E, 0xD4, 0x2E, 0xBA, 0x3C, 0x65, 0x42, 0xB5, 0xB3, 0x7A, 0xFC, 0x7E, 0xBC, 0xB7, 0x39, 0x34, 0x8B, 0x33, 0xC2
                }, pdbReader.DebugMetadataHeader.Id);
            }
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void MatchesModule(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Documents.DllAndPdb(portable));

            Guid expectedGuid;
            uint expectedStamp;

            if (portable)
            {
                expectedGuid = new Guid("2ed41e85-3cba-4265-b5b3-7afc7ebcb739");
                expectedStamp = 0xc2338b34;
            }
            else
            {
                expectedGuid = new Guid("bf4a0c1a-fbf1-4b2c-b3a7-a37b96754e40");
                expectedStamp = 0x8CF1D2D4;
            }


            var anotherGuid = new Guid(new byte[] { 0x88, 0x03, 0x86, 0xAD, 0xFF, 0x27, 0x56, 0x46, 0x9F, 0x3F, 0xE2, 0x18, 0x4B, 0xEF, 0xFC, 0xC0 });
            var anotherStamp = 0xA0520CBF;

            bool matches;
            Assert.Equal(HResult.S_OK, symReader.MatchesModule(expectedGuid, expectedStamp, 1, out matches));
            Assert.True(matches);
            Assert.Equal(HResult.S_OK, symReader.MatchesModule(expectedGuid, expectedStamp, 12, out matches));
            Assert.False(matches);
            Assert.Equal(HResult.S_OK, symReader.MatchesModule(expectedGuid, expectedStamp, 2, out matches));
            Assert.False(matches);
            Assert.Equal(HResult.S_OK, symReader.MatchesModule(anotherGuid, expectedStamp, 1, out matches));
            Assert.False(matches);

            if (portable)
            {
                // Verify matching without the timestamp works correctly also
                Assert.Equal(HResult.S_OK, symReader.MatchesModule(expectedGuid, 0, -1, out matches));
                Assert.True(matches);
                Assert.Equal(HResult.S_OK, symReader.MatchesModule(anotherGuid, 0, -1, out matches));
                Assert.False(matches);
            }

            // Windows PDB matching ignores the stamp:
            Assert.Equal(HResult.S_OK, symReader.MatchesModule(expectedGuid, anotherStamp, 1, out matches));
            Assert.Equal(!portable, matches);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public unsafe void GetSourceServerData_None(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Documents.DllAndPdb(portable));
            Assert.Equal(HResult.S_FALSE, symReader.GetSourceServerData(out byte* ptr, out int size));
            Assert.Equal(0, size);
            Assert.Equal(IntPtr.Zero, (IntPtr)ptr);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public unsafe void GetSourceServerData(bool portable)
        {
            // TODO: https://github.com/dotnet/symreader-portable/issues/112
            if (Path.DirectorySeparatorChar == '/')
            {
                return;
            }

            var symReader = CreateSymReaderFromResource(TestResources.SourceLink.DllAndPdb(portable));
            Assert.Equal(HResult.S_OK, symReader.GetSourceServerData(out byte* ptr, out int size));
            Assert.Equal(103, size);
            Assert.NotEqual(IntPtr.Zero, (IntPtr)ptr);

            var actual = new byte[size];
            Marshal.Copy((IntPtr)ptr, actual, 0, actual.Length);

            AssertEx.Equal(TestResources.SourceLink.Json, actual);
        }

        // TODO: https://github.com/dotnet/symreader-portable/issues/112
        [ConditionalFact(typeof(WindowsOnly))]
        public unsafe void GetSourceServerData_PortableEmbedded()
        {
            var symReader = CreateSymReaderFromEmbeddedPortablePdb(TestResources.SourceLink.EmbeddedDll);
            byte* ptr;
            int size;
            Assert.Equal(HResult.S_OK, symReader.GetSourceServerData(out ptr, out size));
            Assert.Equal(103, size);
            Assert.NotEqual(IntPtr.Zero, (IntPtr)ptr);

            var actual = new byte[size];
            Marshal.Copy((IntPtr)ptr, actual, 0, actual.Length);

            AssertEx.Equal(TestResources.SourceLink.Json, actual);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetDocuments(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Documents.DllAndPdb(portable));

            int actualCount;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(0, out actualCount, null));
            Assert.Equal(14, actualCount);

            var actualDocuments = new ISymUnmanagedDocument[actualCount];
            int actualCount2;
            Assert.Equal(HResult.S_OK, symReader.GetDocuments(actualCount, out actualCount2, actualDocuments));
            Assert.Equal(14, actualCount2);

            // C# compiler writes the primary document record first in Windows PDB and last in Windows PDB:
            ValidateDocument(actualDocuments[portable ? 0 : actualDocuments.Length - 1],
                url: @"/Documents.cs",
                algorithmId: "8829d00f-11b8-4213-878b-770e8597ac16",
                checksum: new byte[] { 0x5E, 0x65, 0xDD, 0xE1, 0xB0, 0xDD, 0x1D, 0xBD, 0x33, 0x14, 0x19, 0xBB, 0xB9, 0x25, 0xD9, 0xBA, 0x8E, 0x3A, 0x2D, 0x94, 0xCA, 0x64, 0x01, 0xE0, 0x02, 0xA1, 0x00, 0x04, 0x73, 0xF6, 0xB4, 0xAA });

            int i = portable ? 1 : 0;
            ValidateDocument(actualDocuments[i++],
                url: @"C:\Documents.cs",
                algorithmId: "ff1816ec-aa5e-4d10-87f7-6f4963833460",
                checksum: new byte[] { 0xDB, 0xEB, 0x2A, 0x06, 0x7B, 0x2F, 0x0E, 0x0D, 0x67, 0x8A, 0x00, 0x2C, 0x58, 0x7A, 0x28, 0x06, 0x05, 0x6C, 0x3D, 0xCE });

            ValidateDocument(actualDocuments[i++], url: @"C:\a\b\c\d\1.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\a\b\c\D\2.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\a\b\C\d\3.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\a\b\c\d\x.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\A\b\c\x.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\a\b\x.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\a\B\3.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\a\B\c\4.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\*\5.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @":6.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\a\b\X.cs", algorithmId: null, checksum: null);
            ValidateDocument(actualDocuments[i++], url: @"C:\a\B\x.cs", algorithmId: null, checksum: null);
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetDocument1(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Documents.DllAndPdb(portable));
            TestGetDocument(symReader, @"x.cs", expectedUrl: @"C:\a\b\c\d\x.cs");
            TestGetDocument(symReader, @"X.CS", expectedUrl: @"C:\a\b\c\d\x.cs");
            TestGetDocument(symReader, @"X.cs", expectedUrl: portable ? @"C:\a\b\X.cs" : @"C:\a\b\c\d\x.cs");
            TestGetDocument(symReader, @"1.cs", expectedUrl: @"C:\a\b\c\d\1.cs");
            TestGetDocument(symReader, @"2.cs", expectedUrl: @"C:\a\b\c\D\2.cs");
            TestGetDocument(symReader, @"3.cs", expectedUrl: @"C:\a\b\C\d\3.cs");
            TestGetDocument(symReader, @"C:\A\b\c\x.cs", expectedUrl: @"C:\A\b\c\x.cs");
            TestGetDocument(symReader, @"C:\a\b\x.cs", expectedUrl: @"C:\a\b\x.cs");
            TestGetDocument(symReader, @"C:\*\5.cs", expectedUrl: @"C:\*\5.cs");
            TestGetDocument(symReader, @"5.cs", expectedUrl: @"C:\*\5.cs");
            TestGetDocument(symReader, @":6.cs", expectedUrl: @":6.cs");
            TestGetDocument(symReader, @"C:\a\B\x.cs", expectedUrl: portable ? @"C:\a\B\x.cs" : @"C:\a\b\x.cs");
            TestGetDocument(symReader, @"C:\a\b\X.cs", expectedUrl: portable ? @"C:\a\b\X.cs" : @"C:\a\b\x.cs");
        }

        private void TestGetDocument(ISymUnmanagedReader symReader, string name, string expectedUrl)
        {
            ISymUnmanagedDocument document;
            if (expectedUrl != null)
            {
                // guids are ignored
                Assert.Equal(HResult.S_OK, symReader.GetDocument(name, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), out document));
                ValidateDocumentUrl(document, expectedUrl);
            }
            else
            {
                // guids are ignored
                Assert.Equal(HResult.S_FALSE, symReader.GetDocument(name, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), out document));
                Assert.Null(document);
            }
        }

        [Fact]
        public void TestSymGetAttribute()
        {
            var symReader = CreateSymReaderFromResource(TestResources.Documents.DllAndPdb(portable: true));

            Assert.Equal(HResult.S_FALSE, symReader.GetSymAttribute(0, "<PortablePdbImage>", 0, out var _, null));
        }

        [Fact]
        public void TestMethods1()
        {
            var symReader = CreateSymReaderFromResource(TestResources.Scopes.PortableDllAndPdb);
            int count;

            //
            //  C<S>.F<T>
            //

            ISymUnmanagedMethod mF;
            Assert.Equal(HResult.S_OK, symReader.GetMethod(0x06000002, out mF));

            // root scope:
            ISymUnmanagedScope rootScope, rootScopeCopy;
            Assert.Equal(HResult.S_OK, mF.GetRootScope(out rootScope));
            Assert.Equal(HResult.S_OK, mF.GetRootScope(out rootScopeCopy));
            Assert.NotSame(rootScope, rootScopeCopy);

            ValidateRange(rootScope, 0, 2);
            ValidateRootScope(rootScope);

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

            ValidateConstant(constants[0], "B", (short)0, new byte[] { 0x02 });
            ValidateConstant(constants[1], "C", (ushort)'\0', new byte[] { 0x03 });
            ValidateConstant(constants[2], "I1", (short)1, new byte[] { 0x04 });
            ValidateConstant(constants[3], "U1", (short)2, new byte[] { 0x05 });
            ValidateConstant(constants[4], "I2", (short)3, new byte[] { 0x06 });
            ValidateConstant(constants[5], "U2", (ushort)4, new byte[] { 0x07 });
            ValidateConstant(constants[6], "I4", 5, new byte[] { 0x08 });
            ValidateConstant(constants[7], "U4", (uint)6, new byte[] { 0x09 });
            ValidateConstant(constants[8], "I8", (long)7, new byte[] { 0x0A });
            ValidateConstant(constants[9], "U8", (ulong)8, new byte[] { 0x0B });
            ValidateConstant(constants[10], "R4", (float)9.1, new byte[] { 0x0C });
            ValidateConstant(constants[11], "R8", 10.2, new byte[] { 0x0D });

            ValidateConstant(constants[12], "EI1", (short)1, new byte[] { 0x11, 0x06 });
            ValidateConstant(constants[13], "EU1", (short)2, new byte[] { 0x11, 0x0A });
            ValidateConstant(constants[14], "EI2", (short)3, new byte[] { 0x11, 0x0E });
            ValidateConstant(constants[15], "EU2", (ushort)4, new byte[] { 0x11, 0x12 });
            ValidateConstant(constants[16], "EI4", 5, new byte[] { 0x11, 0x16 });
            ValidateConstant(constants[17], "EU4", (uint)6, new byte[] { 0x11, 0x1A });
            ValidateConstant(constants[18], "EI8", (long)7, new byte[] { 0x11, 0x1E });
            ValidateConstant(constants[19], "EU8", (ulong)8, new byte[] { 0x11, 0x22 });

            ValidateConstant(constants[20], "StrWithNul", "\0", new byte[] { 0x0e });
            ValidateConstant(constants[21], "EmptyStr", "", new byte[] { 0x0e });
            ValidateConstant(constants[22], "NullStr", 0, new byte[] { 0x0e });
            ValidateConstant(constants[23], "NullObject", 0, new byte[] { 0x1c });
            ValidateConstant(constants[24], "NullDynamic", 0, new byte[] { 0x1c });

            // Note: Native PDBs produce expanded form of the signature stored as StandAloneSig.
            // In Portable PDBs we produce a TypeSpec. Since a StandAlongSig can also contain a TypeSpec 
            // the consumers should be able to resolve it. If we find a case where that's not true we can
            // potentially expand the TypeSpec signature in ISymUnmanagedConstant.GetValue.
            ValidateConstant(constants[25], "NullTypeDef", 0, new byte[] { 0x12, 0x08 });
            ValidateConstant(constants[26], "NullTypeRef", 0, new byte[] { 0x12, 0x19 });
            ValidateConstant(constants[27], "NullTypeSpec", 0, new byte[] { 0x12, 0x26 });

            ValidateConstant(constants[28], "D", 123456.78M, new byte[] { 0x11, 0x29 });

            //
            //  C<S>.NestedScopes
            //

            ISymUnmanagedMethod mNestedScopes;
            Assert.Equal(HResult.S_OK, symReader.GetMethod(0x06000003, out mNestedScopes));

            // root scope:
            Assert.Equal(HResult.S_OK, mNestedScopes.GetRootScope(out rootScope));
            ValidateRootScope(rootScope);

            var main = GetAndValidateChildScopes(rootScope, expectedCount: 1)[0];
            constants = GetAndValidateConstants(main, expectedCount: 0);
            var variables = GetAndValidateVariables(main, expectedCount: 2);

            ValidateVariable(variables[0], "x0", 0, LocalVariableAttributes.None, new byte[] { 0x08 });
            ValidateVariable(variables[1], "y0", 1, LocalVariableAttributes.None, new byte[] { 0x08 });

            children = GetAndValidateChildScopes(main, expectedCount: 2);
            var first = children[0];
            GetAndValidateChildScopes(first, expectedCount: 0);
            var second = children[1];
            var third = GetAndValidateChildScopes(second, expectedCount: 1)[0];
            GetAndValidateChildScopes(third, expectedCount: 0);

            constants = GetAndValidateConstants(first, expectedCount: 1);
            variables = GetAndValidateVariables(first, expectedCount: 1);
            ValidateConstant(constants[0], "c1", 11, new byte[] { 0x08 });
            ValidateVariable(variables[0], "x1", 2, LocalVariableAttributes.None, new byte[] { 0x08 });

            constants = GetAndValidateConstants(second, expectedCount: 0);
            variables = GetAndValidateVariables(second, expectedCount: 1);
            ValidateVariable(variables[0], "y1", 3, LocalVariableAttributes.None, new byte[] { 0x08 });

            constants = GetAndValidateConstants(third, expectedCount: 2);
            variables = GetAndValidateVariables(third, expectedCount: 1);
            ValidateConstant(constants[0], "c2", "c2", new byte[] { 0x0e });
            ValidateConstant(constants[1], "d2", "d2", new byte[] { 0x0e });
            ValidateVariable(variables[0], "y2", 4, LocalVariableAttributes.None, new byte[] { 0x08 });

            // TODO:
            // f.GetOffset();
            // f.GetRanges();

            ISymUnmanagedNamespace ns;
            ISymUnmanagedVariable[] ps = null;
            Assert.Equal(HResult.E_NOTIMPL, mF.GetNamespace(out ns));
            Assert.Equal(HResult.E_NOTIMPL, mF.GetParameters(0, out count, ps));
            // TODO:
            // f.GetScopeFromOffset()
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void TestAsyncMethods(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Async.DllAndPdb(portable));

            ValidateAsyncMethod(
                symReader,
                moveNextMethodToken: 0x06000005,
                kickoffMethodToken: 0x06000001,
                catchHandlerOffset: -1,
                yieldOffsets: new[] { 0x46, 0xAF, 0x11A },
                resumeOffsets: new[] { 0x64, 0xCE, 0x136 });

            ValidateAsyncMethod(
                symReader,
                moveNextMethodToken: 0x06000008,
                kickoffMethodToken: 0x06000002,
                catchHandlerOffset: 0x76,
                yieldOffsets: new[] { 0x2D },
                resumeOffsets: new[] { 0x48 });
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void TestAsyncMethods_GetAsyncStepInfo(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Async.DllAndPdb(portable));

            ISymUnmanagedMethod method;
            Assert.Equal(HResult.S_OK, symReader.GetMethod(0x06000005, out method));

            var asyncMethod = (ISymUnmanagedAsyncMethod)method;

            var actualYieldOffsets = new int[1];
            var actualResumeOffsets = new int[1];
            var actualResumeMethods = new int[1];

            int count2;
            Assert.Equal(HResult.S_OK, asyncMethod.GetAsyncStepInfo(1, out count2, actualYieldOffsets, actualResumeOffsets, actualResumeMethods));

            Assert.Equal(1, count2);
            Assert.NotEqual(0, actualYieldOffsets[0]);
            Assert.NotEqual(0, actualResumeOffsets[0]);
            Assert.NotEqual(0, actualResumeMethods[0]);

            actualYieldOffsets = new int[5];
            actualResumeOffsets = new int[5];
            actualResumeMethods = new int[5];

            Assert.Equal(HResult.S_OK, asyncMethod.GetAsyncStepInfo(4, out count2, actualYieldOffsets, actualResumeOffsets, actualResumeMethods));

            Assert.Equal(3, count2);

            for (int i = 0; i < 3; i++)
            {
                Assert.NotEqual(0, actualYieldOffsets[i]);
                Assert.NotEqual(0, actualResumeOffsets[i]);
                Assert.NotEqual(0, actualResumeMethods[i]);
            }

            for (int i = 3; i < 5; i++)
            {
                Assert.Equal(0, actualYieldOffsets[i]);
                Assert.Equal(0, actualResumeOffsets[i]);
                Assert.Equal(0, actualResumeMethods[i]);
            }
        }

        [Fact]
        public void TestAsyncMethods_Errors()
        {
            var symReader = CreateSymReaderFromResource(TestResources.Scopes.PortableDllAndPdb);

            ISymUnmanagedMethod method;
            Assert.Equal(HResult.S_OK, symReader.GetMethod(0x06000002, out method));

            var asyncMethod = (ISymUnmanagedAsyncMethod)method;

            bool isAsync;
            Assert.Equal(HResult.S_OK, asyncMethod.IsAsyncMethod(out isAsync));
            Assert.False(isAsync);

            int actualKickoffMethodToken;
            Assert.Equal(HResult.E_UNEXPECTED, asyncMethod.GetKickoffMethod(out actualKickoffMethodToken));

            bool hasCatchHandlerILOffset;
            Assert.Equal(HResult.E_UNEXPECTED, asyncMethod.HasCatchHandlerILOffset(out hasCatchHandlerILOffset));

            int actualCatchHandlerOffset;
            Assert.Equal(HResult.E_UNEXPECTED, asyncMethod.GetCatchHandlerILOffset(out actualCatchHandlerOffset));

            int count, count2;
            Assert.Equal(HResult.E_UNEXPECTED, asyncMethod.GetAsyncStepInfoCount(out count));
            Assert.Equal(HResult.E_UNEXPECTED, asyncMethod.GetAsyncStepInfo(count, out count2, null, null, null));
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetMethodsInDocument_Errors(bool portable)
        {
            var symReader1 = CreateSymReaderFromResource(TestResources.MethodBoundaries.DllAndPdb(portable));
            var symReader2 = CreateSymReaderFromResource(TestResources.Scopes.DllAndPdb(portable));
            var document1 = symReader1.GetDocument("MethodBoundaries1.cs");
            var document2 = symReader2.GetDocument("Scopes.cs");

            Assert.Equal(HResult.E_INVALIDARG, symReader1.GetMethodsInDocument(null, 0, out _, null));

            if (portable)
            {
                // bug in symreader: AV
                Assert.Equal(HResult.E_INVALIDARG, symReader1.GetMethodsInDocument(document2, 0, out _, null));
                Assert.Equal(HResult.E_INVALIDARG, symReader1.GetMethodsInDocument(document1, 10, out _, null));
                Assert.Equal(HResult.E_INVALIDARG, symReader1.GetMethodsInDocument(document1, 10, out _, new ISymUnmanagedMethod[1]));
            }
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetMethodsInDocument(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.MethodBoundaries.DllAndPdb(portable));

            var document1 = symReader.GetDocument("MethodBoundaries1.cs");
            var document2 = symReader.GetDocument("MethodBoundaries2.cs");
            var document3 = symReader.GetDocument("MethodBoundaries3.cs");

            // bug in symreader:
            // CDiaWrapper::GetMethodTokens returns all methods whose containing class (compiland) belongs to specified document.

            var expected = portable ? new[] 
            {
                0x06000001,
                0x06000002,
                0x06000003,
            } : new[]
            {
                0x06000001,
                0x06000002,
                0x06000003,
                0x06000004,
                0x06000005,
                0x06000006,
                0x06000007,
                0x06000008,
                0x06000009,
                0x0600000A,
                0x0600000B,
                0x0600000C,
                0x0600000D,
                0x0600000E,
                0x0600000F,
                0x06000010,
            };

            AssertEx.Equal(expected, symReader.GetMethodsInDocument(document1).Select(m => m.GetToken()), itemInspector: i => $"0x{i:X8}");

            expected = portable ? new[]
            {
                0x06000002,
                0x06000004,
                0x06000005,
                0x06000006,
                0x06000007,
                0x06000008,
                0x06000009,
                0x0600000A,
                0x0600000B,
                0x0600000C,
            } : new[]
            {
                0x06000001,
                0x06000002,
                0x06000003,
                0x06000004,
                0x06000005,
                0x06000006,
                0x06000007,
                0x06000008,
                0x06000009,
                0x0600000A,
                0x0600000B,
                0x0600000C,
                0x0600000D,
                0x0600000E,
                0x0600000F,
                0x06000010,
            };

            AssertEx.Equal(expected, symReader.GetMethodsInDocument(document2).Select(m => m.GetToken()), itemInspector: i => $"0x{i:X8}");

            expected = portable ? new[]
            {
                0x0600000D,
                0x0600000E,
                0x0600000F,
                0x06000010
            } : new[]
            {
                0x06000001,
                0x06000002,
                0x06000003,
                0x06000004,
                0x06000005,
                0x06000006,
                0x06000007,
                0x06000008,
                0x06000009,
                0x0600000A,
                0x0600000B,
                0x0600000C,
                0x0600000D,
                0x0600000E,
                0x0600000F,
                0x06000010,
            };

            AssertEx.Equal(expected, symReader.GetMethodsInDocument(document3).Select(m => m.GetToken()), itemInspector: i => $"0x{i:X8}");
        }

        [Theory, ClassData(typeof(PdbTestData))]
        public void GetLocalSignatureToken(bool portable)
        {
            var symReader = CreateSymReaderFromResource(TestResources.Scopes.DllAndPdb(portable));

            //  X..ctor
            var mXCtor = (ISymUnmanagedMethod2)symReader.GetMethod(0x06000001);
            Assert.Null(mXCtor);

            //  C<S>.F<T>
            var mF = (ISymUnmanagedMethod2)symReader.GetMethod(0x06000002);
            Assert.Equal(0, mF.GetLocalSignatureToken());

            //  C<S>.NestedScopes
            var mNestedScopes = (ISymUnmanagedMethod2)symReader.GetMethod(0x06000003);
            Assert.Equal(portable ? 0x11000001 : 0x1100001B, mNestedScopes.GetLocalSignatureToken());

            //  C<S>.NestedScopesLocals
            var mNestedScopesLocals = (ISymUnmanagedMethod2)symReader.GetMethod(0x06000004);
            Assert.Equal(portable ? 0x11000002 : 0x1100001C, mNestedScopesLocals.GetLocalSignatureToken());

            //  C<S>.NestedScopesLocals2
            var mNestedScopesLocals2 = (ISymUnmanagedMethod2)symReader.GetMethod(0x06000005);
            Assert.Equal(portable ? 0x11000003 : 0x1100001D, mNestedScopesLocals2.GetLocalSignatureToken());

            //  C<S>..ctor
            var mCCtor = (ISymUnmanagedMethod2)symReader.GetMethod(0x06000006);
            Assert.Null(mCCtor);
        }
    }
}
