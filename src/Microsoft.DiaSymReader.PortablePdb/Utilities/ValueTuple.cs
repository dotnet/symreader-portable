// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Numerics.Hashing;
using System.Runtime.InteropServices;

namespace System
{
    internal struct ValueTuple
    {
        public static ValueTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2) =>
           new ValueTuple<T1, T2>(item1, item2);
    }

    [StructLayout(LayoutKind.Auto)]
    internal struct ValueTuple<T1, T2> : IEquatable<ValueTuple<T1, T2>>
    {
        public T1 Item1;
        public T2 Item2;

        public ValueTuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public override bool Equals(object obj) => 
            obj is ValueTuple<T1, T2> && Equals((ValueTuple<T1, T2>)obj);

        public bool Equals(ValueTuple<T1, T2> other) => 
            EqualityComparer<T1>.Default.Equals(Item1, other.Item1) && 
            EqualityComparer<T2>.Default.Equals(Item2, other.Item2);

        internal static int CombineHashCodes(int h1, int h2) => 
            HashHelpers.Combine(HashHelpers.Combine(HashHelpers.RandomSeed, h1), h2);

        public override int GetHashCode() => 
            CombineHashCodes(Item1?.GetHashCode() ?? 0, Item2?.GetHashCode() ?? 0);

        private int GetHashCodeCore(IEqualityComparer comparer) => 
            CombineHashCodes(comparer.GetHashCode(Item1), comparer.GetHashCode(Item2));

        public override string ToString() => 
            "(" + Item1?.ToString() + ", " + Item2?.ToString() + ")";
    }
}