// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal sealed class MethodMap
    {
        [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
        internal struct MethodInfo
        {
            public readonly MethodDebugInformationHandle Handle;
            public readonly int Version;

            public MethodInfo(MethodDebugInformationHandle handle, int version)
            {
                Handle = handle;
                Version = version;
            }

            private object GetDebuggerDisplay() => $"{MetadataTokens.GetToken(Handle):X8} v{Version}";
        }

        private List<MethodInfo>? _lazyInfos;
        private readonly int _baselineMethodCount;

        public MethodMap(PortablePdbReader pdbReader)
        {
            _baselineMethodCount = pdbReader.MetadataReader.MethodDebugInformation.Count;
        }

        internal void Update(SymReader symReader, MetadataReader reader, int version, out ImmutableArray<MethodId> handleToIdMap)
        {
            if (_lazyInfos == null)
            {
                _lazyInfos = new List<MethodInfo>();

                foreach (var handle in symReader.GetReader(version: 1).MetadataReader.MethodDebugInformation)
                {
                    _lazyInfos.Add(new MethodInfo(handle, version: 1));
                }
            }

            handleToIdMap = CreateHandleToIdMap(reader, _lazyInfos, version);
        }

        internal MethodInfo GetInfo(MethodId methodId)
        {
            int rowId = methodId.Value;

            if (_lazyInfos == null)
            {
                // Infos are initialized by an EnC update. If not initialized the symreader hasn't been updated.
                Debug.Assert(rowId <= _baselineMethodCount);
                return new MethodInfo(MetadataTokens.MethodDebugInformationHandle(rowId), version: 1);
            }

            return _lazyInfos[rowId - 1];
        }

        internal bool IsValidMethodId(MethodId id) => IsValidMethodRowId(id.Value);

        internal bool IsValidMethodRowId(int rowId) => 
            rowId <= _baselineMethodCount || _lazyInfos != null && rowId <= _lazyInfos.Count;

        internal bool IsValidMethodToken(int token) => 
            MetadataUtilities.IsMethodToken(token) && IsValidMethodRowId(MetadataUtilities.GetRowId(token));

        private static ImmutableArray<MethodId> CreateHandleToIdMap(MetadataReader reader, List<MethodInfo> infos, int version)
        {
            var handleToIdMapBuilder = ImmutableArray.CreateBuilder<MethodId>(reader.MethodDebugInformation.Count);

            // Consider: Ideally MetadataReader would expose GetEditAndContinueMapEntries(TableIndex) and do binary search.
            // However, the only records in the table for Portable PDBs are currently MethodDebugInformation handles.
            foreach (EntityHandle encMapHandle in reader.GetEditAndContinueMapEntries())
            {
                if (encMapHandle.Kind == HandleKind.MethodDebugInformation)
                {
                    var id = new MethodId(MetadataTokens.GetRowNumber(encMapHandle));
                    handleToIdMapBuilder.Add(id);
                    var methodHandle = MetadataTokens.MethodDebugInformationHandle(handleToIdMapBuilder.Count);

                    int index = id.Value - 1;
                    while (infos.Count <= index)
                    {
                        infos.Add(default);
                    }

                    // an existing info is overwritten with new version:
                    infos[index] = new MethodInfo(methodHandle, version);
                }
            }

            return handleToIdMapBuilder.MoveToImmutable();
        }
    }
}
