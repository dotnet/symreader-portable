#line 1 "C:\MiscEmbedded.cs"
#pragma checksum "C:\MiscEmbedded.cs" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "DBEB2A067B2F0E0D678A002C587A2806056C3DCE"

using System;
using System.IO;
using static System.Math; // unused

namespace N
{
    using System.Threading.Tasks;
    using IEB = System.Collections.Generic.IEnumerable<byte>;
    using TS = System.Threading.Tasks.Task<string>; // unused

	class C
	{
		public static void Main()
		{
			Console.WriteLine("hello world");
		}
		
		public static async Task<int> A()
		{
		   await Task.Delay(1);
		   return 2;
		}
		
		public static IEB I()
		{
		   yield return 1;
		   yield return 2;
		   yield return 3;
		}
		
		public static void Locals()
		{
			{
			   const dynamic x = null;
			}
	
			{
			   const C x = null;
			}
	
			{
			   object x = null;
			}
		}
	}
}