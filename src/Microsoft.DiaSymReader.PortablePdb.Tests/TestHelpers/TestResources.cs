// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.DiaSymReader.PortablePdb.UnitTests;

namespace TestResources
{
    public static class Documents
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(Documents) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(Documents) + ".pdbx");

        private static byte[] s_windowsDll;
        public static byte[] WindowsDll => ResourceLoader.GetOrCreateResource(ref s_windowsDll, nameof(Documents) + ".dll");

        private static byte[] s_windowsPdb;
        public static byte[] WindowsPdb => ResourceLoader.GetOrCreateResource(ref s_windowsPdb, nameof(Documents) + ".pdb");

        public static TestResource PortableDllAndPdb => new TestResource(PortableDll, PortablePdb);
        public static TestResource WindowsDllAndPdb => new TestResource(WindowsDll, WindowsPdb);
        public static TestResource DllAndPdb(bool portable) => portable ? PortableDllAndPdb : WindowsDllAndPdb;
    }

    public static class Scopes
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(Scopes) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(Scopes) + ".pdbx");

        private static byte[] s_windowsDll;
        public static byte[] WindowsDll => ResourceLoader.GetOrCreateResource(ref s_windowsDll, nameof(Scopes) + ".dll");

        private static byte[] s_windowsPdb;
        public static byte[] WindowsPdb => ResourceLoader.GetOrCreateResource(ref s_windowsPdb, nameof(Scopes) + ".pdb");

        public static TestResource PortableDllAndPdb => new TestResource(PortableDll, PortablePdb);
        public static TestResource WindowsDllAndPdb => new TestResource(WindowsDll, WindowsPdb);
        public static TestResource DllAndPdb(bool portable) => portable ? PortableDllAndPdb : WindowsDllAndPdb;
    }

    public static class Async
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(Async) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(Async) + ".pdbx");

        private static byte[] s_windowsDll;
        public static byte[] WindowsDll => ResourceLoader.GetOrCreateResource(ref s_windowsDll, nameof(Async) + ".dll");

        private static byte[] s_windowsPdb;
        public static byte[] WindowsPdb => ResourceLoader.GetOrCreateResource(ref s_windowsPdb, nameof(Async) + ".pdb");

        public static TestResource PortableDllAndPdb => new TestResource(PortableDll, PortablePdb);
        public static TestResource WindowsDllAndPdb => new TestResource(WindowsDll, WindowsPdb);
        public static TestResource DllAndPdb(bool portable) => portable ? PortableDllAndPdb : WindowsDllAndPdb;
    }

    public static class MethodBoundaries
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(MethodBoundaries) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(MethodBoundaries) + ".pdbx");

        private static byte[] s_windowsDll;
        public static byte[] WindowsDll => ResourceLoader.GetOrCreateResource(ref s_windowsDll, nameof(MethodBoundaries) + ".dll");

        private static byte[] s_windowsPdb;
        public static byte[] WindowsPdb => ResourceLoader.GetOrCreateResource(ref s_windowsPdb, nameof(MethodBoundaries) + ".pdb");

        public static TestResource PortableDllAndPdb => new TestResource(PortableDll, PortablePdb);
        public static TestResource WindowsDllAndPdb => new TestResource(WindowsDll, WindowsPdb);
        public static TestResource DllAndPdb(bool portable) => portable ? PortableDllAndPdb : WindowsDllAndPdb;
    }

    public static class MiscEmbedded
    {
        private static byte[] s_Dll;
        public static byte[] Dll => ResourceLoader.GetOrCreateResource(ref s_Dll, nameof(MiscEmbedded) + ".dll");
    }

    public static class EmbeddedSource
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(EmbeddedSource) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(EmbeddedSource) + ".pdbx");

        private static byte[] s_windowsDll;
        public static byte[] WindowsDll => ResourceLoader.GetOrCreateResource(ref s_windowsDll, nameof(EmbeddedSource) + ".dll");

        private static byte[] s_windowsPdb;
        public static byte[] WindowsPdb => ResourceLoader.GetOrCreateResource(ref s_windowsPdb, nameof(EmbeddedSource) + ".pdb");

        private static byte[] s_cs;
        public static byte[] CS => ResourceLoader.GetOrCreateResource(ref s_cs, nameof(EmbeddedSource) + ".cs");

        private static byte[] s_csSmall;
        public static byte[] CSSmall => ResourceLoader.GetOrCreateResource(ref s_csSmall, nameof(EmbeddedSource) + "Small.cs");

        public static TestResource PortableDllAndPdb => new TestResource(PortableDll, PortablePdb);
        public static TestResource WindowsDllAndPdb => new TestResource(WindowsDll, WindowsPdb);
        public static TestResource DllAndPdb(bool portable) => portable ? PortableDllAndPdb : WindowsDllAndPdb;
    }

    public static class SourceLink
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(SourceLink) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(SourceLink) + ".pdbx");

        private static byte[] s_windowsDll;
        public static byte[] WindowsDll => ResourceLoader.GetOrCreateResource(ref s_windowsDll, nameof(SourceLink) + ".dll");

        private static byte[] s_windowsPdb;
        public static byte[] WindowsPdb => ResourceLoader.GetOrCreateResource(ref s_windowsPdb, nameof(SourceLink) + ".pdb");

        private static byte[] s_EmbeddedDll;
        public static byte[] EmbeddedDll => ResourceLoader.GetOrCreateResource(ref s_EmbeddedDll, nameof(SourceLink) + ".Embedded.dll");

        private static byte[] s_json;
        public static byte[] Json => ResourceLoader.GetOrCreateResource(ref s_json, nameof(SourceLink) + ".json");

        public static TestResource PortableDllAndPdb => new TestResource(PortableDll, PortablePdb);
        public static TestResource WindowsDllAndPdb => new TestResource(WindowsDll, WindowsPdb);
        public static TestResource DllAndPdb(bool portable) => portable ? PortableDllAndPdb : WindowsDllAndPdb;
    }

    public static class EncMethodExtents
    {
        private const int ImagesPerGeneration = 4;
        private const int GenerationCount = 3;

        private static byte[][] s_images = new byte[ImagesPerGeneration * GenerationCount][];

        public static TestResource Baseline(bool portable) => portable ? 
            new TestResource(
                ResourceLoader.GetOrCreateResource(ref s_images[0], nameof(EncMethodExtents) + ".dllx"),
                ResourceLoader.GetOrCreateResource(ref s_images[1], nameof(EncMethodExtents) + ".pdbx")) :
            new TestResource(
                ResourceLoader.GetOrCreateResource(ref s_images[2], nameof(EncMethodExtents) + ".dll"),
                ResourceLoader.GetOrCreateResource(ref s_images[3], nameof(EncMethodExtents) + ".pdb"));

        public static TestResource Diffs(int generation, bool portable) => portable ?
            new TestResource(
                ResourceLoader.GetOrCreateResource(ref s_images[generation * ImagesPerGeneration + 0], $"{nameof(EncMethodExtents)}.{generation}.metadatax"),
                ResourceLoader.GetOrCreateResource(ref s_images[generation * ImagesPerGeneration + 1], $"{nameof(EncMethodExtents)}.{generation}.pdbx")) :
            new TestResource(
                ResourceLoader.GetOrCreateResource(ref s_images[generation * ImagesPerGeneration + 2], $"{nameof(EncMethodExtents)}.{generation}.metadata"),
                ResourceLoader.GetOrCreateResource(ref s_images[generation * ImagesPerGeneration + 3], $"{nameof(EncMethodExtents)}.{generation}.pdb"));
    }
}
