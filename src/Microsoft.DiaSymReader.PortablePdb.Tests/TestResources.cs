// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace TestResources
{
    public static class Documents
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(Documents) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(Documents) + ".pdbx");

        private static byte[] s_dll;
        public static byte[] Dll => ResourceLoader.GetOrCreateResource(ref s_dll, nameof(Documents) + ".dll");

        private static byte[] s_pdb;
        public static byte[] Pdb => ResourceLoader.GetOrCreateResource(ref s_pdb, nameof(Documents) + ".pdb");

        public static KeyValuePair<byte[], byte[]> PortableDllAndPdb => new KeyValuePair<byte[], byte[]>(PortableDll, PortablePdb);
        public static KeyValuePair<byte[], byte[]> DllAndPdb => new KeyValuePair<byte[], byte[]>(Dll, Pdb);
    }

    public static class Scopes
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(Scopes) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(Scopes) + ".pdbx");

        public static KeyValuePair<byte[], byte[]> PortableDllAndPdb => new KeyValuePair<byte[], byte[]>(PortableDll, PortablePdb);
    }

    public static class Async
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(Async) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(Async) + ".pdbx");

        private static byte[] s_dll;
        public static byte[] Dll => ResourceLoader.GetOrCreateResource(ref s_dll, nameof(Async) + ".dll");

        private static byte[] s_pdb;
        public static byte[] Pdb => ResourceLoader.GetOrCreateResource(ref s_pdb, nameof(Async) + ".pdb");

        public static KeyValuePair<byte[], byte[]> PortableDllAndPdb => new KeyValuePair<byte[], byte[]>(PortableDll, PortablePdb);
        public static KeyValuePair<byte[], byte[]> DllAndPdb => new KeyValuePair<byte[], byte[]>(Dll, Pdb);
    }

    public static class MethodBoundaries
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(MethodBoundaries) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(MethodBoundaries) + ".pdbx");

        private static byte[] s_dll;
        public static byte[] Dll => ResourceLoader.GetOrCreateResource(ref s_dll, nameof(MethodBoundaries) + ".dll");

        private static byte[] s_pdb;
        public static byte[] Pdb => ResourceLoader.GetOrCreateResource(ref s_pdb, nameof(MethodBoundaries) + ".pdb");

        public static KeyValuePair<byte[], byte[]> PortableDllAndPdb => new KeyValuePair<byte[], byte[]>(PortableDll, PortablePdb);
        public static KeyValuePair<byte[], byte[]> DllAndPdb => new KeyValuePair<byte[], byte[]>(Dll, Pdb);
    }

    public static class MiscEmbedded
    {
        private static byte[] s_dll;
        public static byte[] Dll => ResourceLoader.GetOrCreateResource(ref s_dll, nameof(MiscEmbedded) + ".dll");
    }

    public static class EmbeddedSource
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(EmbeddedSource) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(EmbeddedSource) + ".pdbx");

        private static byte[] s_dll;
        public static byte[] Dll => ResourceLoader.GetOrCreateResource(ref s_dll, nameof(EmbeddedSource) + ".dll");

        private static byte[] s_pdb;
        public static byte[] Pdb => ResourceLoader.GetOrCreateResource(ref s_pdb, nameof(EmbeddedSource) + ".pdb");

        private static byte[] s_cs;
        public static byte[] CS => ResourceLoader.GetOrCreateResource(ref s_cs, nameof(EmbeddedSource) + ".cs");

        private static byte[] s_csSmall;
        public static byte[] CSSmall => ResourceLoader.GetOrCreateResource(ref s_csSmall, nameof(EmbeddedSource) + "Small.cs");

        public static KeyValuePair<byte[], byte[]> PortableDllAndPdb => new KeyValuePair<byte[], byte[]>(PortableDll, PortablePdb);
        public static KeyValuePair<byte[], byte[]> DllAndPdb => new KeyValuePair<byte[], byte[]>(Dll, Pdb);
    }

    public static class SourceLink
    {
        private static byte[] s_portableDll;
        public static byte[] PortableDll => ResourceLoader.GetOrCreateResource(ref s_portableDll, nameof(SourceLink) + ".dllx");

        private static byte[] s_portablePdb;
        public static byte[] PortablePdb => ResourceLoader.GetOrCreateResource(ref s_portablePdb, nameof(SourceLink) + ".pdbx");

        private static byte[] s_EmbeddedDll;
        public static byte[] EmbeddedDll => ResourceLoader.GetOrCreateResource(ref s_EmbeddedDll, nameof(SourceLink) + ".Embedded.dll");

        private static byte[] s_json;
        public static byte[] Json => ResourceLoader.GetOrCreateResource(ref s_json, nameof(SourceLink) + ".json");

        public static KeyValuePair<byte[], byte[]> PortableDllAndPdb => new KeyValuePair<byte[], byte[]>(PortableDll, PortablePdb);
    }
}
