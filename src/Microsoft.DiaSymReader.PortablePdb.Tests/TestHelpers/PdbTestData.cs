// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DiaSymReader.PortablePdb.UnitTests
{
    public class PdbTestData : IEnumerable<object[]>
    {
        private static readonly List<object[]> _data;

        static PdbTestData()
        {
            _data = new List<object[]>();

            // always test Portable PDB:
            _data.Add(new object[] { true });

            if (Path.DirectorySeparatorChar == '\\')
            {
                // Test Windows PDBs only on Windows
                _data.Add(new object[] { false });
            }
        }

        public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
