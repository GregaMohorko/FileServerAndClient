using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileServerAndClient
{
	public static class Requests
	{
		public const string DOWNLOAD_FILE = nameof(DOWNLOAD_FILE);
		public const string UPLOAD_FILE = nameof(UPLOAD_FILE);
		public const string DELETE_FILE = nameof(DELETE_FILE);
		public const string CREATE_DIR = nameof(CREATE_DIR);
		public const string DELETE_DIR = nameof(DELETE_DIR);
		public const string LIST_FILESANDDIRS = nameof(LIST_FILESANDDIRS);
		public const string GETTIME = nameof(GETTIME);
	}
}
