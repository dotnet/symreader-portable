// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DiaSymReader.Tools
{
    internal static class Pdb2Pdb
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: Pdb2Pdb <PE file> [/src:<PDB path>] [/dst:<Portable PDB path>]");
                return 1;
            }

            string peFile = args[0];
            var srcPdb = GetArgumentValue(args, "/src:") ?? Path.ChangeExtension(peFile, "pdb");
            var dstPdb = GetArgumentValue(args, "/dst:") ?? Path.ChangeExtension(peFile, "pdbx");

            if (!File.Exists(peFile))
            {
                Console.WriteLine($"PE file not: {peFile}");
                return 1;
            }
            
            if (!File.Exists(srcPdb))
            {
                Console.WriteLine($"PDB file not: {srcPdb}");
                return 1;
            }

            using (var peStream = new FileStream(peFile, FileMode.Open, FileAccess.Read))
            {
                using (var nativePdbStream = new FileStream(srcPdb, FileMode.Open, FileAccess.Read))
                {
                    using (var portablePdbStream = new FileStream(dstPdb, FileMode.Create, FileAccess.ReadWrite))
                    {
                        PdbConverter.Convert(peStream, nativePdbStream, portablePdbStream);
                    }
                }
            }

            return 0;
        }

        private static string GetArgumentValue(string[] args, string prefix)
        {
            return args.
                Where(arg => arg.StartsWith(prefix, StringComparison.Ordinal)).
                Select(arg => arg.Substring(prefix.Length)).LastOrDefault();
        }
    }
}
