// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.DiaSymReader.PortablePdb
{
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
    internal struct MethodLineExtent
    {
        internal sealed class MethodComparer : IComparer<MethodLineExtent>
        {
            public static readonly MethodComparer Instance = new MethodComparer();
            public int Compare(MethodLineExtent x, MethodLineExtent y) => x.Method.CompareTo(y.Method);
        }

        internal sealed class MinLineComparer : IComparer<MethodLineExtent>
        {
            public static readonly MinLineComparer Instance = new MinLineComparer();
            public int Compare(MethodLineExtent x, MethodLineExtent y) => x.MinLine - y.MinLine;
        }

        public readonly MethodId Method;
        public readonly int Version;
        public readonly int MinLine;
        public readonly int MaxLine;

        public MethodLineExtent(MethodId method, int version, int minLine, int maxLine)
        {
            Method = method;
            Version = version;
            MinLine = minLine;
            MaxLine = maxLine;
        }

        public static MethodLineExtent Merge(MethodLineExtent left, MethodLineExtent right)
        {
            Debug.Assert(left.Method == right.Method);
            Debug.Assert(left.Version == right.Version);
            return new MethodLineExtent(left.Method, left.Version, Math.Min(left.MinLine, right.MinLine), Math.Max(left.MaxLine, right.MaxLine));
        }

        public MethodLineExtent ApplyDelta(int delta) =>
            new MethodLineExtent(Method, Version, MinLine + delta, MaxLine + delta);

        private string GetDebuggerDisplay() =>
            $"{Method.Value} v{Version} [{MinLine}-{MaxLine}]";
    }
}
