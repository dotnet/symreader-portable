// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal sealed class ChildScopeData : ScopeData
    {
        private readonly LocalScopeHandle _handle;
        private readonly ScopeData _parent;

        internal ChildScopeData(SymMethod symMethod, ScopeData parent, LocalScopeHandle handle)
            : base(symMethod)
        {
            Debug.Assert(parent != null);
            Debug.Assert(!handle.IsNil);

            _handle = handle;
            _parent = parent;
        }

        internal override ScopeData Parent => _parent;

        internal override int StartOffset
        {
            get
            {
                return SymMethod.MetadataReader.GetLocalScope(_handle).StartOffset;
            }
        }

        internal override int EndOffset
        {
            get
            {
                return AdjustEndOffset(SymMethod.MetadataReader.GetLocalScope(_handle).EndOffset);
            }
        }

        protected override ImmutableArray<ChildScopeData> CreateChildren()
        {
            var builder = ImmutableArray.CreateBuilder<ChildScopeData>();

            var children = SymMethod.MetadataReader.GetLocalScope(_handle).GetChildren();
            while (children.MoveNext())
            {
                builder.Add(new ChildScopeData(SymMethod, this, children.Current));
            }

            return builder.ToImmutable();
        }

        internal override int GetConstants(int bufferLength, out int count, ISymUnmanagedConstant[] constants)
        {
            var pdbReader = SymMethod.PdbReader;
            var scope = pdbReader.MetadataReader.GetLocalScope(_handle);

            var handles = scope.GetLocalConstants();

            int i = 0;
            foreach (var handle in handles)
            {
                if (i >= bufferLength)
                {
                    break;
                }

                constants[i++] = new SymConstant(pdbReader, handle);
            }

            count = (bufferLength == 0) ? handles.Count : i;
            return HResult.S_OK;
        }

        internal override int GetLocals(int bufferLength, out int count, ISymUnmanagedVariable[] locals)
        {
            var mdReader = SymMethod.MetadataReader;
            var scope = mdReader.GetLocalScope(_handle);

            var handles = scope.GetLocalVariables();

            int i = 0;
            foreach (var handle in handles)
            {
                if (i >= bufferLength)
                {
                    break;
                }

                locals[i++] = new SymVariable(SymMethod, handle);
            }

            count = (bufferLength == 0) ? handles.Count : i;
            return HResult.S_OK;
        }
    }
}
