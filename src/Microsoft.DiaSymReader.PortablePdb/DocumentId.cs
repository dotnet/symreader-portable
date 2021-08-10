// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.DiaSymReader.PortablePdb
{
    /// <summary>
    /// Unique ID representing a document across all generations.
    /// </summary>
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
    internal struct DocumentId : IEquatable<DocumentId>
    {
        // 1-based
        public readonly int Value;

        public DocumentId(int id)
        {
            Value = id;
        }

        public bool IsDefault => Value == 0;

        public bool Equals(DocumentId other) => Value == other.Value;
        public override int GetHashCode() => Value.GetHashCode();
        public override bool Equals(object obj) => obj is DocumentId id && Equals(id);

        private object GetDebuggerDisplay() => Value;
    }
}
