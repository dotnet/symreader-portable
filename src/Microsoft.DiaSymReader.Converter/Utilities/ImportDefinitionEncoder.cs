// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Metadata
{
    // TODO: move to SRM

    internal struct ImportDefinitionEncoder
    {
        public MetadataBuilder MetadataBuilder { get; }
        public BlobBuilder Builder { get; }

        public ImportDefinitionEncoder(MetadataBuilder metadataBuilder, BlobBuilder builder)
        {
            MetadataBuilder = metadataBuilder;
            Builder = builder;
        }

        public void Type(EntityHandle type, string alias = null)
        {
            if (alias != null)
            {
                // <import> ::= AliasType <alias> <target-type>
                Builder.WriteByte((byte)ImportDefinitionKind.AliasType);
                Builder.WriteCompressedInteger(MetadataTokens.GetHeapOffset(MetadataBuilder.GetOrAddBlobUTF8(alias)));
            }
            else
            {
                // <import> ::= ImportType <target-type>
                Builder.WriteByte((byte)ImportDefinitionKind.ImportType);
            }

            Builder.WriteCompressedInteger(CodedIndex.TypeDefOrRefOrSpec(type));
        }

        public void Namespace(string namespaceName, string alias = null, AssemblyReferenceHandle externAlias = default(AssemblyReferenceHandle))
        {
            if (!externAlias.IsNil)
            {
                if (alias != null)
                {
                    // <import> ::= AliasAssemblyNamespace <alias> <target-assembly> <target-namespace>
                    Builder.WriteByte((byte)ImportDefinitionKind.AliasAssemblyNamespace);
                    Builder.WriteCompressedInteger(MetadataTokens.GetHeapOffset(MetadataBuilder.GetOrAddBlobUTF8(alias)));
                }
                else
                {
                    // <import> ::= ImportAssemblyNamespace <target-assembly> <target-namespace>
                    Builder.WriteByte((byte)ImportDefinitionKind.ImportAssemblyNamespace);
                }

                Builder.WriteCompressedInteger(MetadataTokens.GetRowNumber(externAlias));
            }
            else
            {
                if (alias != null)
                {
                    // <import> ::= AliasNamespace <alias> <target-namespace>
                    Builder.WriteByte((byte)ImportDefinitionKind.AliasNamespace);
                    Builder.WriteCompressedInteger(MetadataTokens.GetHeapOffset(MetadataBuilder.GetOrAddBlobUTF8(alias)));
                }
                else
                {
                    // <import> ::= ImportNamespace <target-namespace>
                    Builder.WriteByte((byte)ImportDefinitionKind.ImportNamespace);
                }
            }

            Builder.WriteCompressedInteger(MetadataTokens.GetHeapOffset(MetadataBuilder.GetOrAddBlobUTF8(namespaceName)));
        }

        public void XmlNamespace(string alias, string target)
        {
            // <import> ::= ImportXmlNamespace <alias> <target-namespace>
            Builder.WriteByte((byte)ImportDefinitionKind.ImportXmlNamespace);
            Builder.WriteCompressedInteger(MetadataTokens.GetHeapOffset(MetadataBuilder.GetOrAddBlobUTF8(alias)));
            Builder.WriteCompressedInteger(MetadataTokens.GetHeapOffset(MetadataBuilder.GetOrAddBlobUTF8(target)));
        }

        public void ImportAssemblyReferenceAlias(string alias)
        {
            // <import> ::= ImportAssemblyReferenceAlias <alias>
            Builder.WriteByte((byte)ImportDefinitionKind.ImportAssemblyReferenceAlias);
            Builder.WriteCompressedInteger(MetadataTokens.GetHeapOffset(MetadataBuilder.GetOrAddBlobUTF8(alias)));
        }

        public void AliasAssemblyReference(AssemblyReferenceHandle assemblyReference, string alias)
        {
            // <import> ::= AliasAssemblyReference <alias> <target-assembly>
            Builder.WriteByte((byte)ImportDefinitionKind.AliasAssemblyReference);
            Builder.WriteCompressedInteger(MetadataTokens.GetHeapOffset(MetadataBuilder.GetOrAddBlobUTF8(alias)));
            Builder.WriteCompressedInteger(MetadataTokens.GetRowNumber(assemblyReference));
        }
    }
}
