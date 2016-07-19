// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Roslyn.Utilities;

namespace Microsoft.DiaSymReader.Tools
{
    internal sealed class ConstantSignatureConverter
    {
        public unsafe static void Convert(BlobBuilder builder, MetadataModel metadataModel, byte[] signature, object value)
        {
            fixed (byte* sigPtr = signature)
            {
                var sigReader = new BlobReader(sigPtr, signature.Length);

                // copy custom modifiers over:
                byte rawTypeCode;
                while (true)
                {
                    rawTypeCode = sigReader.ReadByte();
                    if (rawTypeCode != (int)SignatureTypeCode.OptionalModifier && rawTypeCode != (int)SignatureTypeCode.RequiredModifier)
                    {
                        break;
                    }

                    builder.WriteByte(rawTypeCode);
                    builder.WriteCompressedInteger(sigReader.ReadCompressedInteger());
                }

                switch ((SignatureTypeCode)rawTypeCode)
                {
                    case (SignatureTypeCode)SignatureTypeKind.Class:
                    case (SignatureTypeCode)SignatureTypeKind.ValueType:
                        int typeRefDefSpec = sigReader.ReadCompressedInteger();

                        if (value is decimal)
                        {
                            // GeneralConstant: VALUETYPE TypeDefOrRefOrSpecEncoded <decimal>
                            builder.WriteByte((byte)SignatureTypeKind.ValueType);
                            builder.WriteCompressedInteger(typeRefDefSpec);
                            builder.WriteDecimal((decimal)value);
                        }
                        else if (value is DateTime)
                        {
                            // GeneralConstant: VALUETYPE TypeDefOrRefOrSpecEncoded <date-time>
                            builder.WriteByte((byte)SignatureTypeKind.ValueType);
                            builder.WriteCompressedInteger(typeRefDefSpec);
                            builder.WriteDateTime((DateTime)value);
                        }
                        else if (value == null)
                        {
                            // GeneralConstant: CLASS TypeDefOrRefOrSpecEncoded
                            builder.WriteByte(rawTypeCode);
                            builder.WriteCompressedInteger(typeRefDefSpec);
                        }
                        else
                        {
                            // EnumConstant ::= EnumTypeCode EnumValue EnumType
                            // EnumTypeCode ::= BOOLEAN | CHAR | I1 | U1 | I2 | U2 | I4 | U4 | I8 | U8
                            // EnumType     ::= TypeDefOrRefOrSpecEncoded

                            var enumTypeCode = MetadataHelpers.GetConstantTypeCode(value);
                            builder.WriteByte((byte)enumTypeCode);
                            builder.WriteConstant(value);
                            builder.WriteCompressedInteger(typeRefDefSpec);
                        }

                        break;

                    case SignatureTypeCode.Object:
                        // null:
                        Debug.Assert(value == null);
                        builder.WriteByte((byte)SignatureTypeCode.Object);
                        break;

                    case SignatureTypeCode.Boolean:
                    case SignatureTypeCode.Char:
                    case SignatureTypeCode.SByte:
                    case SignatureTypeCode.Byte:
                    case SignatureTypeCode.Int16:
                    case SignatureTypeCode.UInt16:
                    case SignatureTypeCode.Int32:
                    case SignatureTypeCode.UInt32:
                    case SignatureTypeCode.Int64:
                    case SignatureTypeCode.UInt64:
                    case SignatureTypeCode.Single:
                    case SignatureTypeCode.Double:
                    case SignatureTypeCode.String:
                        // PrimitiveConstant
                        builder.WriteByte(rawTypeCode);
                        builder.WriteConstant(value);
                        break;

                    case SignatureTypeCode.SZArray:
                    case SignatureTypeCode.Array:
                    case SignatureTypeCode.GenericTypeInstance:
                        Debug.Assert(value == null);

                        // Find an existing TypeSpec in metadata.
                        // If there isn't one we can't represent the constant type in the Portable PDB, use Object.

                        // +1/-1 for the type code we already read.
                        var spec = new byte[sigReader.RemainingBytes + 1];
                        Buffer.BlockCopy(signature, sigReader.Offset - 1, spec, 0, spec.Length);

                        TypeSpecificationHandle typeSpec;
                        if (metadataModel.TryResolveTypeSpecification(spec, out typeSpec))
                        {
                            builder.WriteCompressedInteger(CodedIndex.TypeDefOrRefOrSpec(typeSpec));
                        }
                        else
                        {
                            // TODO: warning - can't translate const type
                            builder.WriteByte((byte)SignatureTypeCode.Object);
                        }

                        break;

                    case SignatureTypeCode.GenericMethodParameter:
                    case SignatureTypeCode.GenericTypeParameter:
                    case SignatureTypeCode.FunctionPointer:
                    case SignatureTypeCode.Pointer:
                        // generic parameters, pointers are not valid types for constants:
                        throw new BadImageFormatException();
                }
            }
        }
    }
}
