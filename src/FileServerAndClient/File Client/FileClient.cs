using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileServerAndClient;

namespace File_Client
{
	class FileClient
	{
		private const bool DEBUG = false;

		[STAThread]
		static void Main(string[] args)
		{
			Console.Title = "File Client";
			
			Common.WriteTitle(false);

			// set default IP and port
			string IPAddress = "127.0.0.1";
			int port = 1234;

			TcpClient tcp = null;
			NetworkStream stream = null;

			string serverPath= null;
			string clientFilePath = null;

			while(true) {
				try {
					int option = Menu(IPAddress, port);
					
					if(option <= 2) {
						switch(option) {
							case 1: // Change IP Address
								ChangeIPAddress(ref IPAddress);
								break;
							case 2: // Change port
								ChangePort(ref port);
								break;
							case 0: // Exit
								return;
						}
						continue;
					}

					// check if any options have to be set ...
					switch(option) {
						case 3: // Download file
							if(!DownloadFileOptions(out serverPath, out clientFilePath)) {
								continue;
							}
							break;
						case 4: // Upload file
							if(!UploadFileOptions(out serverPath,out clientFilePath)) {
								continue;
							}
							break;
						case 5: // Delete file
							ManipulateFileOptions(out serverPath);
							break;
						case 6: // Create directory
						case 7: // Delete directory
						case 8: // List files and directories
							ManipulateDirectoryOptions(out serverPath);
							break;
					}

					// connect with server
					Console.Write("Connecting with server ... ");
					tcp = new TcpClient();
					tcp.Connect(IPAddress, port);
					Console.WriteLine("OK");
					
					stream = tcp.GetStream();

					// exchange keys for message encoding
					Console.Write("Exchanging keys ... ");
					string clientPrivateKey;
					string clientPublicKey;
					RSACryptoServiceProvider clientRSA = Cryptography.CreateKeys(out clientPrivateKey, out clientPublicKey);
					IO.Write(stream, clientPublicKey);
					string serverPublicKey = IO.Read(stream, clientRSA);
					var serverRSA = new RSACryptoServiceProvider();
					serverRSA.FromXmlString(serverPublicKey);
					Console.WriteLine("OK");

					switch(option) {
						case 3: // Download file
							DownloadFile(serverPath,clientFilePath, stream, clientRSA, serverRSA);
							break;
						case 4: // Upload file
							UploadFile(serverPath, clientFilePath, stream, clientRSA, serverRSA);
							break;
						case 5: // Delete file
							DeleteFile(serverPath, stream, clientRSA, serverRSA);
							break;
						case 6: // Create directory
							CreateDirectory(serverPath, stream, clientRSA, serverRSA);
							break;
						case 7: // Delete directory
							DeleteDirectory(serverPath, stream, clientRSA, serverRSA);
							break;
						case 8: // List files and directories
							ListFilesAndDirectories(serverPath, stream, clientRSA, serverRSA);
							break;
						case 9: // Get server time
							GetTime(stream, clientRSA, serverRSA);
							break;
					}
				} catch(Exception e) {
					Console.WriteLine("Error: " + e.Message);
					if(DEBUG) {
						Console.WriteLine(e.StackTrace);
						Console.WriteLine();
					}
				} finally {
					stream?.Dispose();
					tcp?.Dispose();
				}
			}
		}

