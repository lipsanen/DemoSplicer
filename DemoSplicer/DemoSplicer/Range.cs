using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoSplicer
{
	public struct Range
	{
		public Range(int start, int end) : this()
		{
			Start = start;
			End = end;
		}

		/// <summary>
		/// End is included in the range.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public bool InRange(int index)
		{
			return Start <= index && index <= End;
		}

		public int Start { get; set; }
		public int End { get; set; }
	}
}
