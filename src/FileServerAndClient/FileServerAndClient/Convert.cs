using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileServerAndClient
{
	public static class Convert
	{
		public static byte[] ToBytes(string text)
		{
			return Encoding.Unicode.GetBytes(text);
		}

		public static string FromBytes(byte[] bytes)
		{
			return Encoding.Unicode.GetString(bytes);
		}
	}
}