		private static void DownloadFile(string serverFilePath, string clientFilePath, NetworkStream stream, RSACryptoServiceProvider clientRSA, RSACryptoServiceProvider serverRSA)
		{
			Console.Write("Sending " + Requests.DOWNLOAD_FILE + " request and file path ... ");
			IO.Write(stream, serverRSA, Requests.DOWNLOAD_FILE, serverFilePath);
			Console.WriteLine("OK");

			// the first response should be 8 bytes for file size
			byte[] responseBytes = IO.ReadBytes(stream, clientRSA);
			if(responseBytes.Length != 8) {
				// error?
				string serverResponse = FileServerAndClient.Convert.FromBytes(responseBytes);
				switch(serverResponse) {
					case Common.ERROR_INVALIDNAME:
						Console.WriteLine("The specified file path is invalid.");
						break;
					case Common.ERROR_NOTFOUND:
						Console.WriteLine("Server could not find the file. Make sure that the file exists on the server and that you typed the file path correctly.");
						break;
					case Common.ERROR_PATHTOOLONG:
						Console.WriteLine("The specified path is too long for the server. Use a shorter path or a shorter file name.");
						break;
					default:
						Console.WriteLine("Server returned an unexpected response.");
						break;
				}
				return;
			}
			long fileSize = BitConverter.ToInt64(responseBytes, 0);
			string fileSizeFormatted = FormatSize((int)fileSize);

			// first, pump all data to a temporary encrypted file
			// then, decrypt it into the actual file
			string clientEncryptedFilePath = clientFilePath + ".encrypted";

			Console.WriteLine("Downloading ...");
			int receiveSize = IO.BUFFERPACKAGESIZE;
			long bytesLeft = fileSize % IO.FILE_BUFFERSIZE;
			int leftReceiveSize = Cryptography.GetPieceCount((int)bytesLeft) * 128;
			int encryptedFileSize = ((int)fileSize / IO.FILE_BUFFERSIZE) * receiveSize;
			if(bytesLeft != 0) {
				encryptedFileSize += leftReceiveSize;
			}
			string encryptedFileSizeFormatted = FormatSize(encryptedFileSize);
			FileStream encryptedFileStream = null;
			try {
				encryptedFileStream = new FileStream(clientEncryptedFilePath, FileMode.Create, FileAccess.Write, FileShare.None, receiveSize, FileOptions.Encrypted);

				Stopwatch watch = Stopwatch.StartNew();
				int receivedSize = 0;
				string receivedSizeFormatted;
				byte[] receivedBytes;
				for(int i = encryptedFileSize / receiveSize; i > 0; --i) {
					receivedBytes = IO.ReadBytes(stream, receiveSize);
					encryptedFileStream.Write(receivedBytes, 0, receivedBytes.Length);
					receivedSize += receivedBytes.Length;

					if(watch.ElapsedMilliseconds >= 1000) {
						receivedSizeFormatted = FormatSize(receivedSize);
						Console.WriteLine(receivedSizeFormatted + " / " + encryptedFileSizeFormatted);
						watch.Restart();
					}
				}
				watch.Stop();
				if(bytesLeft != 0) {
					receivedBytes = IO.ReadBytes(stream, leftReceiveSize);
					encryptedFileStream.Write(receivedBytes, 0, receivedBytes.Length);
					receivedSize += receivedBytes.Length;
				}
				encryptedFileStream?.Dispose();
				receivedSizeFormatted = FormatSize(receivedSize);
				Console.WriteLine(receivedSizeFormatted + " / " + encryptedFileSizeFormatted);
			} catch {
				// has to be disposed here, otherwise the file is locked
				encryptedFileStream?.Dispose();
				if(File.Exists(clientEncryptedFilePath)) {
					File.Delete(clientEncryptedFilePath);
				}
				throw;
			}
			Console.WriteLine("Download finished.");

			Console.WriteLine("Decrypting ...");
			FileStream fileStream = null;
			try {
				encryptedFileStream = new FileStream(clientEncryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, receiveSize, FileOptions.SequentialScan | FileOptions.DeleteOnClose);
				fileStream = new FileStream(clientFilePath, FileMode.Create, FileAccess.Write, FileShare.None, IO.FILE_BUFFERSIZE);

				Stopwatch watch = Stopwatch.StartNew();
				int decryptedSize = 0;
				string decryptedSizeFormatted;
				byte[] encryptedBuffer = new byte[receiveSize];
				byte[] decryptedBytes;
				for(int i = encryptedFileSize / receiveSize; i > 0; --i) {
					encryptedFileStream.Read(encryptedBuffer, 0, encryptedBuffer.Length);
					decryptedBytes = Cryptography.Decrypt(encryptedBuffer, clientRSA);
					fileStream.Write(decryptedBytes, 0, decryptedBytes.Length);
					decryptedSize += decryptedBytes.Length;

					if(watch.ElapsedMilliseconds >= 1000) {
						decryptedSizeFormatted = FormatSize(decryptedSize);
						Console.WriteLine(decryptedSizeFormatted + " / " + fileSizeFormatted);
						watch.Restart();
					}
				}
				watch.Stop();
				if(bytesLeft != 0) {
					encryptedBuffer = new byte[leftReceiveSize];
					encryptedFileStream.Read(encryptedBuffer, 0, encryptedBuffer.Length);
					decryptedBytes = Cryptography.Decrypt(encryptedBuffer, clientRSA);
					fileStream.Write(decryptedBytes, 0, decryptedBytes.Length);
					decryptedSize += decryptedBytes.Length;
				}
				decryptedSizeFormatted = FormatSize(decryptedSize);
				Console.WriteLine(decryptedSizeFormatted + " / " + fileSizeFormatted);
			} finally {
				encryptedFileStream?.Dispose();
				fileStream?.Dispose();
				// the temporary file should be automatically deleted, but just to make sure ...
				if(File.Exists(clientEncryptedFilePath)) {
					File.Delete(clientEncryptedFilePath);
				}
			}
			Console.WriteLine("Decrypting finished.");
		}

