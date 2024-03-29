﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.DiaSymReader.PortablePdb.UnitTests
{
    public class EnumerableHelpersTests
    {
        [Fact]
        public void GroupBy1()
        {
            var pairs = new[]
            {
                KeyValuePair.Create("A", 1),
                KeyValuePair.Create("B", 2),
                KeyValuePair.Create("C", 3),
                KeyValuePair.Create("a", 4),
                KeyValuePair.Create("B", 5),
                KeyValuePair.Create("A", 6),
                KeyValuePair.Create("d", 7),
            };

            var groups = pairs.GroupBy(StringComparer.OrdinalIgnoreCase);
            AssertEx.SetEqual(new[] { "A", "B", "C", "d" }, groups.Keys);

            Assert.Equal(0, groups["A"].Single);
            AssertEx.Equal(new[] { 1, 4, 6 }, groups["A"].Multiple);

            Assert.Equal(0, groups["B"].Single);
            AssertEx.Equal(new[] { 2, 5 }, groups["B"].Multiple);

            Assert.Equal(3, groups["C"].Single);
            Assert.True(groups["C"].Multiple.IsDefault);

            Assert.Equal(7, groups["d"].Single);
            Assert.True(groups["d"].Multiple.IsDefault);
        }
    }
}
