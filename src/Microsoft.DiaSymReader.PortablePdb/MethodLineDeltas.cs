// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal struct MethodLineDeltas
    {
        /// <summary>
        /// Delta applied to all sequence points.
        /// </summary>
        private readonly int _delta;

        /// <summary>
        /// Per-sequence-point deltas.
        /// </summary>
        private readonly ImmutableArray<int> _deltas;

        public MethodLineDeltas(int delta, ImmutableArray<int> deltas)
        {
            Debug.Assert(!deltas.IsDefault);
            _deltas = deltas;
            _delta = delta;
        }

        public bool IsDefault => _deltas.IsDefault;

        public MethodLineDeltas Merge(MethodLineDeltas other)
        {
            int maxLength = Math.Max(_deltas.Length, other._deltas.Length);
            int minLength = Math.Min(_deltas.Length, other._deltas.Length);

            var builder = ImmutableArray.CreateBuilder<int>(maxLength);

            for (int i = 0; i < minLength; i++)
            {
                builder.Add(unchecked(_deltas[i] + other._deltas[i]));
            }

            builder.AddSubRange(_deltas, minLength);
            builder.AddSubRange(other._deltas, minLength);

            return new MethodLineDeltas(unchecked(_delta + other._delta), builder.MoveToImmutable());
        }

        public int GetDeltaForSequencePoint(int index)
            => unchecked(_delta + (!_deltas.IsDefault && index < _deltas.Length ? _deltas[index] : 0));
    }
}
