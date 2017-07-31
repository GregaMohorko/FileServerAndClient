using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileServerAndClient;

namespace File_Server
{
	class FileServer
	{
		private const bool DEBUG = false;

		static void Main(string[] args)
		{
			Console.Title = "File Server";

			TcpListener tcpListener = null;

			try {
				Common.WriteTitle(true);
				Console.WriteLine();

				int port = SelectPort();

				// start up the server and use the localhost address
				IPHostEntry localhost = Dns.GetHostEntry("localhost");
				IPAddress IPAddress = localhost.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
				tcpListener = new TcpListener(IPAddress, port);
				tcpListener.Start();

				string serverAddress = tcpListener.LocalEndpoint.ToString();

				WriteServerStarted(serverAddress);
				Console.Title = "File Server: " + serverAddress;
				Console.Beep();

				Console.CancelKeyPress += delegate
				{
					tcpListener?.Stop();
					Console.Beep();
				};

				// main server loop
				while(true) {
					Socket client = tcpListener.AcceptSocket();
					if(client.Connected) {
						Thread thread = new Thread(ListenClient);
						thread.Start(client);

						if(DEBUG) {
							Console.Beep();
						}
					}
				}
			} catch(Exception e) {
				Log("Error: " + e.ToString());
				if(DEBUG) {
					Console.WriteLine(e.GetType().Name);
					Console.WriteLine(e.StackTrace);
					Console.WriteLine();
				}
				tcpListener?.Stop();
				Console.Beep();
			}
		}

		private static void ListenClient(object parameter)
		{
			Socket client = (Socket)parameter;
			NetworkStream stream = null;
			string clientAddress = null;

			try {
				clientAddress = client.RemoteEndPoint.ToString();

				stream = new NetworkStream(client);

				// exchange keys for message encoding
				string serverPrivateKey;
				string serverPublicKey;
				RSACryptoServiceProvider serverRSA = Cryptography.CreateKeys(out serverPrivateKey, out serverPublicKey);
				string clientPublicKey = IO.Read(stream);
				var clientRSA = new RSACryptoServiceProvider();
				clientRSA.FromXmlString(clientPublicKey);
				IO.Write(stream, serverPublicKey, clientRSA);

				// read the request
				string request = IO.Read(stream, serverRSA);
				Log(clientAddress, "Connected: " + request);

				switch(request) {
					case Requests.DOWNLOAD_FILE:
						DownloadFile(clientAddress, stream, serverRSA, clientRSA);
						break;
					case Requests.UPLOAD_FILE:
						UploadFile(clientAddress, stream, serverRSA, clientRSA);
						break;
					case Requests.DELETE_FILE:
						DeleteFile(clientAddress, stream, serverRSA, clientRSA);
						break;
					case Requests.CREATE_DIR:
						CreateDirectory(clientAddress, stream, serverRSA, clientRSA);
						break;
					case Requests.DELETE_DIR:
						DeleteDirectory(clientAddress, stream, serverRSA, clientRSA);
						break;
					case Requests.LIST_FILESANDDIRS:
						ListFilesAndDirectories(clientAddress, stream, serverRSA, clientRSA);
						break;
					case Requests.GETTIME:
						GetTime(stream, clientRSA);
						break;
					default:
						throw new Exception("Invalid request.");
				}
			} catch(Exception e) {
				Log(clientAddress, "Error while communicating with the client: " + e.Message);
				if(DEBUG) {
					Console.WriteLine(e.GetType().Name);
					Console.WriteLine(e.StackTrace);
					Console.WriteLine();
				}
				Console.Beep();
			} finally {
				stream?.Dispose();
				client?.Dispose();
			}
		}

