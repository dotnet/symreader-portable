// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal static class Debug
    {
        [Conditional("DEBUG")]
        public static void Assert([DoesNotReturnIf(false)] bool b)
            => System.Diagnostics.Debug.Assert(b);

        /// <inheritdoc cref="Debug.Assert(bool, string)"/>
        [Conditional("DEBUG")]
        public static void Assert([DoesNotReturnIf(false)] bool b, string message)
            => System.Diagnostics.Debug.Assert(b, message);
    }
}

