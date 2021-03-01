using System;
using System.Security;
using System.Security.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Pools;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {
	public class ChaCha20Poly1305FileEncryptor : IDisposableExtended{

		public static ObjectPool<XChaChaPoly1305> ChaChaPoly1305Pool { get; } = new ObjectPool<XChaChaPoly1305>(() => new XChaChaPoly1305(), 0, 1);

		protected readonly ChaCha20Poly1305EncryptorParameters parameters;

		protected readonly XChaChaPoly1305 chachaCipher;

		public ChaCha20Poly1305FileEncryptor(ChaCha20Poly1305EncryptorParameters parameters) {

			this.parameters = parameters;

			this.chachaCipher = ChaChaPoly1305Pool.GetObject();
			this.chachaCipher.SetRounds(parameters.Rounds);
		}
		
		
		public class ChachaContext : FileEncryptor.FileEncryptorContextHandler.FileEncryptorContext {
			public SafeArrayHandle Key { get; set; }

			protected override void DisposeAll() {
				base.DisposeAll();

				Key.Clear();
			}
		}

		public static ChaCha20Poly1305EncryptorParameters GenerateEncryptionParameters(EncryptorParameters.SymetricCiphers cypher, SafeArrayHandle salt, SafeArrayHandle nonce, int? iterations = null) {
			int saltIterations = iterations.HasValue ? iterations.Value : CryptoUtil.GetIterations(GlobalRandom.GetNext(), 1000, 10_000);

			ChaCha20Poly1305EncryptorParameters entry = new ChaCha20Poly1305EncryptorParameters(cypher) {Iterations = saltIterations, KeyBitLength = 256};
			entry.Salt.Entry = salt.Entry;
			entry.Nonce.Entry = nonce.Entry;

			return entry;
		}

		public static ChaCha20Poly1305EncryptorParameters GenerateEncryptionParameters(EncryptorParameters.SymetricCiphers cypher, int? iterations = null, int saltSize = 500) {

			SafeArrayHandle salt = SafeArrayHandle.Create(saltSize);

			// get a random salt
			salt.FillSafeRandom();
			
			SafeArrayHandle nonce = SafeArrayHandle.Create(XChaCha20.NONCE_SIZE_IN_BYTES);
			nonce.FillSafeRandom();
			
			return GenerateEncryptionParameters(cypher, salt, nonce, iterations);
		}

		private SafeArrayHandle GenerateKey(SafeArrayHandle password) {

			using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArrayCopy(), this.parameters.Salt.ToExactByteArrayCopy(), this.parameters.Iterations, HashAlgorithmName.SHA512)) {

				return SafeArrayHandle.WrapAndOwn(rfc2898DeriveBytes.GetBytes(XChaCha.KEY_SIZE_IN_BYTES));
			}
		}

		private class AeadParameters {
			public SafeArrayHandle Nonce { get; }
			public SafeArrayHandle Key { get; }

			public AeadParameters(SafeArrayHandle key, SafeArrayHandle nonce) {
				this.Nonce = nonce;
				this.Key = key;
			}
		}
		private AeadParameters GetAeadParameters(FileEncryptor.FileEncryptorContextHandler contextHandler) {

			if(contextHandler.Entry == null) {
				contextHandler.Entry = new ChachaContext();
			}

			var context = contextHandler.Entry as ChachaContext;

			if(context.Key == null) {
				context.Key = GenerateKey(contextHandler.Password);
			}

			return new AeadParameters(context.Key, this.parameters.Nonce);
		}


		public SafeArrayHandle Encrypt(SafeArrayHandle plain, SafeArrayHandle password) {

			using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
			contextHandler.PasswordBytes = password;

			return this.Encrypt(plain, contextHandler);
		}
		
		public SafeArrayHandle Encrypt(SafeArrayHandle plain, FileEncryptor.FileEncryptorContextHandler contextHandler, int prefix = 0) {

			var parameters = this.GetAeadParameters(contextHandler);
			
			return this.chachaCipher.Encrypt(plain, parameters.Nonce, parameters.Key, null, prefix);

		}

		public SafeArrayHandle Decrypt(SafeArrayHandle cipher, SecureString password) {
			try {
				using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
				contextHandler.PasswordString = password;
				
				return this.Decrypt(cipher, contextHandler);

			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public SafeArrayHandle Decrypt(SafeArrayHandle cipher, SafeArrayHandle password) {
			
			if(this.IsDisposed) {
				throw new ObjectDisposedException("object is disposed");
			}
			try {
				
				using FileEncryptor.FileEncryptorContextHandler contextHandler = new FileEncryptor.FileEncryptorContextHandler();
				contextHandler.PasswordBytes = password;
				
				return this.Decrypt(cipher, contextHandler);
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public SafeArrayHandle Decrypt(SafeArrayHandle ciphertext, FileEncryptor.FileEncryptorContextHandler contextHandler) {

			if(this.IsDisposed) {
				throw new ObjectDisposedException("object is disposed");
			}
			try {

				var parameters = this.GetAeadParameters(contextHandler);
			
				
				return this.chachaCipher.Decrypt(ciphertext, parameters.Nonce, parameters.Key);
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}
		
	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {

				ChaChaPoly1305Pool.PutObject(this.chachaCipher);
			}

			this.IsDisposed = true;
		}

		~ChaCha20Poly1305FileEncryptor() {
			this.Dispose(false);
		}

	#endregion
	}
}