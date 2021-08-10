// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

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
