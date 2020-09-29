using System;
using System.IO;
using System.Security.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.POW.V1.Crypto {
	public class POWAesGCMCrypto : POWCryptoBase {
		private const int AESGCM_KEY_SIZE = 32;
		private const int AESGCM_NONCE_SIZE = 12;
		private const int AESGCM_TAG_SIZE = 16;
		
		public override void EncryptStringToBytes(SafeArrayHandle message, SafeArrayHandle encrypted) {
			
			Span<byte> key = stackalloc byte[AESGCM_KEY_SIZE];
			message.Span.Slice(message.Length - AESGCM_KEY_SIZE, AESGCM_KEY_SIZE).CopyTo(key);

			using(AesGcm aesGcm = new AesGcm(key)) {

				Span<byte> nonce = stackalloc byte[AESGCM_NONCE_SIZE];
				message.Span.Slice(message.Length - AESGCM_NONCE_SIZE - AESGCM_KEY_SIZE, AESGCM_NONCE_SIZE).CopyTo(nonce);
				
				Span<byte> tag = stackalloc byte[AESGCM_TAG_SIZE];
				message.Span.Slice(message.Length - AESGCM_TAG_SIZE - AESGCM_KEY_SIZE - AESGCM_NONCE_SIZE, AESGCM_TAG_SIZE).CopyTo(tag);
				
				aesGcm.Encrypt(nonce, message.Span, encrypted.Span, tag);
			}
		}
	}
}