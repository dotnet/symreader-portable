// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Roslyn.Test.Utilities
{
    internal static class DiaSymReaderNativeRuntime
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr AddDllDirectory(string newDirectory);

        [ModuleInitializer]
        internal static void AddInstalledDotNetRuntimeDirectory()
        {
            string directory = GetInstalledDotNetRuntimeDirectory();
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Could not locate an installed .NET runtime containing Microsoft.DiaSymReader.Native.");
            }

            if (AddDllDirectory(directory) == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private static string GetInstalledDotNetRuntimeDirectory()
        {
            string root = Environment.GetEnvironmentVariable("DOTNET_ROOT")
                ?? Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR");

            if (string.IsNullOrEmpty(root))
            {
                string hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                if (!string.IsNullOrEmpty(hostPath))
                {
                    root = Path.GetDirectoryName(hostPath);
                }
            }

            if (string.IsNullOrEmpty(root))
            {
                string dotnetHost = FindDotNetHost();
                if (dotnetHost != null)
                {
                    root = Path.GetDirectoryName(dotnetHost);
                }
            }

            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
            }

            string shared = Path.Combine(root, "shared", "Microsoft.NETCore.App");
            if (!Directory.Exists(shared))
            {
                return null;
            }

            string dllName = IntPtr.Size == 4
                ? "Microsoft.DiaSymReader.Native.x86.dll"
                : RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "Microsoft.DiaSymReader.Native.arm64.dll"
                    : "Microsoft.DiaSymReader.Native.amd64.dll";

            return Directory.GetDirectories(shared)
                .Select(dir => (dir, version: TryParseVersion(Path.GetFileName(dir))))
                .Where(item => item.version != null && File.Exists(Path.Combine(item.dir, dllName)))
                .OrderByDescending(item => item.version)
                .Select(item => item.dir)
                .FirstOrDefault();
        }

        private static Version TryParseVersion(string name)
        {
            string core = name.Split('-')[0];
            return Version.TryParse(core, out var version) ? version : null;
        }

        private static string FindDotNetHost()
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv == null)
            {
                return null;
            }

            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                string candidate = Path.Combine(dir.Trim(), "dotnet.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}

#endif