		private static void DownloadFile(string clientAddress, NetworkStream stream, RSACryptoServiceProvider serverRSA, RSACryptoServiceProvider clientRSA)
		{
			string serverFilePath = IO.Read(stream, serverRSA);

			FileStream fileStream = null;

			try {
				fileStream = new FileStream(serverFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, IO.FILE_BUFFERSIZE);

				// send file size
				long fileSize = fileStream.Length;
				IO.Write(stream, BitConverter.GetBytes(fileSize), clientRSA);

				// send file content
				byte[] buffer = new byte[IO.FILE_BUFFERSIZE];
				for(long i = fileSize / IO.FILE_BUFFERSIZE; i > 0; --i) {
					fileStream.Read(buffer, 0, buffer.Length);
					IO.Write(stream, buffer, clientRSA, false);
				}
				long bytesLeft = fileSize % IO.FILE_BUFFERSIZE;
				if(bytesLeft != 0) {
					buffer = new byte[bytesLeft];
					fileStream.Read(buffer, 0, buffer.Length);
					IO.Write(stream, buffer, clientRSA, false);
				}

				Log(clientAddress, "DOWNLOADED FILE \"" + serverFilePath + "\"");
			} catch(ArgumentException e) {
				IO.Write(stream, Common.ERROR_INVALIDNAME, clientRSA);
				Log(clientAddress, "Invalid download path (" + e.Message + "): " + serverFilePath);
			} catch(FileNotFoundException) {
				IO.Write(stream, Common.ERROR_NOTFOUND, clientRSA);
				Log(clientAddress, "Invalid download file (not found): " + serverFilePath);
			} catch(DirectoryNotFoundException) {
				IO.Write(stream, Common.ERROR_NOTFOUND, clientRSA);
				Log(clientAddress, "Invalid download directory (not found): " + serverFilePath);
			} catch(PathTooLongException) {
				IO.Write(stream, Common.ERROR_PATHTOOLONG);
				Log(clientAddress, "Invalid download path (too long): " + serverFilePath);
			} finally {
				fileStream?.Dispose();
			}
		}

		private static void UploadFile(string clientAddress, NetworkStream stream, RSACryptoServiceProvider serverRSA, RSACryptoServiceProvider clientRSA)
		{
			string serverFilePath = IO.Read(stream, serverRSA);
			byte[] fileSizeBytes = IO.ReadBytes(stream, serverRSA);
			long fileSize = BitConverter.ToInt64(fileSizeBytes, 0);

			FileStream fileStream = null;

			try {
				fileStream = new FileStream(serverFilePath, FileMode.Create, FileAccess.Write, FileShare.None, IO.FILE_BUFFERSIZE);

				// file stream was successfuly created, writing to it can now begin
				IO.Write(stream, Common.CONFIRM, clientRSA);

				// receive file content
				byte[] receivedBytes;
				for(long i = fileSize / IO.FILE_BUFFERSIZE; i > 0; --i) {
					receivedBytes = IO.ReadBytes(stream, IO.BUFFERPACKAGESIZE, serverRSA);
					fileStream.Write(receivedBytes, 0, receivedBytes.Length);
				}
				int bytesLeft = (int)fileSize % IO.FILE_BUFFERSIZE;
				if(bytesLeft != 0) {
					bytesLeft = Cryptography.GetPieceCount(bytesLeft) * 128;
					receivedBytes = IO.ReadBytes(stream, bytesLeft, serverRSA);
					fileStream.Write(receivedBytes, 0, receivedBytes.Length);
				}

				Log(clientAddress, "UPLOADED FILE \"" + serverFilePath + "\"");
			} catch(ArgumentException e) {
				IO.Write(stream, Common.ERROR_INVALIDNAME, clientRSA);
				Log(clientAddress, "Invalid upload path (" + e.Message + "): " + serverFilePath);
			} catch(DirectoryNotFoundException) {
				IO.Write(stream, Common.ERROR_NOTFOUND, clientRSA);
				Log(clientAddress, "Invalid upload directory (not found): " + serverFilePath);
			} catch(PathTooLongException) {
				IO.Write(stream, Common.ERROR_PATHTOOLONG, clientRSA);
				Log(clientAddress, "Invalid upload path (too long): " + serverFilePath);
			} catch {
				if(File.Exists(serverFilePath)) {
					// has to be disposed, otherwise the file is locked and cannot be deleted
					fileStream?.Dispose();
					fileStream = null;
					File.Delete(serverFilePath);
				}
				throw;
			} finally {
				fileStream?.Dispose();
			}
		}

