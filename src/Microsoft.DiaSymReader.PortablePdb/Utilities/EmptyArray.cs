// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal static class EmptyArray<T>
    {
        public static readonly T[] Instance = new T[0];
    }
}
