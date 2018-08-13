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
			if (args.Length < 1)
			{
				Console.WriteLine("Usage: DemoSplicer.exe <input file/directories> - e.g.");
				Console.WriteLine("DemoSplicer.exe test - produces a combined demo/calculates the time from all the demos in the test folder.");
				Console.WriteLine("The demos are ordered based on filename. The ordering is the same as the one used in explorer.");
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
			Console.WriteLine("Press any key(or Enter) to exit");
			Console.ReadKey();
		}
	}
}
