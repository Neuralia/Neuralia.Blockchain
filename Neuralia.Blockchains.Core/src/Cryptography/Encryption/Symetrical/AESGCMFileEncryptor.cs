using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IO;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {
	
	
	public class AESGCMFileEncryptorResult  {
		
		public AESGCMFileEncryptorResult() {

		}
		
		public AESGCMFileEncryptorResult(ByteArray cipher, ByteArray tag) {
			this.Cipher = cipher;
			this.Tag = tag;
		}
		public ByteArray Cipher { get; private set; }
		public ByteArray Tag { get;  private set; }
		
		public static AESGCMFileEncryptorResult Rehydrate(ByteArray bytes) {

			var rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

			var result = new AESGCMFileEncryptorResult();
			result.Rehydrate(rehydrator);

			return result;
		}
		
		private void Rehydrate(IDataRehydrator rehydrator) {
			this.Cipher = rehydrator.ReadNonNullableArray();
			this.Tag = rehydrator.ReadNonNullableArray();
		}

		public ByteArray Dehydrate() {
			using(var dehydrator = DataSerializationFactory.CreateDehydrator()) {

				this.Dehydrate(dehydrator);

				return dehydrator.ToArray().Release();
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
		public static AesGcmEncryptorParameters GenerateEncryptionParameters() {
			SecureRandom rnd = new SecureRandom();

			ByteArray salt = ByteArray.Create(500);

			// get a random salt
			salt.FillSafeRandom();

			return new AesGcmEncryptorParameters {cipher = EncryptorParameters.SymetricCiphers.AES_GCM_256, Salt = salt.ToExactByteArrayCopy(), Iterations = rnd.Next(1000, short.MaxValue), KeyBitLength = 256};
		}

		public static (ByteArray Key, ByteArray Nonce) InitSymmetric(SecureString password, AesGcmEncryptorParameters parameters) {
			return InitSymmetric((ByteArray) Encoding.UTF8.GetBytes(password.ConvertToUnsecureString()), parameters);
		}

		public static (ByteArray Key, ByteArray Nonce) InitSymmetric(SafeArrayHandle password, AesGcmEncryptorParameters parameters) {
			//its not ideal i know, but we have no choice for now. no way to pass a secure string to the encryptor
			//TODO: can this be made safer by clearing the password?

			try {
				using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), parameters.Salt.ToExactByteArray(), parameters.Iterations)) {
					
					ByteArray key = rfc2898DeriveBytes.GetBytes(parameters.KeyBitLength / 8);
					ByteArray nonce = rfc2898DeriveBytes.GetBytes(12);

					return (key, nonce);
				}
			} finally {

				// hopefully this will clear the password from memory (we hope)
				GC.Collect();
			}

		}

		private static ByteArray EncryptBytes(SafeArrayHandle plain, SafeArrayHandle password, AesGcmEncryptorParameters parameters) {

			(ByteArray key, ByteArray nonce) = InitSymmetric(password, parameters);

			ByteArray tag = ByteArray.Create(key.Length);
			ByteArray ciphertext = ByteArray.Create(plain.Length);
			
#if (NETSTANDARD2_0)
			throw new NotImplementedException();
#else
				using (AesGcm aesGcm = new AesGcm(key.ToExactByteArray()))
			{
				aesGcm.Encrypt(nonce.Span, plain.Span, ciphertext.Span, tag.Span);
			}
#endif
		

			AESGCMFileEncryptorResult result = new AESGCMFileEncryptorResult(ciphertext, tag);

			return result.Dehydrate();
		}
		
		public static ByteArray Encrypt(SafeArrayHandle plain, SecureString password, AesGcmEncryptorParameters parameters) {
			
			return Encrypt(plain, password.ConvertToUnsecureBytes(), parameters);
			
		}

		public static ByteArray Encrypt(SafeArrayHandle plain, SafeArrayHandle password, AesGcmEncryptorParameters parameters) {
			return EncryptBytes(plain, password, parameters);
		}
		
		private static ByteArray DecryptBytes(SafeArrayHandle cipher, SafeArrayHandle tag, SafeArrayHandle password, AesGcmEncryptorParameters parameters) {
			
			(ByteArray key, ByteArray nonce) = InitSymmetric(password, parameters);
			
			ByteArray decrypted = ByteArray.Create(cipher.Length);
			
#if (NETSTANDARD2_0)
			throw new NotImplementedException();
#else
			using (AesGcm aesGcm = new AesGcm(key.ToExactByteArray()))
			{
				aesGcm.Decrypt(nonce.Span, cipher.Span, decrypted.Span, tag.Span);
			}
#endif

			return decrypted;
		}

		public static ByteArray Decrypt(SafeArrayHandle cipher, SecureString password, AesGcmEncryptorParameters parameters) {
			try {

				return Decrypt(cipher, password.ConvertToUnsecureBytes(), parameters);
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static ByteArray Decrypt(SafeArrayHandle cipher, SafeArrayHandle password, AesGcmEncryptorParameters parameters) {
			try {
				AESGCMFileEncryptorResult result = AESGCMFileEncryptorResult.Rehydrate(cipher.Entry);
				
				return DecryptBytes(result.Cipher, result.Tag, password, parameters);
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}
		
	}
}