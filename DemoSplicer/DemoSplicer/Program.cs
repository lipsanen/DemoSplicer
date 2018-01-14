using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DemoSplicer
{
	class Program
	{
		static bool ArgsValid(string[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Usage: DemoSplicer.exe <output file> <input file/directories> - e.g.");
				Console.WriteLine("DemoSplicer.exe test.dem test - produces a combined demo from all the demos in the test folder.");
				Console.WriteLine("If all filenames are numbers, number ordering is used.");
				Console.WriteLine("If not, alphabetic ordering is used.");
				return false;
			}


			foreach (var invalidChar in Path.GetInvalidPathChars())
			{
				for (int i = 0; i < args.Length; ++i)
				{
					if (args[i].Contains(invalidChar))
					{
						Console.WriteLine("Path {0} contains invalid character {1}", args[i], invalidChar);
						return false;
					}
				}
			}

			return true;
		}

		static void Main(string[] args)
		{
			
			if (!ArgsValid(args))
				return;

			Splicer.DoDemoWriting(args);
			//Test.TestBitWriting();
		}
	}
}
