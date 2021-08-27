// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal static class FileNameUtilities
    {
        private const string DirectorySeparatorStr = "\\";
        private const char DirectorySeparatorChar = '\\';
        private const char AltDirectorySeparatorChar = '/';
        private const char VolumeSeparatorChar = ':';

        /// <summary>
        /// Returns the position in given path where the file name starts.
        /// </summary>
        /// <returns>-1 if path is null.</returns>
        internal static int IndexOfFileName(string? path)
        {
            if (path == null)
            {
                return -1;
            }

            for (int i = path.Length - 1; i >= 0; i--)
            {
                char ch = path[i];
                if (ch == DirectorySeparatorChar || ch == AltDirectorySeparatorChar || ch == VolumeSeparatorChar)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        internal static bool IsDirectorySeparator(char separator)
        {
            return separator == DirectorySeparatorChar || separator == AltDirectorySeparatorChar;
        }

        /// <summary>
        /// Get file name from path.
        /// </summary>
        /// <remarks>Unlike <see cref="System.IO.Path.GetFileName"/> doesn't check for invalid path characters.</remarks>
        internal static string GetFileName(string path)
        {
            int fileNameStart = IndexOfFileName(path);
            return (fileNameStart <= 0) ? path : path.Substring(fileNameStart);
        }
    }
}
