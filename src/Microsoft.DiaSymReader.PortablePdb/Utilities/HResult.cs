// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal static class HResult
    {
        internal const int S_OK = 0;
        internal const int S_FALSE = 1;
        internal const int E_NOTIMPL = unchecked((int)0x80004001);
        internal const int E_FAIL = unchecked((int)0x80004005);
        internal const int E_INVALIDARG = unchecked((int)0x80070057);
        internal const int E_WIN32_NOT_ENOUGH_MEMORY = unchecked((int)0x80070008);
        internal const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
        internal const int E_UNEXPECTED = unchecked((int)0x8000FFFF);

        internal const int E_PDB_NOT_FOUND = unchecked((int)0x806D0005);
    }
}
