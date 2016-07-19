// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis.Collections;
using Roslyn.Utilities;

namespace Roslyn.Utilities
{
    public static class MetadataHelpers
    {
        internal static string GetAssemblyDisplayName(MetadataReader reader, AssemblyReference assemblyRef)
        {
            string nameStr = reader.GetString(assemblyRef.Name);
            string cultureName = assemblyRef.Culture.IsNil ? "" : reader.GetString(assemblyRef.Culture);
            ImmutableArray<byte> publicKeyOrToken = reader.GetBlobContent(assemblyRef.PublicKeyOrToken);
            bool hasPublicKey = (assemblyRef.Flags & AssemblyFlags.PublicKey) != 0;

            if (publicKeyOrToken.IsEmpty)
            {
                publicKeyOrToken = default(ImmutableArray<byte>);
            }

            return BuildDisplayName(
                name: nameStr,
                version: assemblyRef.Version,
                cultureName: cultureName,
                publicKeyOrToken: publicKeyOrToken,
                hasPublicKey: hasPublicKey,
                isRetargetable: (assemblyRef.Flags & AssemblyFlags.Retargetable) != 0,
                contentType: (AssemblyContentType)((int)(assemblyRef.Flags & AssemblyFlags.ContentTypeMask) >> 9));
        }

        internal const string InvariantCultureDisplay = "neutral";
        internal const int PublicKeyTokenSize = 8;

        private static string BuildDisplayName(
            string name,
            Version version,
            string cultureName,
            ImmutableArray<byte> publicKeyOrToken,
            bool hasPublicKey,
            bool isRetargetable,
            AssemblyContentType contentType)
        {
            PooledStringBuilder pooledBuilder = PooledStringBuilder.GetInstance();
            var sb = pooledBuilder.Builder;
            EscapeName(sb, name);

            sb.Append(", Version=");
            sb.Append(version.Major);
            sb.Append(".");
            sb.Append(version.Minor);
            sb.Append(".");
            sb.Append(version.Build);
            sb.Append(".");
            sb.Append(version.Revision);

            sb.Append(", Culture=");
            if (cultureName.Length == 0)
            {
                sb.Append(InvariantCultureDisplay);
            }
            else
            {
                EscapeName(sb, cultureName);
            }

            if (hasPublicKey)
            {
                sb.Append(", PublicKey=");
                AppendKey(sb, publicKeyOrToken);
            }
            else
            {
                sb.Append(", PublicKeyToken=");
                if (publicKeyOrToken.Length > 0)
                {
                    AppendKey(sb, publicKeyOrToken);
                }
                else
                {
                    sb.Append("null");
                }
            }

            if (isRetargetable)
            {
                sb.Append(", Retargetable=Yes");
            }

            switch (contentType)
            {
                case AssemblyContentType.Default:
                    break;

                case AssemblyContentType.WindowsRuntime:
                    sb.Append(", ContentType=WindowsRuntime");
                    break;

                default:
                    throw ExceptionUtilities.UnexpectedValue(contentType);
            }

            string result = sb.ToString();
            pooledBuilder.Free();
            return result;
        }

        private static void AppendKey(StringBuilder sb, ImmutableArray<byte> key)
        {
            foreach (byte b in key)
            {
                sb.Append(b.ToString("x2"));
            }
        }

        private static void EscapeName(StringBuilder result, string name)
        {
            bool quoted = false;
            if (IsWhiteSpace(name[0]) || IsWhiteSpace(name[name.Length - 1]))
            {
                result.Append('"');
                quoted = true;
            }

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                switch (c)
                {
                    case ',':
                    case '=':
                    case '\\':
                    case '"':
                    case '\'':
                        result.Append('\\');
                        result.Append(c);
                        break;

                    case '\t':
                        result.Append("\\t");
                        break;

                    case '\r':
                        result.Append("\\r");
                        break;

                    case '\n':
                        result.Append("\\n");
                        break;

                    default:
                        result.Append(c);
                        break;
                }
            }

            if (quoted)
            {
                result.Append('"');
            }
        }

        private static bool IsWhiteSpace(char c)
        {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }

        public static SignatureTypeCode GetConstantTypeCode(object value)
        {
            if (value == null)
            {
                return (SignatureTypeCode)SignatureTypeKind.Class;
            }

            Debug.Assert(!value.GetType().GetTypeInfo().IsEnum);

            // Perf: Note that JIT optimizes each expression val.GetType() == typeof(T) to a single register comparison.
            // Also the checks are sorted by commonality of the checked types.

            if (value.GetType() == typeof(int))
            {
                return SignatureTypeCode.Int32;
            }

            if (value.GetType() == typeof(string))
            {
                return SignatureTypeCode.String;
            }

            if (value.GetType() == typeof(bool))
            {
                return SignatureTypeCode.Boolean;
            }

            if (value.GetType() == typeof(char))
            {
                return SignatureTypeCode.Char;
            }

            if (value.GetType() == typeof(byte))
            {
                return SignatureTypeCode.Byte;
            }

            if (value.GetType() == typeof(long))
            {
                return SignatureTypeCode.Int64;
            }

            if (value.GetType() == typeof(double))
            {
                return SignatureTypeCode.Double;
            }

            if (value.GetType() == typeof(short))
            {
                return SignatureTypeCode.Int16;
            }

            if (value.GetType() == typeof(ushort))
            {
                return SignatureTypeCode.UInt16;
            }

            if (value.GetType() == typeof(uint))
            {
                return SignatureTypeCode.UInt32;
            }

            if (value.GetType() == typeof(sbyte))
            {
                return SignatureTypeCode.SByte;
            }

            if (value.GetType() == typeof(ulong))
            {
                return SignatureTypeCode.UInt64;
            }

            if (value.GetType() == typeof(float))
            {
                return SignatureTypeCode.Single;
            }

            throw ExceptionUtilities.Unreachable;
        }
    }
}
