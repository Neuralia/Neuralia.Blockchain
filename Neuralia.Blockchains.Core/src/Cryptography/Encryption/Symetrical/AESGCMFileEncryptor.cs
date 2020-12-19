using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;


namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {

	public class AESGCMFileEncryptorResult {

		public AESGCMFileEncryptorResult() {

		}

		public AESGCMFileEncryptorResult(SafeArrayHandle cipher, SafeArrayHandle tag) {
			this.Cipher = cipher;
			this.Tag = tag;
		}

		public SafeArrayHandle Cipher { get; private set; }
		public SafeArrayHandle Tag { get; private set; }

		public static AESGCMFileEncryptorResult Rehydrate(ByteArray bytes) {

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

			AESGCMFileEncryptorResult result = new AESGCMFileEncryptorResult();
			result.Rehydrate(rehydrator);

			return result;
		}

		private void Rehydrate(IDataRehydrator rehydrator) {
			this.Cipher = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			this.Tag = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
		}

		public SafeArrayHandle Dehydrate() {
			using(IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator()) {

				this.Dehydrate(dehydrator);

				return (SafeArrayHandle)dehydrator.ToReleasedArray();
			}
		}

		private void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.WriteNonNullable(this.Cipher);
			dehydrator.WriteNonNullable(this.Tag);
		}
	}

	/// <summary>
	///     Utility class to encrypt with AES 256
	/// </summary>
	public class AESGCMFileEncryptor {
		
		public class AESGCMContext : FileEncryptor.FileEncryptorContextHandler.FileEncryptorContext {
			public SafeArrayHandle Key { get; set; }
			public bool KeySet => this.Key != null && !this.Key.IsZero;
			
			public SafeArrayHandle Nonce { get; set; }
			public bool NonceSet => this.Nonce != null && !this.Nonce.IsZero;

			public bool AllSet => this.KeySet && this.NonceSet;
			
			public (SafeArrayHandle key, SafeArrayHandle nonce) All => (this.Key, this.Nonce);
			
			protected override void DisposeAll() {
				base.DisposeAll();
				
				this.Key?.SafeDispose();
				this.Nonce?.SafeDispose();
			}
		}
		
		public static AesGcmEncryptorParameters GenerateEncryptionParameters() {

			ByteArray salt = ByteArray.Create(500);

			// get a random salt
			salt.FillSafeRandom();

			AesGcmEncryptorParameters entry = new AesGcmEncryptorParameters {Cipher = EncryptorParameters.SymetricCiphers.AES_GCM_256, Iterations = GlobalRandom.GetNext(1000, short.MaxValue), KeyBitLength = 256};
			entry.Salt.Entry = salt;

			return entry;
		}

		public static (SafeArrayHandle Key, SafeArrayHandle Nonce) InitSymmetric(SecureString password, AesGcmEncryptorParameters parameters) {
			return InitSymmetric(SafeArrayHandle.WrapAndOwn(Encoding.UTF8.GetBytes(password.ConvertToUnsecureString())), parameters);
		}

		public static (SafeArrayHandle Key, SafeArrayHandle Nonce) InitSymmetric(SafeArrayHandle password, AesGcmEncryptorParameters parameters) {
			//its not ideal i know, but we have no choice for now. no way to pass a secure string to the encryptor
			//TODO: can this be made safer by clearing the password?

			try {
				using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), parameters.Salt.ToExactByteArrayCopy(), parameters.Iterations, HashAlgorithmName.SHA512)) {

					SafeArrayHandle key = SafeArrayHandle.WrapAndOwn(rfc2898DeriveBytes.GetBytes(parameters.KeyBitLength / 8));
					SafeArrayHandle nonce = SafeArrayHandle.WrapAndOwn(rfc2898DeriveBytes.GetBytes(12));

					return (key, nonce);
				}
			} finally {

				// hopefully this will clear the password from memory (we hope)
				GC.Collect();
			}

		}

		private static (SafeArrayHandle Key, SafeArrayHandle Nonce) GetComponents(FileEncryptor.FileEncryptorContextHandler contextHandler, AesGcmEncryptorParameters parameters) {
			if(contextHandler.Entry == null) {
				contextHandler.Entry = new AESGCMContext();
			}
			var context = contextHandler.Entry as AESGCMContext;
			
			if(!context.AllSet) {
				(context.Key, context.Nonce) = InitSymmetric(contextHandler.Password, parameters);
			}
			
			return context.All;
		}
		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, FileEncryptor.FileEncryptorContextHandler contextHandler, AesGcmEncryptorParameters parameters) {

			(SafeArrayHandle key, SafeArrayHandle nonce) = GetComponents(contextHandler, parameters);
			try {
				SafeArrayHandle tag = SafeArrayHandle.Create(16);

				SafeArrayHandle ciphertext = SafeArrayHandle.Create(plain.Length);
				
				using(AesGcm aesGcm = new AesGcm(key.Span)) {
					aesGcm.Encrypt(nonce.Span, plain.Span, ciphertext.Span, tag.Span);
				}
				
				AESGCMFileEncryptorResult result = new AESGCMFileEncryptorResult(ciphertext, tag);

				return result.Dehydrate();
			} finally {
				key.Dispose();
				nonce.Dispose();
			}
		}

		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, SecureString password, AesGcmEncryptorParameters parameters) {

			using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
			contextHandler.PasswordString = password;
			
			return Encrypt(plain, contextHandler, parameters);

		}

		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, SafeArrayHandle password, AesGcmEncryptorParameters parameters) {
			
			using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
			contextHandler.PasswordBytes = password;
			
			return Encrypt(plain, contextHandler, parameters);
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, FileEncryptor.FileEncryptorContextHandler contextHandler, AesGcmEncryptorParameters parameters) {

			AESGCMFileEncryptorResult cipherComponents = AESGCMFileEncryptorResult.Rehydrate(cipher.Entry);
			(SafeArrayHandle key, SafeArrayHandle nonce) = GetComponents(contextHandler, parameters);

			try {
				SafeArrayHandle decrypted = SafeArrayHandle.Create(cipherComponents.Cipher.Length);
				
				using(AesGcm aesGcm = new AesGcm(key.Span)) {
					aesGcm.Decrypt(nonce.Span, cipherComponents.Cipher.Span, cipherComponents.Tag.Span, decrypted.Span);
				}
				
				return decrypted;
			}
			finally {
				key.Dispose();
				nonce.Dispose();
			}
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, SecureString password, AesGcmEncryptorParameters parameters) {
			try {

				using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
				contextHandler.PasswordString = password;
				
				return Decrypt(cipher, contextHandler, parameters);
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, SafeArrayHandle password, AesGcmEncryptorParameters parameters) {
			try {
				using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
				contextHandler.PasswordBytes = password;
				
				return Decrypt(cipher, contextHandler, parameters);
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}
	}
}