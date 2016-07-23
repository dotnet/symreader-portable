// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal static class PortableShim
    {
        private static class CoreNames
        {
            internal const string System_IO_FileSystem = "System.IO.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            internal const string System_IO_FileSystem_Primitives = "System.IO.FileSystem.Primitives, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            internal const string System_Runtime_Extensions = "System.Runtime.Extensions, Version=4.0.10.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
        }

        internal static class Environment
        {
            internal const string TypeName = "System.Environment";

            internal static readonly Type Type = ReflectionUtilities.GetTypeFromEither(
                contractName: $"{TypeName}, {CoreNames.System_Runtime_Extensions}",
                desktopName: TypeName);

            internal static Func<string, string> GetEnvironmentVariable = (Func<string, string>)Type
                .GetTypeInfo()
                .GetDeclaredMethod(nameof(GetEnvironmentVariable), paramTypes: new[] { typeof(string) })
                .CreateDelegate(typeof(Func<string, string>));
        }

        internal static class File
        {
            internal const string TypeName = "System.IO.File";

            internal static readonly Type Type = ReflectionUtilities.GetTypeFromEither(
                contractName: $"{TypeName}, {CoreNames.System_IO_FileSystem}",
                desktopName: TypeName);

            internal static readonly Func<string, bool> Exists = Type
                .GetTypeInfo()
                .GetDeclaredMethod(nameof(Exists), new[] { typeof(string) })
                .CreateDelegate<Func<string, bool>>();

            internal static readonly Func<string, byte[]> ReadAllBytes = Type
                .GetTypeInfo()
                .GetDeclaredMethod(nameof(ReadAllBytes), paramTypes: new[] { typeof(string) })
                .CreateDelegate<Func<string, byte[]>>();
        }

        internal static class FileMode
        {
            internal const string TypeName = "System.IO.FileMode";

            internal static readonly Type Type = ReflectionUtilities.GetTypeFromEither(
                contractName: $"{TypeName}, {CoreNames.System_IO_FileSystem_Primitives}",
                desktopName: TypeName);

            internal static readonly object Open = Enum.ToObject(Type, 3);
        }

        internal static class FileAccess
        {
            internal const string TypeName = "System.IO.FileAccess";

            internal static readonly Type Type = ReflectionUtilities.GetTypeFromEither(
                contractName: $"{TypeName}, {CoreNames.System_IO_FileSystem_Primitives}",
                desktopName: TypeName);

            internal static readonly object Read = Enum.ToObject(Type, 1);
        }

        internal static class FileShare
        {
            internal const string TypeName = "System.IO.FileShare";

            internal static readonly Type Type = ReflectionUtilities.GetTypeFromEither(
                contractName: $"{TypeName}, {CoreNames.System_IO_FileSystem_Primitives}",
                desktopName: TypeName);

            internal static readonly object ReadOrDelete = Enum.ToObject(Type, 1 | 4);
        }

        internal static class FileStream
        {
            internal const string TypeName = "System.IO.FileStream";

            internal static readonly Type Type = ReflectionUtilities.GetTypeFromEither(
                contractName: $"{TypeName}, {CoreNames.System_IO_FileSystem}",
                desktopName: TypeName);

            private static ConstructorInfo s_Ctor_String_FileMode_FileAccess_FileShare = Type
                .GetTypeInfo()
                .GetDeclaredConstructor(paramTypes: new[] { typeof(string), FileMode.Type, FileAccess.Type, FileShare.Type });

            internal static Stream CreateReadShareDelete(string path)
            {
                return s_Ctor_String_FileMode_FileAccess_FileShare.InvokeConstructor<Stream>(path, FileMode.Open, FileAccess.Read, FileShare.ReadOrDelete);
            }
        }

    }
}
