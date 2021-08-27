// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal static class EnumerableHelpers
    {
        /// <summary>
        /// Groups specified entries by key optimizing for single-item groups. 
        /// The ordering of values within each bucket is the same as their ordering in the <paramref name="entries"/> sequence.
        /// </summary>
        public static Dictionary<K, (V Single, ImmutableArray<V> Multiple)> GroupBy<K, V>(this IEnumerable<KeyValuePair<K, V>> entries, IEqualityComparer<K> keyComparer)
        {
            var builder = new Dictionary<K, (V Single, ImmutableArray<V>.Builder? Multiple)>(keyComparer);

            foreach (var entry in entries)
            {
                if (!builder.TryGetValue(entry.Key, out var existing))
                {
                    builder[entry.Key] = (entry.Value, default(ImmutableArray<V>.Builder));
                }
                else if (existing.Multiple == null)
                {
                    var list = ImmutableArray.CreateBuilder<V>();
                    list.Add(existing.Single);
                    list.Add(entry.Value);
                    builder[entry.Key] = (default(V)!, list);
                }
                else
                {
                    existing.Multiple.Add(entry.Value);
                }
            }

            var result = new Dictionary<K, (V, ImmutableArray<V>)>(builder.Count, keyComparer);
            foreach (var entry in builder)
            {
                result.Add(entry.Key, (entry.Value.Single, entry.Value.Multiple?.ToImmutable() ?? default));
            }

            return result;
        }
    }
}