		private static void DeleteFile(string clientAddress, NetworkStream stream, RSACryptoServiceProvider serverRSA, RSACryptoServiceProvider clientRSA)
		{
			string serverFilePath = IO.Read(stream, serverRSA);

			try {
				if(!File.Exists(serverFilePath)) {
					throw new FileNotFoundException();
				}

				File.Delete(serverFilePath);
				IO.Write(stream, Common.CONFIRM, clientRSA);

				Log(clientAddress, "DELETED FILE: " + serverFilePath);
			} catch(ArgumentException e) {
				IO.Write(stream, Common.ERROR_INVALIDNAME, clientRSA);
				Log(clientAddress, "Invalid file deletion path (" + e.Message + "): " + serverFilePath);
			} catch(FileNotFoundException) {
				IO.Write(stream, Common.ERROR_NOTFOUND, clientRSA);
				Log(clientAddress, "Invalid file deletion path (file not found): " + serverFilePath);
			} catch(DirectoryNotFoundException) {
				IO.Write(stream, Common.ERROR_NOTFOUND, clientRSA);
				Log(clientAddress, "Invalid file deletion path (directory not found): " + serverFilePath);
			} catch(PathTooLongException) {
				IO.Write(stream, Common.ERROR_PATHTOOLONG, clientRSA);
				Log(clientAddress, "Invalid file deletion path (too long): " + serverFilePath);
			}
		}

		private static void CreateDirectory(string clientAddress, NetworkStream stream, RSACryptoServiceProvider serverRSA, RSACryptoServiceProvider clientRSA)
		{
			string serverDirectoryPath = IO.Read(stream, serverRSA);

			try {
				if(Directory.Exists(serverDirectoryPath)) {
					IO.Write(stream, Common.ERROR_DIRECTORYALREADYEXISTS, clientRSA);
					Log(clientAddress, "Invalid directory creation path (already exists): " + serverDirectoryPath);
					return;
				}

				Directory.CreateDirectory(serverDirectoryPath);
				IO.Write(stream, Common.CONFIRM, clientRSA);

				Log(clientAddress, "CREATED DIRECTORY: " + serverDirectoryPath);
			} catch(ArgumentException e) {
				IO.Write(stream, Common.ERROR_INVALIDNAME, clientRSA);
				Log(clientAddress, "Invalid directory creation path (" + e.Message + "): " + serverDirectoryPath);
			} catch(DirectoryNotFoundException) {
				IO.Write(stream, Common.ERROR_NOTFOUND, clientRSA);
				Log(clientAddress, "Invalid directory creation path (not found): " + serverDirectoryPath);
			} catch(PathTooLongException) {
				IO.Write(stream, Common.ERROR_PATHTOOLONG, clientRSA);
				Log(clientAddress, "Invalid directory creation path (too long): " + serverDirectoryPath);
			}
		}

		private static void DeleteDirectory(string clientAddress, NetworkStream stream, RSACryptoServiceProvider serverRSA, RSACryptoServiceProvider clientRSA)
		{
			string serverDirectoryPath = IO.Read(stream, serverRSA);

			try {
				if(serverDirectoryPath == "/" || serverDirectoryPath == "\\") {
					throw new ArgumentException("Specified directory path is not allowed.");
				}

				Directory.Delete(serverDirectoryPath, true);
				IO.Write(stream, Common.CONFIRM, clientRSA);

				Log(clientAddress, "DELETED DIRECTORY: " + serverDirectoryPath);
			} catch(ArgumentException e) {
				IO.Write(stream, Common.ERROR_INVALIDNAME, clientRSA);
				Log(clientAddress, "Invalid directory deletion path (" + e.Message + "): " + serverDirectoryPath);
			} catch(DirectoryNotFoundException) {
				IO.Write(stream, Common.ERROR_NOTFOUND, clientRSA);
				Log(clientAddress, "Invalid directory deletion path (not found): " + serverDirectoryPath);
			} catch(PathTooLongException) {
				IO.Write(stream, Common.ERROR_PATHTOOLONG, clientRSA);
				Log(clientAddress, "Invalid directory deletion path (too long): " + serverDirectoryPath);
			}
		}

