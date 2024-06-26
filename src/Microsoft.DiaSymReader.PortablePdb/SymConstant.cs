﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

#if NET9_0_OR_GREATER
using System.Runtime.InteropServices.Marshalling;
#endif

namespace Microsoft.DiaSymReader.PortablePdb
{
    [ComVisible(false)]
#if NET9_0_OR_GREATER
    [GeneratedComClass]
#endif
    public sealed partial class SymConstant : ISymUnmanagedConstant
    {
        private readonly PortablePdbReader _pdbReader;
        private readonly LocalConstantHandle _handle;

        private object? _lazyValue = s_uninitialized;
        private byte[]? _lazySignature;

        private static readonly object s_nullReferenceValue = 0;
        private static readonly object s_uninitialized = new object();

        internal SymConstant(PortablePdbReader pdbReader, LocalConstantHandle handle)
        {
            Debug.Assert(pdbReader != null);
            _pdbReader = pdbReader;
            _handle = handle;
        }

        public int GetName(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]char[] name)
        {
            var mdReader = _pdbReader.MetadataReader;
            var constant = mdReader.GetLocalConstant(_handle);

            var str = mdReader.GetString(constant.Name);
            return InteropUtilities.StringToBuffer(str, bufferLength, out count, name);
        }

        public int GetSignature(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]byte[] signature)
        {
            if (_lazySignature == null)
            {
                InitializeValueAndSignature();
            }

            return InteropUtilities.BytesToBuffer(_lazySignature, bufferLength, out count, signature);
        }

        public int GetValue(out object? value)
        {
            if (_lazyValue == s_uninitialized)
            {
                InitializeValueAndSignature();
            }

            value = _lazyValue;
            return HResult.S_OK;
        }

        [MemberNotNull(nameof(_lazySignature))]
        private void InitializeValueAndSignature()
        {
            var mdReader = _pdbReader.MetadataReader;
            var constant = mdReader.GetLocalConstant(_handle);

            var sigReader = mdReader.GetBlobReader(constant.Signature);
            var sigWriter = new BlobWriter(sigReader.Length);

            // custom modifiers:
            int rawTypeCode;
            while (true)
            {
                rawTypeCode = sigReader.ReadCompressedInteger();
                if (rawTypeCode == (int)SignatureTypeCode.OptionalModifier || rawTypeCode == (int)SignatureTypeCode.RequiredModifier)
                {
                    sigReader.ReadCompressedInteger();
                }
                else
                {
                    break;
                }
            }

            int customModifiersLength = sigReader.Offset - 1;
            if (customModifiersLength > 0)
            {
                sigWriter.Write(mdReader.GetBlobBytes(constant.Signature), 0, customModifiersLength);
            }

            object? translatedValue;
            if (rawTypeCode == (int)MetadataUtilities.SignatureTypeCode_ValueType ||
                rawTypeCode == (int)MetadataUtilities.SignatureTypeCode_Class)
            {
                var typeHandle = sigReader.ReadTypeHandle();

                string? qualifiedName = _pdbReader.SymReader.GetMetadataImport().GetQualifiedTypeName(typeHandle);
                if (qualifiedName == "System.Decimal")
                {
                    translatedValue = sigReader.ReadDecimal();
                }
                else if (qualifiedName == "System.DateTime")
                {
                    translatedValue = BitConverter.Int64BitsToDouble(sigReader.ReadDateTime().Ticks);
                }
                else if (sigReader.RemainingBytes == 0)
                {
                    // null reference is returned as a boxed integer 0:
                    translatedValue = s_nullReferenceValue;
                }
                else
                {
                    // unknown (not produced by C# or VB)
                    translatedValue = null;
                }

                sigWriter.Write((byte)rawTypeCode);
                sigWriter.WriteCompressedInteger(MetadataUtilities.GetTypeDefOrRefOrSpecCodedIndex(typeHandle));
            }
            else
            {
                bool isEnumTypeCode;
                translatedValue = ReadAndTranslateValue(ref sigReader, (SignatureTypeCode)rawTypeCode, out isEnumTypeCode);

                if (sigReader.RemainingBytes == 0)
                {
                    // primitive type code:
                    sigWriter.Write((byte)rawTypeCode);
                }
                else if (isEnumTypeCode)
                {
                    var enumTypeHandle = sigReader.ReadTypeHandle();

                    // enum type signature:
                    sigWriter.Write((byte)MetadataUtilities.SignatureTypeCode_ValueType);
                    sigWriter.WriteCompressedInteger(MetadataUtilities.GetTypeDefOrRefOrSpecCodedIndex(enumTypeHandle));
                }

                if (sigReader.RemainingBytes > 0)
                {
                    throw new BadImageFormatException();
                }
            }

            _lazyValue = translatedValue;
            _lazySignature = sigWriter.ToArray();
        }

        private object ReadAndTranslateValue(ref BlobReader sigReader, SignatureTypeCode typeCode, out bool isEnumTypeCode)
        {
            switch (typeCode)
            {
                case SignatureTypeCode.Boolean:
                    isEnumTypeCode = true;
                    return (short)(sigReader.ReadBoolean() ? 1 : 0);

                case SignatureTypeCode.Char:
                    isEnumTypeCode = true;
                    return (ushort)sigReader.ReadChar();

                case SignatureTypeCode.SByte:
                    isEnumTypeCode = true;
                    return (short)sigReader.ReadSByte();

                case SignatureTypeCode.Byte:
                    isEnumTypeCode = true;
                    return (short)sigReader.ReadByte();

                case SignatureTypeCode.Int16:
                    isEnumTypeCode = true;
                    return sigReader.ReadInt16();

                case SignatureTypeCode.UInt16:
                    isEnumTypeCode = true;
                    return sigReader.ReadUInt16();

                case SignatureTypeCode.Int32:
                    isEnumTypeCode = true;
                    return sigReader.ReadInt32();

                case SignatureTypeCode.UInt32:
                    isEnumTypeCode = true;
                    return sigReader.ReadUInt32();

                case SignatureTypeCode.Int64:
                    isEnumTypeCode = true;
                    return sigReader.ReadInt64();

                case SignatureTypeCode.UInt64:
                    isEnumTypeCode = true;
                    return sigReader.ReadUInt64();

                case SignatureTypeCode.Single:
                    isEnumTypeCode = false;
                    return sigReader.ReadSingle();

                case SignatureTypeCode.Double:
                    isEnumTypeCode = false;
                    return sigReader.ReadDouble();

                case SignatureTypeCode.String:
                    isEnumTypeCode = false;

                    if (sigReader.RemainingBytes == 1)
                    {
                        if (sigReader.ReadByte() != 0xff)
                        {
                            throw new BadImageFormatException();
                        }

                        return s_nullReferenceValue;
                    }

                    if (sigReader.RemainingBytes % 2 != 0)
                    {
                        throw new BadImageFormatException();
                    }

                    return sigReader.ReadUTF16(sigReader.RemainingBytes);

                case SignatureTypeCode.Object:
                    // null reference
                    isEnumTypeCode = false;
                    return s_nullReferenceValue;

                default:
                    throw new BadImageFormatException();
            }
        }
    }
}
