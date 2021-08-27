// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal static class InteropUtilities
    {
        public static int StringToBuffer(
            string str,
            int bufferLength,
            out int count,
            char[] buffer)
        {
            // include NUL terminator:
            count = str.Length + 1;

            if (buffer == null)
            {
                return HResult.S_OK;
            }

            if (count > bufferLength)
            {
                count = 0;
                return HResult.E_OUTOFMEMORY;
            }

            str.CopyTo(0, buffer, 0, str.Length);
            buffer[str.Length] = '\0';

            return HResult.S_OK;
        }

        public static int BytesToBuffer(
            byte[] bytes,
            int bufferLength,
            out int count,
            byte[] buffer)
        {
            count = bytes.Length;

            if (buffer == null)
            {
                return HResult.S_OK;
            }

            if (count > bufferLength)
            {
                count = 0;
                return HResult.E_OUTOFMEMORY;
            }

            Buffer.BlockCopy(bytes, 0, buffer, 0, count);
            return HResult.S_OK;
        }

        internal static void TransferOwnershipOrRelease(ref object? obj, object? newOwner)
        {
            if (newOwner != null)
            {
                if (obj != null && Marshal.IsComObject(obj))
                {
                    Marshal.ReleaseComObject(obj);
                }

                obj = null;
            }
        }
    }
}
