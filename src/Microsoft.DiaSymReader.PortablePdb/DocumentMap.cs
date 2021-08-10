// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal sealed class DocumentMap
    {
        [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
        internal struct DocumentInfo
        {
            public readonly DocumentHandle Handle;
            public readonly int Version;
            public readonly string FileName;

            public DocumentInfo(DocumentHandle handle, int version, string fileName)
            {
                Handle = handle;
                Version = version;
                FileName = fileName;
            }

            private object GetDebuggerDisplay() => $"'{FileName}' {MetadataTokens.GetToken(Handle):X8} v{Version}";
        }

        private readonly MetadataReader _reader;

        // { last part of document name -> one or many document ids that have the name part in common }
        private readonly Dictionary<string, (DocumentId Single, ImmutableArray<DocumentId> Multiple)> _map;

        /// <summary>
        /// Info for each document, indexed by <see cref="DocumentId.Value"/> - 1. 
        /// </summary>
        private readonly List<DocumentInfo> _infos;

        public DocumentMap(MetadataReader reader)
        {
            _reader = reader;
            _infos = CreateBaselineDocumentInfos(reader);

            // group ignoring case, we will match the case when comparing within the group:
            _map = _infos.Select(info => KeyValuePair.Create(info.FileName, new DocumentId(MetadataTokens.GetRowNumber(info.Handle)))).
                GroupBy(StringComparer.OrdinalIgnoreCase);
        }

        public int DocumentCount => _infos.Count;
        public IReadOnlyList<DocumentInfo> Infos => _infos;

        private int GetInfoIndex(DocumentId documentId) => documentId.Value - 1;
        internal DocumentInfo GetInfo(DocumentId documentId) => _infos[GetInfoIndex(documentId)];

        private static bool DocumentFullPathEquals(MetadataReader reader, DocumentHandle handle, string fullPath, bool ignoreCase) =>
            reader.StringComparer.Equals(reader.GetDocument(handle).Name, fullPath, ignoreCase);

        private static List<DocumentInfo> CreateBaselineDocumentInfos(MetadataReader reader)
        {
            var result = new List<DocumentInfo>(reader.Documents.Count);

            foreach (var documentHandle in reader.Documents)
            {
                string fileName = GetFileName(reader, documentHandle);

                // invalid metadata: document doesn't have a name
                if (fileName == null)
                {
                    continue;
                }

                result.Add(new DocumentInfo(documentHandle, 1, fileName));
            }

            return result;
        }

        internal void Update(SymReader symReader, MetadataReader reader, int version, out ImmutableArray<DocumentId> handleToIdMap)
        {
            // add new documents, replace existing documents with exactly the same path with their new versions
            var handleToIdMapBuilder = ImmutableArray.CreateBuilder<DocumentId>(reader.Documents.Count);

            foreach (var documentHandle in reader.Documents)
            {
                string fileName = GetFileName(reader, documentHandle);

                // invalid metadata: document doesn't have a name
                if (fileName == null)
                {
                    handleToIdMapBuilder.Add(default(DocumentId));
                    continue;
                }

                var documentId = default(DocumentId);
                var info = new DocumentInfo(documentHandle, version, fileName);

                if (!_map.TryGetValue(fileName, out var existing))
                {
                    _infos.Add(info);
                    documentId = new DocumentId(_infos.Count);
                    _map.Add(fileName, (documentId, default(ImmutableArray<DocumentId>)));
                }
                else
                {
                    var document = reader.GetDocument(documentHandle);
                    string fullName = reader.GetString(document.Name);

                    if (existing.Multiple.IsDefault)
                    {
                        var infoIndex = GetInfoIndex(existing.Single);
                        var otherInfo = _infos[infoIndex];
                        var otherReader = (otherInfo.Version <= symReader.Version) ? symReader.GetReader(otherInfo.Version).MetadataReader : reader;

                        if (DocumentFullPathEquals(otherReader, otherInfo.Handle, fullName, ignoreCase: false))
                        {
                            _infos[infoIndex] = info;
                            documentId = existing.Single;
                        }
                        else
                        {
                            _infos.Add(info);
                            documentId = new DocumentId(_infos.Count);
                            _map[fileName] = (default(DocumentId), ImmutableArray.Create(existing.Single, documentId));
                        }
                    }
                    else
                    {
                        bool found = false;
                        foreach (var id in existing.Multiple)
                        {
                            var infoIndex = GetInfoIndex(id);
                            var otherInfo = _infos[infoIndex];
                            var otherReader = (otherInfo.Version <= symReader.Version) ? symReader.GetReader(otherInfo.Version).MetadataReader : reader;

                            if (DocumentFullPathEquals(otherReader, otherInfo.Handle, fullName, ignoreCase: false))
                            {
                                _infos[infoIndex] = info;
                                documentId = id;
                                break;
                            }
                        }

                        if (!found)
                        {
                            _infos.Add(info);
                            documentId = new DocumentId(_infos.Count);
                            _map[fileName] = (default(DocumentId), existing.Multiple.Add(documentId));
                        }
                    }
                }

                handleToIdMapBuilder.Add(documentId);
            }

            handleToIdMap = handleToIdMapBuilder.MoveToImmutable();
        }

        private static string GetFileName(MetadataReader reader, DocumentHandle documentHandle)
        {
            var document = reader.GetDocument(documentHandle);

            if (document.Name.IsNil)
            {
                return null;
            }

            var nameReader = reader.GetBlobReader(document.Name);

            int separator = nameReader.ReadByte();
            if (!FileNameUtilities.IsDirectorySeparator((char)separator))
            {
                return FileNameUtilities.GetFileName(reader.GetString(document.Name));
            }

            // find the last part handle:
            BlobHandle partHandle = default(BlobHandle);
            while (nameReader.RemainingBytes > 0)
            {
                partHandle = nameReader.ReadBlobHandle();
            }

            if (partHandle.IsNil)
            {
                return string.Empty;
            }

            var partReader = reader.GetBlobReader(partHandle);
            var part = partReader.ReadUTF8(partReader.Length);
            if (part.IndexOf('\0') >= 0)
            {
                // bad metadata
                return null;
            }

            // it is valid to encode document name so that the parts contain directory separators:
            return FileNameUtilities.GetFileName(part);
        }

        internal bool TryGetDocument(string fullPath, out DocumentId id)
        {
            var fileName = FileNameUtilities.GetFileName(fullPath);

            if (!_map.TryGetValue(fileName, out var documents))
            {
                id = default(DocumentId);
                return false;
            }

            // SymReader first attempts to find the document by the full path, then by file name with extension.

            if (documents.Multiple.IsDefault)
            {
                // There is only one document with the specified file name.
                // SymReader returns the document regardless of whether the path matches the name.
                id = documents.Single;
                return true;
            }

            Debug.Assert(documents.Multiple.Length > 1);

            // We have multiple candidates with the same file name. Find the one whose name matches the specified full path.
            // If none does return the first one. It will be the one with the smallest handle, due to the multi-map construction implementation.

            // First try to find candidate whose full name is exactly matching.
            foreach (DocumentId candidate in documents.Multiple)
            {
                if (DocumentFullPathEquals(_reader, GetInfo(candidate).Handle, fullPath, ignoreCase: false))
                {
                    id = candidate;
                    return true;
                }
            }

            // Then try to find candidate whose full name is matching ignoring case.
            foreach (DocumentId candidate in documents.Multiple)
            {
                if (DocumentFullPathEquals(_reader, GetInfo(candidate).Handle, fullPath, ignoreCase: true))
                {
                    id = candidate;
                    return true;
                }
            }

            // Then try to find candidate whose file name is matching exactly.
            foreach (DocumentId candidate in documents.Multiple)
            {
                if (GetInfo(candidate).FileName == fileName)
                {
                    id = candidate;
                    return true;
                }
            }

            id = documents.Multiple[0];
            return true;
        }
    }
}
