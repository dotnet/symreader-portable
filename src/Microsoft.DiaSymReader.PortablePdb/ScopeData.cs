﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal abstract class ScopeData
    {
        internal readonly SymMethod SymMethod;

        private ImmutableArray<ChildScopeData> _lazyChildren;

        internal ScopeData(SymMethod symMethod)
        {
            SymMethod = symMethod;
        }

        internal ImmutableArray<ChildScopeData> GetChildren()
        {
            if (_lazyChildren.IsDefault)
            {
                _lazyChildren = CreateChildren();
            }

            return _lazyChildren;
        }

        public int AdjustEndOffset(int value)
        {
            // Portable PDB uses edge-exclusive semantics like C#.
            // VB end offset is inclusive.
            return SymMethod.SymReader.VbSemantics.Value && Parent is not RootScopeData ? value - 1 : value;
        }

        protected abstract ImmutableArray<ChildScopeData> CreateChildren();

        internal abstract int StartOffset { get; }
        internal abstract int EndOffset { get; }
        internal abstract ScopeData? Parent { get; }
        internal abstract int GetConstants(int bufferLength, out int count, ISymUnmanagedConstant[]? constants);
        internal abstract int GetLocals(int bufferLength, out int count, ISymUnmanagedVariable[]? locals);
    }
}
