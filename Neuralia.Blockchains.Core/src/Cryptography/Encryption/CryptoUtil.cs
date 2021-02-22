using System;
using System.Security.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption {
	public static class CryptoUtil {
		
		public const int CODE_BUFFER_SIZE = 64;
		public const int PASSWORD_LENGTH = CODE_BUFFER_SIZE - SALT_LENGTH;
		public const int SALT_LENGTH = 16;

		public const int CODE_LENGTH = 900;
		public const int CODE_PASSWORD_LENGTH = CODE_LENGTH - CODE_SALT_LENGTH;
		public const int CODE_SALT_LENGTH = 100;
		
		public static SafeArrayHandle GenerateCodeBuffer() {
			return SafeArrayHandle.CreateSafeRandom(CODE_BUFFER_SIZE);
		}
		
		public static (SafeArrayHandle nonce, SafeArrayHandle key) GenerateKeyNonceSet(SafeArrayHandle password, SafeArrayHandle salt, int rounds = 5000) {
			SafeArrayHandle nonce = null;
			SafeArrayHandle key = null;
			using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), salt.ToExactByteArrayCopy(), rounds, HashAlgorithmName.SHA512)) {
					
				key = SafeArrayHandle.WrapAndOwn(rfc2898DeriveBytes.GetBytes(XChaCha.KEY_SIZE_IN_BYTES));
				nonce = SafeArrayHandle.WrapAndOwn(rfc2898DeriveBytes.GetBytes(XChaCha.NONCE_SIZE_IN_BYTES));
			}
			
			return (nonce, key);
		}
		
		public static int GetIterations(int value, int min, int max) {
			return Math.Min(Math.Max(Math.Abs(value) & 0xFFFF, min), max);
		}

		public static (SafeArrayHandle password, SafeArrayHandle salt) GeneratePasswordSalt(SafeArrayHandle codeBuffer) {

			if(codeBuffer.Length != CODE_BUFFER_SIZE) {
				throw new InvalidOperationException("Code is of wrong length");
			}
			
			using var password = (SafeArrayHandle) codeBuffer.Entry.Slice(0, PASSWORD_LENGTH);
			using var salt = (SafeArrayHandle) codeBuffer.Entry.Slice(SALT_LENGTH);

			TypeSerializer.Deserialize(password.Span.Slice(0, sizeof(int)), out int iterations);

			using SafeArrayHandle code = SafeArrayHandle.Create(CODE_LENGTH);

			using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), salt.ToExactByteArrayCopy(), GetIterations(iterations, 3000, 10_000), HashAlgorithmName.SHA512)) {
				SafeArrayHandle.Wrap(rfc2898DeriveBytes.GetBytes(code.Length)).CopyTo(code);
			}
			
			var resultPassword = (SafeArrayHandle) code.Entry.Slice(0, CODE_PASSWORD_LENGTH);
			var resultSalt = (SafeArrayHandle) code.Entry.Slice(CODE_PASSWORD_LENGTH, CODE_SALT_LENGTH);
			
			return (resultPassword, resultSalt);
		}
		
		
		public static SafeArrayHandle Encrypt(SafeArrayHandle message, SafeArrayHandle password, SafeArrayHandle salt) {

			TypeSerializer.Deserialize(password.Span.Slice(0, sizeof(int)), out int iterations);

			using XchachaEncryptor xchacha = new XchachaEncryptor();

			return xchacha.Encrypt(message, password, salt, GetIterations(iterations, 2000, 10_000));
		}
		
		public static SafeArrayHandle Decrypt(SafeArrayHandle encrypted, SafeArrayHandle password, SafeArrayHandle salt) {

			TypeSerializer.Deserialize(password.Span.Slice(0, sizeof(int)), out int iterations);

			using XchachaEncryptor xchacha = new XchachaEncryptor();

			return xchacha.Decrypt(encrypted, password, salt, GetIterations(iterations, 2000, 10_000));
		}
	}
	
	
}