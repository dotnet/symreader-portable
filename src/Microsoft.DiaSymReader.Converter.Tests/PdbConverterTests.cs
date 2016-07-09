// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using Xunit;

namespace Microsoft.DiaSymReader.Tools.UnitTests
{
    public class PdbConverterTests
    {
        [Fact]
        public void Convert()
        {
            Assert.Throws<ArgumentNullException>(() => PdbConverter.Convert(null, new MemoryStream(), new MemoryStream()));
            Assert.Throws<ArgumentNullException>(() => PdbConverter.Convert(new MemoryStream(), null,  new MemoryStream()));
            Assert.Throws<ArgumentNullException>(() => PdbConverter.Convert(new MemoryStream(), new MemoryStream(), null));
        }
    }
}
