using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileServerAndClient
{
	public static class IO
	{
		/// <summary>
		/// 100 KB
		/// </summary>
		public const int FILE_BUFFERSIZE = 100 * 1024;

		/// <summary>
		/// The receive size is actually bigger than buffer because of the RSA encoding.
		/// </summary>
		public static readonly int BUFFERPACKAGESIZE;

		static IO()
		{
			BUFFERPACKAGESIZE=Cryptography.GetPieceCount(FILE_BUFFERSIZE) * 128;
		}

		public static string Read(NetworkStream stream, RSACryptoServiceProvider rsa = null)
		{
			byte[] bytes = ReadBytes(stream, rsa);
			return Convert.FromBytes(bytes);
		}

		public static byte[] ReadBytes(NetworkStream stream, RSACryptoServiceProvider rsa = null)
		{
			byte[] headBytes = new byte[4];
			stream.Read(headBytes, 0, 4);
			int messageLength = BitConverter.ToInt32(headBytes, 0);
			byte[] messageBytes = new byte[messageLength];
			stream.Read(messageBytes, 0, messageBytes.Length);
			if(rsa != null) {
				messageBytes = Cryptography.Decrypt(messageBytes, rsa);
			}
			return messageBytes;
		}

		public static byte[] ReadBytes(NetworkStream stream, int size, RSACryptoServiceProvider rsa=null)
		{
			byte[] bytes = new byte[size];
			stream.Read(bytes, 0, bytes.Length);
			if(rsa != null) {
				bytes = Cryptography.Decrypt(bytes, rsa);
			}
			return bytes;
		}

		public static void Write(NetworkStream stream, string message, RSACryptoServiceProvider rsa = null)
		{
			byte[] messageBytes = Convert.ToBytes(message);
			Write(stream, messageBytes, rsa);
		}

		public static void Write(NetworkStream stream, RSACryptoServiceProvider rsa=null, params string[] messages)
		{
			var messagesBytes = messages.Select(m => Convert.ToBytes(m));
			Write(stream, messagesBytes, rsa);
		}

		public static void Write(NetworkStream stream, IEnumerable<byte[]> messagesBytes, RSACryptoServiceProvider rsa=null)
		{
			if(rsa != null) {
				messagesBytes = messagesBytes.Select(b => Cryptography.Encrypt(b, rsa));
			}
			// packages length = byte count + message count * head (4B)
			byte[] packages = new byte[messagesBytes.Sum(b => b.Length)+messagesBytes.Count()*4];
			byte[] head;
			int i = 0;
			foreach(byte[] messageBytes in messagesBytes) {
				head = CreateHead(messageBytes);
				Array.Copy(head, 0, packages, i, 4);
				i += 4;
				Array.Copy(messageBytes, 0, packages, i, messageBytes.Length);
				i += messageBytes.Length;
			}
			stream.Write(packages, 0, packages.Length);
		}

		public static void Write(NetworkStream stream, byte[] messageBytes, RSACryptoServiceProvider rsa = null,bool includeHead=true)
		{
			if(rsa != null) {
				messageBytes = Cryptography.Encrypt(messageBytes, rsa);
			}
			byte[] package;
			if(includeHead) {
				byte[] head = CreateHead(messageBytes);
				package = new byte[4 + messageBytes.Length];
				Array.Copy(head, package, 4);
				Array.Copy(messageBytes, 0, package, 4, messageBytes.Length);
			}else {
				package = messageBytes;
			}
			stream.Write(package, 0, package.Length);
		}

		private static byte[] CreateHead(byte[] message)
		{
			return BitConverter.GetBytes(message.Length);
		}
	}
}