		private static void UploadFile(string serverFilePath, string clientFilePath, NetworkStream stream, RSACryptoServiceProvider clientRSA, RSACryptoServiceProvider serverRSA)
		{
			using(var fileStream = new FileStream(clientFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, IO.FILE_BUFFERSIZE)) {
				long fileSize = fileStream.Length;
				byte[] fileSizeBytes = BitConverter.GetBytes(fileSize);
				string fileSizeText = FileServerAndClient.Convert.FromBytes(fileSizeBytes);
				string fileSizeFormatted = FormatSize((int)fileSize);

				Console.Write("Sending " + Requests.UPLOAD_FILE + " request and file data ... ");
				IO.Write(stream, serverRSA, Requests.UPLOAD_FILE, serverFilePath, fileSizeText);
				Console.WriteLine("OK");

				string serverResponse = IO.Read(stream, clientRSA);
				if(serverResponse != Common.CONFIRM) {
					switch(serverResponse) {
						case Common.ERROR_INVALIDNAME:
							Console.WriteLine("The specified file path is invalid.");
							break;
						case Common.ERROR_NOTFOUND:
							Console.WriteLine("Server could not find the directory. Make sure that the directory exists on the server and that you typed the path correctly.");
							break;
						case Common.ERROR_PATHTOOLONG:
							Console.WriteLine("The specified path is too long for the server. Use a shorter path or a shorter file name.");
							break;
						default:
							Console.WriteLine("Server returned an unexpected response.");
							break;
					}
					return;
				}

				Console.WriteLine("Uploading ...");
				Stopwatch watch = Stopwatch.StartNew();
				int sentSize = 0;
				string sentSizeFormatted;
				byte[] buffer = new byte[IO.FILE_BUFFERSIZE];
				for(long i = fileSize / IO.FILE_BUFFERSIZE; i > 0; --i) {
					fileStream.Read(buffer, 0, buffer.Length);
					IO.Write(stream, buffer, serverRSA, false);
					sentSize += IO.FILE_BUFFERSIZE;

					if(watch.ElapsedMilliseconds >= 1000) {
						sentSizeFormatted = FormatSize(sentSize);
						Console.WriteLine(sentSizeFormatted + " / " + fileSizeFormatted);
						watch.Restart();
					}
				}
				watch.Stop();
				long bytesLeft = fileSize % IO.FILE_BUFFERSIZE;
				if(bytesLeft != 0) {
					buffer = new byte[bytesLeft];
					fileStream.Read(buffer, 0, buffer.Length);
					IO.Write(stream, buffer, serverRSA, false);
					sentSize += (int)bytesLeft;
				}
				sentSizeFormatted = FormatSize(sentSize);
				Console.WriteLine(sentSizeFormatted + " / " + fileSizeFormatted);
			}

			Console.WriteLine("Uploading finished.");
		}

		private static void DeleteFile(string serverFilePath, NetworkStream stream, RSACryptoServiceProvider clientRSA, RSACryptoServiceProvider serverRSA)
		{
			Console.Write("Sending " + Requests.DELETE_FILE + " request and file path ... ");
			IO.Write(stream, serverRSA, Requests.DELETE_FILE, serverFilePath);
			Console.WriteLine("OK");

			string serverResponse = IO.Read(stream, clientRSA);
			switch(serverResponse) {
				case Common.CONFIRM:
					Console.WriteLine("File deleted.");
					break;
				case Common.ERROR_INVALIDNAME:
					Console.WriteLine("The specified file name is invalid.");
					break;
				case Common.ERROR_NOTFOUND:
					Console.WriteLine("Server could not find the file. Make sure that the file exists on the server and that you typed the file path correctly.");
					break;
				case Common.ERROR_PATHTOOLONG:
					Console.WriteLine("The specified path is too long.");
					break;
				default:
					Console.WriteLine("Server returned an unexpected response.");
					break;
			}
		}

