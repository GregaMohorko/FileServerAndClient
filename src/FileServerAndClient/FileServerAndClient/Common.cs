using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileServerAndClient
{
	public static class Common
	{
		public const string CONFIRM = nameof(CONFIRM);
		public const string ERROR_NOTFOUND = "NotFound";
		public const string ERROR_PATHTOOLONG = "PathTooLong";
		public const string ERROR_DIRECTORYALREADYEXISTS = "DirectoryAlreadyExists";
		public const string ERROR_INVALIDNAME = "IllegalName";

		public static void WriteTitle(bool server)
		{
			string serverClient = server ? "Server" : "Client";

			ConsoleColor bannerColor = ConsoleColor.DarkRed;
			ConsoleColor titleColor = ConsoleColor.DarkGreen;

			Console.BackgroundColor = bannerColor;
			Console.Write("//-------------------------------------\\\\");
			Console.ResetColor();
			Console.WriteLine();
			Console.BackgroundColor = bannerColor;
			Console.Write("      File server by Grega Mohorko       ");
			Console.ResetColor();
			Console.WriteLine();
			Console.BackgroundColor = bannerColor;
			Console.Write("\\\\-------------------------------------//");
			Console.ResetColor();
			Console.WriteLine();
			Console.Write("      ");
			Console.BackgroundColor = titleColor;
			Console.Write(" /-------------------------\\ ");
			Console.ResetColor();
			Console.WriteLine();
			Console.Write("      ");
			Console.BackgroundColor = titleColor;
			Console.Write("<          "+ serverClient + "           >");
			Console.ResetColor();
			Console.WriteLine();
			Console.Write("      ");
			Console.BackgroundColor = titleColor;
			Console.Write(" \\-------------------------/ ");
			Console.ResetColor();
			Console.WriteLine();
			Console.WriteLine();
		}
	}
}
