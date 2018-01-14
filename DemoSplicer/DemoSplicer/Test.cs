using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoSplicer
{
	public static class Test
	{
		public static void TestBitWriting()
		{
			try
			{
				Random rng = new Random();
				const int size = 1000;
				byte[] byteArray = new byte[size];

				for (int i = 0; i < size; ++i)
				{
					byteArray[i] = ((byte)rng.Next(0, 255));
				}

				int currentBit = 0;
				int totalBits = byteArray.Count() * 8;
				BitWriterDeluxe writer = new BitWriterDeluxe();

				while (currentBit < totalBits)
				{
					int addition = Math.Min(totalBits - currentBit, rng.Next(1, 100));
					int newIndex = currentBit + addition;
					writer.WriteRangeFromArray(byteArray, currentBit, newIndex);
					currentBit = newIndex;
				}

				var results = writer.Data;

				for (int i = 0; i < byteArray.Count(); ++i)
				{
					if (results[i] != byteArray[i])
						throw new Exception(string.Format("{0} = {1} on index {2}", results[i], byteArray[i], i));
				}

				Console.WriteLine("Copying succeeded.");
				Console.WriteLine("Size comparison {0} = {1}", byteArray.Count(), results.Count());
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
			Console.ReadLine();
		}

	}
}
