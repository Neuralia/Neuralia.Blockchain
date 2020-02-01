using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IO;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.BouncyCastle.extra.Security;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {
	/// <summary>
	///     Utility class to encrypt with AES 256
	/// </summary>
	public class AESFileEncryptor {
		public static AesEncryptorParameters GenerateEncryptionParameters() {
			SecureRandom rnd = new BetterSecureRandom();

			ByteArray salt = ByteArray.Create(500);

			// get a random salt
			salt.FillSafeRandom();

			var entry = new AesEncryptorParameters {cipher = EncryptorParameters.SymetricCiphers.AES_256, Iterations = rnd.Next(1000, short.MaxValue), KeyBitLength = 256};
			entry.Salt.Entry = salt;
			return entry;
		}

		public static SymmetricAlgorithm InitSymmetric(SymmetricAlgorithm algorithm, SecureString password, AesEncryptorParameters parameters) {
			return InitSymmetric(algorithm, ByteArray.WrapAndOwn(Encoding.UTF8.GetBytes(password.ConvertToUnsecureString())), parameters);
		}

		public static SymmetricAlgorithm InitSymmetric(SymmetricAlgorithm algorithm, SafeArrayHandle password, AesEncryptorParameters parameters) {
			//its not ideal i know, but we have no choice for now. no way to pass a secure string to the encryptor
			//TODO: can this be made safer by clearing the password?

			try {
				using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), parameters.Salt.ToExactByteArrayCopy(), parameters.Iterations)) {

					if(!algorithm.ValidKeySize(parameters.KeyBitLength)) {
						throw new InvalidOperationException("Invalid size key");
					}

					algorithm.Key = rfc2898DeriveBytes.GetBytes(parameters.KeyBitLength / 8);
					algorithm.IV = rfc2898DeriveBytes.GetBytes(algorithm.BlockSize / 8);

					return algorithm;
				}
			} finally {

				// hopefully this will clear the password from memory (we hope)
				GC.Collect();
			}

		}

		private static ByteArray Transform(SafeArrayHandle bytes, Func<ICryptoTransform> selectCryptoTransform) {
			using(RecyclableMemoryStream memoryStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("encryptor")) {

				using(CryptoStream cryptoStream = new CryptoStream(memoryStream, selectCryptoTransform(), CryptoStreamMode.Write, true)) {

					cryptoStream.Write(bytes.Bytes, bytes.Offset, bytes.Length);

					cryptoStream.FlushFinalBlock();

					return ByteArray.Create(memoryStream);
				}
			}
		}

		private static ByteArray Transform(byte[] bytes, Func<ICryptoTransform> selectCryptoTransform) {
			using(RecyclableMemoryStream memoryStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("encryptor")) {
				using(CryptoStream cryptoStream = new CryptoStream(memoryStream, selectCryptoTransform(), CryptoStreamMode.Write)) {
					cryptoStream.Write(bytes, 0, bytes.Length);

					cryptoStream.FlushFinalBlock();

					return ByteArray.Create(memoryStream);
				}
			}
		}

		private static ByteArray Transform(ReadOnlySpan<byte> bytes, Func<ICryptoTransform> selectCryptoTransform) {
			using(RecyclableMemoryStream memoryStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("encryptor")) {
				using(CryptoStream cryptoStream = new CryptoStream(memoryStream, selectCryptoTransform(), CryptoStreamMode.Write)) {

					cryptoStream.Write(bytes);

					cryptoStream.FlushFinalBlock();

					return ByteArray.Create(memoryStream);
				}
			}
		}

		public static ByteArray Encrypt(SafeArrayHandle plain, SecureString password, AesEncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(SafeArrayHandle plain, SafeArrayHandle password, AesEncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(byte[] plain, SecureString password, AesEncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(byte[] plain, SafeArrayHandle password, AesEncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(ReadOnlySpan<byte> plain, SecureString password, AesEncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(ReadOnlySpan<byte> plain, SafeArrayHandle password, AesEncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Decrypt(SafeArrayHandle cipher, SecureString password, AesEncryptorParameters parameters) {
			try {
				using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
					return Transform(cipher, rijndael.CreateDecryptor);
				}
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static ByteArray Decrypt(SafeArrayHandle cipher, SafeArrayHandle password, AesEncryptorParameters parameters) {
			try {
				using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
					return Transform(cipher, rijndael.CreateDecryptor);
				}
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static ByteArray Decrypt(byte[] cipher, SecureString password, AesEncryptorParameters parameters) {
			try {
				using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
					return Transform(cipher, rijndael.CreateDecryptor);
				}
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static ByteArray Decrypt(byte[] cipher, SafeArrayHandle password, AesEncryptorParameters parameters) {
			try {
				using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
					return Transform(cipher, rijndael.CreateDecryptor);
				}
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static ByteArray Decrypt(ReadOnlySpan<byte> cipher, SecureString password, AesEncryptorParameters parameters) {
			try {
				using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
					return Transform(cipher, rijndael.CreateDecryptor);
				}
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static ByteArray Decrypt(ReadOnlySpan<byte> cipher, SafeArrayHandle password, AesEncryptorParameters parameters) {
			try {
				using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
					return Transform(cipher, rijndael.CreateDecryptor);
				}
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}
	}
}