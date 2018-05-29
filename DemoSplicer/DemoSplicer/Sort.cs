using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace DemoSplicer
{
	[SuppressUnmanagedCodeSecurity]
	internal static class SafeNativeMethods
	{
		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
		public static extern int StrCmpLogicalW(string psz1, string psz2);
	}

	public sealed class NaturalStringComparer : IComparer<string>
	{
		public int Compare(string a, string b)
		{
			return SafeNativeMethods.StrCmpLogicalW(a, b);
		}
	}

}
