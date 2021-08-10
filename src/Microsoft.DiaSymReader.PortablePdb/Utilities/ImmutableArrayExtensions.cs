﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal static class ImmutableArrayExtensions
    {
        internal static ImmutableArray<T> ToImmutableArrayOrEmpty<T>(this IEnumerable<T> items)
        {
            if (items == null)
            {
                return ImmutableArray.Create<T>();
            }

            return ImmutableArray.CreateRange<T>(items);
        }

        internal static ImmutableArray<T> ToImmutableArrayOrEmpty<T>(this ImmutableArray<T> items)
        {
            if (items.IsDefault)
            {
                return ImmutableArray.Create<T>();
            }

            return items;
        }

        // same as Array.BinarySearch but the ability to pass arbitrary value to the comparer without allocation
        internal static int BinarySearch<TElement, TValue>(this ImmutableArray<TElement> array, TValue value, Func<TElement, TValue, int> comparer)
        {
            int low = 0;
            int high = array.Length - 1;

            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = comparer(array[middle], value);

                if (comparison == 0)
                {
                    return middle;
                }

                if (comparison > 0)
                {
                    high = middle - 1;
                }
                else
                {
                    low = middle + 1;
                }
            }

            return ~low;
        }

        internal static void AddSubRange<T>(this ImmutableArray<T>.Builder builder, ImmutableArray<T> items, int start)
        {
            for (int i = start; i < items.Length; i++)
            {
                builder.Add(items[i]);
            }
        }
    }
}
