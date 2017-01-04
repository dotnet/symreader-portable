// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
