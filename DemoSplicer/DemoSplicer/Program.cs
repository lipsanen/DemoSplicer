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
		static List<ParsedDemo> ParseDemos(IEnumerable<string> orderedDemoFiles)
		{
			List<ParsedDemo> parsedDemos = new List<ParsedDemo>();
			foreach (var file in orderedDemoFiles)
			{
				using (var stream = File.Open(file, FileMode.Open))
				{
					ParsedDemo parser = new ParsedDemo(stream);
					parsedDemos.Add(parser);
				}
			}

			return parsedDemos;
		}

		static List<string> GetDemoFiles(string[] args)
		{
			List<string> files = new List<string>();

			for (int i = 1; i < args.Count(); ++i)
			{
				if (Directory.Exists(args[i]))
					files.AddRange(Directory.GetFiles(args[i]).Where(a => a.EndsWith(".dem")));
				else if (File.Exists(args[i]) && args[i].EndsWith(".dem"))
					files.Add(args[i]);
			}

			return files;
		}

		static IEnumerable<string> OrderDemos(List<string> files)
		{
			int temp;

			if (files.All(a => int.TryParse(Path.GetFileNameWithoutExtension(a), out temp)))
			{
				return files.OrderBy(a => int.Parse(Path.GetFileNameWithoutExtension(a)));
			}
			else
			{
				return files.OrderBy(a => a);
			}
		}

		static void CombineDemos(string path, List<ParsedDemo> parsedDemos)
		{
			try
			{
				Console.WriteLine("Combine demos? y/n");
				if (Console.Read() == 'y')
					ParsedDemo.CombineDemos(path, parsedDemos);
			}
			catch (Exception e)
			{
				Console.WriteLine("Combining demos failed.");
				Console.WriteLine(e.ToString());
			}
		}


		static void PrintDemoData(IEnumerable<string> demos, string path)
		{
			Console.WriteLine("Output path is {0}", path);
			int index = 0;
			foreach (var demo in demos)
				Console.WriteLine("Demo {0}: {1}", ++index, demo);
		}

		static void DoDemoWriting(string[] args)
		{
			List<string> files = GetDemoFiles(args);
			string path = args[0];
			IEnumerable<string> orderedDemoFiles = OrderDemos(files);
			List<ParsedDemo> parsedDemos = ParseDemos(orderedDemoFiles);

			PrintDemoData(orderedDemoFiles, path);
			CombineDemos(path, parsedDemos);
		}

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

			DoDemoWriting(args);
		}
	}
}
