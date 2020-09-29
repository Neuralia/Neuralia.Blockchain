using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IO;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {
	/// <summary>
	///     Utility class to encrypt with AES 256
	/// </summary>
	public class AESFileEncryptor {
		public static AesEncryptorParameters GenerateEncryptionParameters(int saltSize = 500) {

			ByteArray salt = ByteArray.Create(saltSize);

			// get a random salt
			salt.FillSafeRandom();

			AesEncryptorParameters entry = new AesEncryptorParameters {Cipher = EncryptorParameters.SymetricCiphers.AES_256, Iterations = GlobalRandom.GetNext(1000, short.MaxValue), KeyBitLength = 256};
			entry.Salt.Entry = salt;

			return entry;
		}
		
		public class AESContext : FileEncryptor.FileEncryptorContextHandler.FileEncryptorContext {
			public SafeArrayHandle Key { get; set; }
			public bool KeySet => this.Key != null && !this.Key.IsZero;
			
			public SafeArrayHandle IV { get; set; }
			public bool IVSet => this.IV != null && !this.IV.IsZero;

			public bool AllSet => this.KeySet && this.IVSet;
			
			public (SafeArrayHandle key, SafeArrayHandle iv) All => (this.Key, this.IV);
			
			protected override void DisposeAll() {
				base.DisposeAll();
				
				this.Key?.SafeDispose();
				this.IV?.SafeDispose();
			}
		}
		
		public static SymmetricAlgorithm InitSymmetric(SymmetricAlgorithm algorithm, FileEncryptor.FileEncryptorContextHandler contextHandler, AesEncryptorParameters parameters) {
			//its not ideal i know, but we have no choice for now. no way to pass a secure string to the encryptor
			//TODO: can this be made safer by clearing the password?

			if(contextHandler.Entry == null) {
				contextHandler.Entry = new AESContext();
			}
			var context = contextHandler.Entry as AESContext;
			

			if(!context.AllSet) {
				using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(contextHandler.Password.ToExactByteArrayCopy(), parameters.Salt.ToExactByteArrayCopy(), parameters.Iterations, HashAlgorithmName.SHA512)) {

					if(!algorithm.ValidKeySize(parameters.KeyBitLength)) {
						throw new InvalidOperationException("Invalid size key");
					}

					context.Key = (SafeArrayHandle)rfc2898DeriveBytes.GetBytes(parameters.KeyBitLength / 8);
					context.IV = (SafeArrayHandle)rfc2898DeriveBytes.GetBytes(algorithm.BlockSize / 8);
				}
			}
			
			(SafeArrayHandle key, SafeArrayHandle iv) = context.All;
			
			try {
				//TODO: should it be a copy instead?
				algorithm.Key = key.ToExactByteArray();
				algorithm.IV = iv.ToExactByteArray();

				return algorithm;
			} finally {

				// hopefully this will clear the password from memory (we hope)
				GC.Collect();
			}

		}

		public static void Transform(SafeArrayHandle bytes, Stream encryptedStream, Func<ICryptoTransform> selectCryptoTransform) {

			using(CryptoStream cryptoStream = new CryptoStream(encryptedStream, selectCryptoTransform(), CryptoStreamMode.Write, true)) {

				cryptoStream.Write(bytes.Bytes, bytes.Offset, bytes.Length);

				cryptoStream.FlushFinalBlock();
			}
		}
		
		public static SafeArrayHandle Transform(SafeArrayHandle bytes, Func<ICryptoTransform> selectCryptoTransform) {
			using(RecyclableMemoryStream memoryStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("encryptor")) {

				Transform(bytes, memoryStream, selectCryptoTransform);
				
				return SafeArrayHandle.Create(memoryStream);
			}
		}

		public static SafeArrayHandle Transform(byte[] bytes, Func<ICryptoTransform> selectCryptoTransform) {
			using(RecyclableMemoryStream memoryStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("encryptor")) {
				using(CryptoStream cryptoStream = new CryptoStream(memoryStream, selectCryptoTransform(), CryptoStreamMode.Write)) {
					cryptoStream.Write(bytes, 0, bytes.Length);

					cryptoStream.FlushFinalBlock();

					return SafeArrayHandle.Create(memoryStream);
				}
			}
		}

		public static SafeArrayHandle Transform(ReadOnlySpan<byte> bytes, Func<ICryptoTransform> selectCryptoTransform) {
			using(RecyclableMemoryStream memoryStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("encryptor")) {
				using(CryptoStream cryptoStream = new CryptoStream(memoryStream, selectCryptoTransform(), CryptoStreamMode.Write)) {

					cryptoStream.Write(bytes);

					cryptoStream.FlushFinalBlock();

					return SafeArrayHandle.Create(memoryStream);
				}
			}
		}

		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, SecureString password, AesEncryptorParameters parameters) {
			
			using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
			contextHandler.PasswordString = password;

			return Encrypt(plain, contextHandler, parameters);
		}

		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, SafeArrayHandle password, AesEncryptorParameters parameters) {
			
			using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
			contextHandler.PasswordBytes = password;

			return Encrypt(plain, contextHandler, parameters);
		}
		
		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, FileEncryptor.FileEncryptorContextHandler contextHandler, AesEncryptorParameters parameters) {

			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), contextHandler, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}
		
		

		public static SafeArrayHandle Encrypt(byte[] plain, SecureString password, AesEncryptorParameters parameters) {
			return Encrypt(SafeArrayHandle.Wrap(plain), password, parameters);
		}

		public static SafeArrayHandle Encrypt(byte[] plain, SafeArrayHandle password, AesEncryptorParameters parameters) {

			return Encrypt(SafeArrayHandle.Wrap(plain), password, parameters);
		}

		public static SafeArrayHandle Encrypt(ReadOnlySpan<byte> plain, SecureString password, AesEncryptorParameters parameters) {
			using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
			contextHandler.PasswordString = password;
			
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), contextHandler, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static SafeArrayHandle Encrypt(ReadOnlySpan<byte> plain, SafeArrayHandle password, AesEncryptorParameters parameters) {
			
			using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
			contextHandler.PasswordBytes = password;
			
			using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), contextHandler, parameters)) {
				return Transform(plain, rijndael.CreateEncryptor);
			}
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, SecureString password, AesEncryptorParameters parameters) {
			using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
			contextHandler.PasswordString = password;

			return Decrypt(cipher, contextHandler, parameters);
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, SafeArrayHandle password, AesEncryptorParameters parameters) {
			using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
			contextHandler.PasswordBytes = password;

			return Decrypt(cipher, contextHandler, parameters);
		}
		
		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, FileEncryptor.FileEncryptorContextHandler contextHandler, AesEncryptorParameters parameters) {
			try {

				using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), contextHandler, parameters)) {
					return Transform(cipher, rijndael.CreateDecryptor);
				}
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static SafeArrayHandle Decrypt(byte[] cipher, SecureString password, AesEncryptorParameters parameters) {
			return Decrypt(SafeArrayHandle.Wrap(cipher), password, parameters);
		}

		public static SafeArrayHandle Decrypt(byte[] cipher, SafeArrayHandle password, AesEncryptorParameters parameters) {
			return Decrypt(SafeArrayHandle.Wrap(cipher), password, parameters);
		}

		public static SafeArrayHandle Decrypt(ReadOnlySpan<byte> cipher, SecureString password, AesEncryptorParameters parameters) {
			try {
				
				using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
				contextHandler.PasswordString = password;
				
				using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), contextHandler, parameters)) {
					return Transform(cipher, rijndael.CreateDecryptor);
				}
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static SafeArrayHandle Decrypt(ReadOnlySpan<byte> cipher, SafeArrayHandle password, AesEncryptorParameters parameters) {
			try {
				
				using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
				contextHandler.PasswordBytes = password;
				
				using(SymmetricAlgorithm rijndael = InitSymmetric(Rijndael.Create(), contextHandler, parameters)) {
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