using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoSplicer
{
	public static class Splicer
	{
		/// <summary>
		/// Parses the demos.
		/// </summary>
		/// <param name="orderedDemoFiles"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Gets the demo files from the commandline arguments provided.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Orders demos either based on alphabetic ordering or numeric ordering.
		/// </summary>
		/// <param name="files"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Asks the user if they want to combine the demos.
		/// </summary>
		/// <param name="path"></param>
		/// <param name="parsedDemos"></param>
		static void PromptCombineDemoFiles(string path, List<ParsedDemo> parsedDemos)
		{
			try
			{
				Console.WriteLine("Combine demos? y/n");
				if (Console.Read() == 'y')
					CombineDemos(path, parsedDemos);
			}
			catch (Exception e)
			{
				Console.WriteLine("Combining demos failed.");
				Console.WriteLine(e.ToString());
			}
		}

		/// <summary>
		/// Prints information about the demos prior to starting to write them to file.
		/// </summary>
		/// <param name="demos"></param>
		/// <param name="path"></param>
		static void PrintDemoData(IEnumerable<string> demos, string path)
		{
			Console.WriteLine("Output path is {0}", path);
			int index = 0;
			foreach (var demo in demos)
				Console.WriteLine("Demo {0}: {1}", ++index, demo);
		}

		public static void DoDemoWriting(string[] args)
		{
			List<string> files = GetDemoFiles(args);
			string path = args[0];
			IEnumerable<string> orderedDemoFiles = OrderDemos(files);
			List<ParsedDemo> parsedDemos = ParseDemos(orderedDemoFiles);

			PrintDemoData(orderedDemoFiles, path);
			PromptCombineDemoFiles(path, parsedDemos);
		}

		static List<DemoWriteInfo> GetWriteInfo(List<ParsedDemo> files)
		{
			List<DeltaPacket> lastFile = null;
			List<DeltaPacket> currentFile = null;
			List<DemoWriteInfo> writeInfos = new List<DemoWriteInfo>();

			for (int i = 0; i < files.Count; ++i)
			{
				var info = new DemoWriteInfo();
				lastFile = currentFile;
				currentFile = files[i].Info.FindDeltaPacketInfo();

				if (i == 0)
				{
					info.SetFirstDemo();
				}
				else
				{
					int lastIndex = writeInfos.Count - 1;
					bool adjacentTickFound = false;

					// Check for adjacent ticks on consecutive demos on the same map
					if (files[i - 1].Info.MapName == files[i].Info.MapName)
						// Start from the beginning of the latter demo to find as early of a tick to sync on as possible
						for (int u = 0; u < currentFile.Count && !adjacentTickFound; ++u)
						{
							for (int v = lastFile.Count - 1; v >= 0; --v)
							{
								if (lastFile[v].GlobalTick == currentFile[u].GlobalTick - 1)
								{
									adjacentTickFound = true;
									writeInfos[lastIndex].LastTick = lastFile[v].Tick;
									info.StartTick = currentFile[u].Tick;
									break;
								}
							}
						}

					if (!adjacentTickFound)
					{
						// Let the previous demo run until the end if no tick is found
						writeInfos[lastIndex].LastTick = int.MaxValue;

						// If on the same map, start from the first delta tick
						if (files[i].Info.MapName == files[i - 1].Info.MapName && currentFile.Count > 1)
						{
							info.StartTick = currentFile[0].Tick;
						}
						// If not on the same map, include all the baseline shit, string tables, etc.
						else
						{
							info.StartTick = int.MinValue;
						}
					}

				}

				// If last demo, include all the ticks
				if (i == files.Count - 1)
					info.LastTick = int.MaxValue;

				writeInfos.Add(info);
			}

			return writeInfos;
		}

		static void CombineDemos(string path, List<ParsedDemo> files)
		{
			var writeInfo = GetWriteInfo(files);
			using (var stream = File.Open(path, FileMode.Create))
			{
				int runningTick = -9999;

				for (int i = 0; i < files.Count; ++i)
				{
					Console.WriteLine("Writing file {0}", i);
					bool lastDemo = i == files.Count - 1;
					bool lastDemoMap;
					bool firstMapDemo = i == 0 || files[i - 1].Info.MapName != files[i].Info.MapName;

					if (!lastDemo)
						lastDemoMap = files[i].Info.MapName != files[i + 1].Info.MapName;
					else
						lastDemoMap = true;

					runningTick = files[i].WriteToFile(stream, writeInfo[i].StartTick, writeInfo[i].LastTick, writeInfo[i].WriteHeader, runningTick, lastDemo, firstMapDemo, lastDemoMap);
				}
			}
		}
	}
}