		private static void CreateDirectory(string serverDirectoryPath, NetworkStream stream, RSACryptoServiceProvider clientRSA, RSACryptoServiceProvider serverRSA)
		{
			Console.Write("Sending " + Requests.CREATE_DIR + " request and directory path ... ");
			IO.Write(stream, serverRSA, Requests.CREATE_DIR, serverDirectoryPath);
			Console.WriteLine("OK");

			string serverResponse = IO.Read(stream, clientRSA);
			switch(serverResponse) {
				case Common.CONFIRM:
					Console.WriteLine("Directory created.");
					break;
				case Common.ERROR_DIRECTORYALREADYEXISTS:
					Console.WriteLine("A directory with the specified path already exists on the server.");
					break;
				case Common.ERROR_INVALIDNAME:
					Console.WriteLine("The specified directory name is invalid.");
					break;
				case Common.ERROR_NOTFOUND:
					Console.WriteLine("Server could not find the directory. Make sure that the directory exists on the server and that you typed the new directory path correctly.");
					break;
				case Common.ERROR_PATHTOOLONG:
					Console.WriteLine("The specified path is too long for the server. Use a shorter path or a shorter directory name.");
					break;
				default:
					Console.WriteLine("Server returned an unexpected response.");
					break;
			}
		}

		private static void DeleteDirectory(string serverDirectoryPath, NetworkStream stream, RSACryptoServiceProvider clientRSA, RSACryptoServiceProvider serverRSA)
		{
			Console.Write("Sending " + Requests.DELETE_DIR + " request and directory path ... ");
			IO.Write(stream, serverRSA, Requests.DELETE_DIR, serverDirectoryPath);
			Console.WriteLine("OK");

			string serverResponse = IO.Read(stream, clientRSA);
			switch(serverResponse) {
				case Common.CONFIRM:
					Console.WriteLine("Directory deleted.");
					break;
				case Common.ERROR_INVALIDNAME:
					Console.WriteLine("The specified directory name is invalid.");
					break;
				case Common.ERROR_NOTFOUND:
					Console.WriteLine("Server could not find the directory. Make sure that the directory exists on the server and that you typed the new directory path correctly.");
					break;
				case Common.ERROR_PATHTOOLONG:
					Console.WriteLine("The specified path is too long for the server. Use a shorter path or a shorter directory name.");
					break;
				default:
					Console.WriteLine("Server returned an unexpected response.");
					break;
			}
		}

		private static void ListFilesAndDirectories(string serverDirectoryPath, NetworkStream stream, RSACryptoServiceProvider clientRSA, RSACryptoServiceProvider serverRSA)
		{
			Console.Write("Sending " + Requests.LIST_FILESANDDIRS + " request and directory path ... ");
			IO.Write(stream, serverRSA, Requests.LIST_FILESANDDIRS, serverDirectoryPath);
			Console.WriteLine("OK");

			// the first response should be 8 bytes for the count of files and directories
			byte[] responseBytes = IO.ReadBytes(stream, clientRSA);
			if(responseBytes.Length != 8) {
				// error?
				string serverResponse = FileServerAndClient.Convert.FromBytes(responseBytes);
				switch(serverResponse) {
					case Common.ERROR_INVALIDNAME:
						Console.WriteLine("The specified directory path is invalid.");
						break;
					case Common.ERROR_NOTFOUND:
						Console.WriteLine("Server could not find the directory. Make sure that the directory exists on the server and that you typed the directory path correctly.");
						break;
					case Common.ERROR_PATHTOOLONG:
						Console.WriteLine("The specified path is too long for the server. Use a shorter path or a shorter directory name.");
						break;
					default:
						Console.WriteLine("Server returned an unexpected response.");
						break;
				}
				return;
			}
			long itemCount = BitConverter.ToInt64(responseBytes, 0);

			Console.WriteLine("Item count: " + itemCount);

			Console.Write("Receiving the list ... ");
			string[] filesAndDirs = new string[itemCount];
			for(long i = 0; i < itemCount; ++i) {
				filesAndDirs[i] = IO.Read(stream, clientRSA);
			}
			Console.WriteLine("OK");

			Console.WriteLine("Files and directories:");
			for(long i = 0; i < itemCount; ++i) {
				Console.WriteLine((i+1)+". "+filesAndDirs[i]);
			}
		}

