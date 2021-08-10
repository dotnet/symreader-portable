// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.DiaSymReader.PortablePdb
{
    /// <summary>
    /// Unique ID representing a method across all generations.
    /// </summary>
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
    internal struct MethodId : IEquatable<MethodId>, IComparable<MethodId>
    {
        // 1-based
        public readonly int Value;

        public MethodId(int id)
        {
            Value = id;
        }

        public int Token => MetadataUtilities.MethodDefToken(Value);
        public static MethodId FromToken(int methodToken) => new MethodId(MetadataUtilities.GetRowId(methodToken));

        public bool IsDefault => Value == 0;

        public bool Equals(MethodId other) => Value == other.Value;
        public override int GetHashCode() => Value.GetHashCode();
        public override bool Equals(object obj) => obj is MethodId id && Equals(id);

        public int CompareTo(MethodId other) => Value.CompareTo(other.Value);

        public static bool operator ==(MethodId left, MethodId right) => left.Equals(right);
        public static bool operator !=(MethodId left, MethodId right) => !left.Equals(right);
        public static bool operator <(MethodId left, MethodId right) => left.Value < right.Value;
        public static bool operator >(MethodId left, MethodId right) => left.Value > right.Value;
        public static bool operator <=(MethodId left, MethodId right) => left.Value <= right.Value;
        public static bool operator >=(MethodId left, MethodId right) => left.Value >= right.Value;

        private object GetDebuggerDisplay() => Value;
    }
}
