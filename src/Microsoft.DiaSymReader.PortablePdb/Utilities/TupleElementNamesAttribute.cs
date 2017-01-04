// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Event)]
    internal sealed class TupleElementNamesAttribute : Attribute
    {
        public IList<string> TransformNames { get; }

        public TupleElementNamesAttribute(string[] transformNames)
        {
            TransformNames = transformNames;
        }
    }
}