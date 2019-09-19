using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IO;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {
	/// <summary>
	///     Utility class to encrypt with AES 256
	/// </summary>
	public class AESFileEncryptor {
		public static EncryptorParameters GenerateEncryptionParameters() {
			SecureRandom rnd = new SecureRandom();

			ByteArray salt = ByteArray.Create(500);

			// get a random salt
			salt.FillSafeRandom();

			return new EncryptorParameters {cipher = EncryptorParameters.SymetricCiphers.AES_256, salt = salt.ToExactByteArrayCopy(), iterations = rnd.Next(1000, short.MaxValue), keyBitLength = 256};
		}

		public static SymmetricAlgorithm InitSymmetric(SymmetricAlgorithm algorithm, SecureString password, EncryptorParameters parameters) {
			return InitSymmetric(algorithm, (ByteArray) Encoding.UTF8.GetBytes(password.ConvertToUnsecureString()), parameters);
		}

		public static SymmetricAlgorithm InitSymmetric(SymmetricAlgorithm algorithm, SafeArrayHandle password, EncryptorParameters parameters) {
			//its not ideal i know, but we have no choice for now. no way to pass a secure string to the encryptor
			//TODO: can this be made safer by clearing the password?

			try {
				using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), parameters.salt, parameters.iterations)) {

					if(!algorithm.ValidKeySize(parameters.keyBitLength)) {
						throw new InvalidOperationException("Invalid size key");
					}

					algorithm.Key = rfc2898DeriveBytes.GetBytes(parameters.keyBitLength / 8);
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
#if (NETSTANDARD2_0)
				using(CryptoStream cryptoStream = new CryptoStream(memoryStream, selectCryptoTransform(), CryptoStreamMode.Write)) {
#elif (NETCOREAPP2_2)
				using(CryptoStream cryptoStream = new CryptoStream(memoryStream, selectCryptoTransform(), CryptoStreamMode.Write, true)) {
#else
	throw new NotImplementedException();
#endif
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
#if (NETSTANDARD2_0)
					cryptoStream.Write(bytes.ToArray(), 0, bytes.Length);
#elif (NETCOREAPP2_2)
					cryptoStream.Write(bytes);
#else
	throw new NotImplementedException();
#endif

					cryptoStream.FlushFinalBlock();

					return ByteArray.Create(memoryStream);
				}
			}
		}

		public static ByteArray Encrypt(SafeArrayHandle plain, SecureString password, EncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(SafeArrayHandle plain, SafeArrayHandle password, EncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(byte[] plain, SecureString password, EncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(byte[] plain, SafeArrayHandle password, EncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(ReadOnlySpan<byte> plain, SecureString password, EncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Encrypt(ReadOnlySpan<byte> plain, SafeArrayHandle password, EncryptorParameters parameters) {
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), password, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static ByteArray Decrypt(SafeArrayHandle cipher, SecureString password, EncryptorParameters parameters) {
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

		public static ByteArray Decrypt(SafeArrayHandle cipher, SafeArrayHandle password, EncryptorParameters parameters) {
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

		public static ByteArray Decrypt(byte[] cipher, SecureString password, EncryptorParameters parameters) {
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

		public static ByteArray Decrypt(byte[] cipher, SafeArrayHandle password, EncryptorParameters parameters) {
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

		public static ByteArray Decrypt(ReadOnlySpan<byte> cipher, SecureString password, EncryptorParameters parameters) {
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

		public static ByteArray Decrypt(ReadOnlySpan<byte> cipher, SafeArrayHandle password, EncryptorParameters parameters) {
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