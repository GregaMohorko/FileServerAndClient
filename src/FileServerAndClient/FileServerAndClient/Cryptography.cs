using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FileServerAndClient
{
	public static class Cryptography
	{
		public static RSACryptoServiceProvider CreateKeys(out string @private,out string @public)
		{
			var rsa = new RSACryptoServiceProvider();
			@private = rsa.ToXmlString(true);
			@public = rsa.ToXmlString(false);
			return rsa;
		}

		public static int GetPieceCount(int size)
		{
			int pieceCount = size / 117;
			if(size % 117 != 0) {
				++pieceCount;
			}
			return pieceCount;
		}

		public static byte[] Encrypt(byte[] bytes, RSACryptoServiceProvider rsa)
		{
			if(bytes.Length <= 117) {
				return rsa.Encrypt(bytes, false);
			}

			// has to be split into pieces, because max length of decryption is 117 bytes

			int pieceCount = GetPieceCount(bytes.Length);

			byte[] encodedBytes = new byte[pieceCount * 128];
			byte[] pieceBytes = new byte[117];
			byte[] encodedPiece;
			int i = 0;
			int j = 0;
			while(i + 117 <= bytes.Length) {
				Array.Copy(bytes, i, pieceBytes, 0, 117);
				encodedPiece = rsa.Encrypt(pieceBytes, false);
				Array.Copy(encodedPiece, 0, encodedBytes, j, encodedPiece.Length);
				i += 117;
				j += 128;
			}
			if(i < bytes.Length) {
				int bytesLeft = bytes.Length - i;
				pieceBytes = new byte[bytesLeft];
				Array.Copy(bytes, i, pieceBytes, 0, bytesLeft);
				encodedPiece = rsa.Encrypt(pieceBytes, false);
				Array.Copy(encodedPiece, 0, encodedBytes, j, encodedPiece.Length);
			}
			return encodedBytes;
		}

		public static byte[] Decrypt(byte[] bytes, RSACryptoServiceProvider rsa)
		{
			if(bytes.Length == 128) {
				return rsa.Decrypt(bytes, false);
			}

			// the message was split into pieces, because max length of decryption is 117 bytes

			if(bytes.Length % 128 != 0) {
				throw new ArgumentException("Invalid message length.", "bytes");
			}

			int pieceCount = bytes.Length / 128;
			List<byte> decodedBytes = new List<byte>(pieceCount * 117);
			byte[] pieceBytes = new byte[128];
			for(int i = 0; i < bytes.Length; i += pieceBytes.Length) {
				Array.Copy(bytes, i, pieceBytes, 0, pieceBytes.Length);
				decodedBytes.AddRange(rsa.Decrypt(pieceBytes, false));
			}
			return decodedBytes.ToArray();
		}
	}
}