		private static void GetTime(NetworkStream stream, RSACryptoServiceProvider clientRSA, RSACryptoServiceProvider serverRSA)
		{
			Console.Write("Sending " + Requests.GETTIME + " request ... ");
			IO.Write(stream, Requests.GETTIME, serverRSA);
			Console.WriteLine("OK");

			Console.Write("Receiving server time ... ");
			string time = IO.Read(stream, clientRSA);
			Console.WriteLine("OK");

			Console.WriteLine("Server time: " + time);
		}

		private static bool DownloadFileOptions(out string serverFilePath, out string clientFilePath)
		{
			ManipulateFileOptions(out serverFilePath);

			SaveFileDialog save = new SaveFileDialog();
			save.FileName = Path.GetFileName(serverFilePath);
			save.Title = "Choose where to save the file";
			if(save.ShowDialog() != DialogResult.OK) {
				save.Dispose();
				clientFilePath = null;
				return false;
			}
			clientFilePath = save.FileName;
			save.Dispose();
			Console.WriteLine("Download path: " + clientFilePath);
			return true;
		}

		private static bool UploadFileOptions(out string serverFilePath, out string clientFilePath)
		{
			OpenFileDialog open = new OpenFileDialog();
			open.Multiselect = false;
			open.Title = "Choose a file to upload";
			if(open.ShowDialog() != DialogResult.OK) {
				open.Dispose();
				clientFilePath = null;
				serverFilePath = null;
				return false;
			}
			clientFilePath = open.FileName;
			open.Dispose();
			Console.WriteLine("File to upload: " + clientFilePath);

			ManipulateFileOptions(out serverFilePath);
			return true;
		}

		private static void ManipulateFileOptions(out string serverFilePath)
		{
			Console.Write("File path on the server: ");
			serverFilePath = Console.ReadLine();
		}

		private static void ManipulateDirectoryOptions(out string serverDirectoryPath)
		{
			Console.Write("Directory path on the server: ");
			serverDirectoryPath = Console.ReadLine();
		}

		private static void ChangePort(ref int port)
		{
			Console.WriteLine("Current port: " + port);
			do {
				Console.Write("New port: ");
			} while(!int.TryParse(Console.ReadLine(), out port));
		}

		private static void ChangeIPAddress(ref string IPAddress)
		{
			Console.WriteLine("Current IP address: " + IPAddress);
			Console.Write("New IP address: ");
			IPAddress = Console.ReadLine();
		}

		private static string FormatSize(int size)
		{
			const double BtoMB = 1.0 / (1024 * 1024);
			double sizeMB = size * BtoMB;
			int wholePart = (int)sizeMB;
			int decimalPart = (int)(sizeMB * 100) - (int)sizeMB * 100;
			return wholePart + "." + (decimalPart >= 10 ? decimalPart.ToString() : "0" + decimalPart) + " MB";
		}

		private static int Menu(string IPAddress, int port)
		{
			Console.WriteLine();
			Console.BackgroundColor = ConsoleColor.DarkCyan;
			Console.Write("<---- Menu ---->");
			Console.ResetColor();
			Console.WriteLine();
			Console.WriteLine("   IP address: " + IPAddress);
			Console.WriteLine("   Port: " + port);
			Console.WriteLine("1) Change IP Address");
			Console.WriteLine("2) Change port");
			Console.WriteLine("3) Download file");
			Console.WriteLine("4) Upload file");
			Console.WriteLine("5) Delete file");
			Console.WriteLine("6) Create directory");
			Console.WriteLine("7) Delete directory");
			Console.WriteLine("8) List files and directories");
			Console.WriteLine("9) Get server time");
			Console.WriteLine("0) Exit");

			int option = -1;
			while(option < 0 || option > 9) {
				Console.Write("   Choose: ");
				try {
					option = int.Parse(Console.ReadLine());
				}catch {
					option = -1;
				}
			}
			Console.WriteLine();
			return option;
		}
	}
}