		private static void ListFilesAndDirectories(string clientAddress, NetworkStream stream, RSACryptoServiceProvider serverRSA, RSACryptoServiceProvider clientRSA)
		{
			string serverDirectoryPath = IO.Read(stream, serverRSA);

			string[] filesAndDirs = null;
			try {
				if(serverDirectoryPath.Contains(':') || serverDirectoryPath.StartsWith("..")) {
					// examples of not allowed paths:
					// C:/
					// ../
					throw new ArgumentException("Path '" + serverDirectoryPath + "' is not allowed.");
				}

				bool isRoot = false;
				if(serverDirectoryPath == "/" || serverDirectoryPath == "\\") {
					serverDirectoryPath = Directory.GetCurrentDirectory();
					isRoot = true;
				}

				string[] dirs = Directory.GetDirectories(serverDirectoryPath);
				string[] files = Directory.GetFiles(serverDirectoryPath);

				// only send the names, no path
				filesAndDirs = dirs.Select(d => "DIR: " + Path.GetFileName(d))
					.Concat(files.Select(f => "FILE: " + Path.GetFileName(f)))
					.ToArray();

				if(isRoot) {
					serverDirectoryPath = "/";
				}
			} catch(ArgumentException e) {
				IO.Write(stream, Common.ERROR_INVALIDNAME, clientRSA);
				Log(clientAddress, "Invalid directory path for listing (" + e.Message + "): " + serverDirectoryPath);
			} catch(DirectoryNotFoundException) {
				IO.Write(stream, Common.ERROR_NOTFOUND, clientRSA);
				Log(clientAddress, "Invalid directory path for listing (not found): " + serverDirectoryPath);
			} catch(PathTooLongException) {
				IO.Write(stream, Common.ERROR_PATHTOOLONG, clientRSA);
				Log(clientAddress, "Invalid directory path for listing (too long): " + serverDirectoryPath);
			}
			if(filesAndDirs == null) {
				return;
			}

			// send the count of files and directories
			IO.Write(stream, BitConverter.GetBytes(filesAndDirs.LongLength), clientRSA);

			// send all the items
			foreach(string item in filesAndDirs) {
				IO.Write(stream, item, clientRSA);
			}

			Log(clientAddress, "SENT LIST FOR PATH: " + serverDirectoryPath);
		}

		private static void GetTime(NetworkStream stream, RSACryptoServiceProvider clientRSA)
		{
			string time = DateTime.Now.ToString("hh:mm:ss");
			IO.Write(stream, time, clientRSA);
		}

		private static int SelectPort()
		{
			Console.Write("Port of the server: ");
			try {
				string input = Console.ReadLine();
				return int.Parse(input);
			} catch(Exception e) {
				Console.WriteLine("Error: " + e.Message);
				Console.WriteLine();
				return SelectPort();
			}
		}

		private static void Log(string address, string message)
		{
			message = address + " " + message;
			Log(message);
		}

		private static void Log(string message)
		{
			string logMessage = ToLogMessage(message);
			Console.WriteLine(logMessage);
		}

		private static string ToLogMessage(string message)
		{
			return DateTime.Now.ToString("hh:mm:ss") + " " + message;
		}

		private static void WriteServerStarted(string address)
		{
			Console.WriteLine();
			Console.BackgroundColor = ConsoleColor.DarkCyan;
			Console.Write(ToLogMessage("Server started: " + address));
			Console.ResetColor();
			Console.WriteLine();
			Console.WriteLine("Press Ctrl+C or Ctrl+Break to exit.");
			Console.WriteLine();
		}
	}
}
