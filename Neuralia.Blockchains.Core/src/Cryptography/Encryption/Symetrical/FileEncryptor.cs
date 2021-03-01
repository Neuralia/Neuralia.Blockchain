using System;
using System.Security;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {

	public static class FileEncryptor {

		public class FileEncryptorContextHandler : IDisposableExtended{

			public abstract class FileEncryptorContext : IDisposableExtended {
			#region disposable

				public bool IsDisposed { get; private set; }

				public void Dispose() {
					this.Dispose(true);
					GC.SuppressFinalize(this);
				}

				private void Dispose(bool disposing) {

					if(disposing && !this.IsDisposed) {
						this.DisposeAll();
					}

					this.IsDisposed = true;
				}

				protected virtual void DisposeAll() {

					
				}

				~FileEncryptorContext() {
					this.Dispose(false);

				}

			#endregion
			}

			public FileEncryptorContext Entry { get; set; }

			public SafeArrayHandle PasswordBytes { get; set; }
			public SecureString PasswordString { get; set; }

			public SafeArrayHandle Password {
				get {
					if(PasswordBytes == null || PasswordBytes.IsZero && PasswordString != null) {
						PasswordBytes = PasswordString.ConvertToUnsecureBytes();
					}

					return PasswordBytes;
				}
			}

		#region Dispose

			public bool IsDisposed { get; private set; }

			public void Dispose() {
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing) {

				if(disposing && !this.IsDisposed) {

					this.Entry?.Dispose();
					this.PasswordBytes?.Dispose();
					this.PasswordString?.Dispose();
				}

				this.IsDisposed = true;
			}

			~FileEncryptorContextHandler() {
				this.Dispose(false);
			}

		#endregion
		}

		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, FileEncryptorContextHandler handler, IEncryptorParameters parameters, int prefix = 0) {
			if(parameters.Cipher == EncryptorParameters.SymetricCiphers.AES_256) {
				return AESFileEncryptor.Encrypt(plain, handler, (AesEncryptorParameters) parameters);
			}

			if(parameters.Cipher == EncryptorParameters.SymetricCiphers.AES_GCM_256) {
				return AESGCMFileEncryptor.Encrypt(plain, handler, (AesGcmEncryptorParameters) parameters);
			}

			if(parameters.Cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20_POLY_1305 ||
			   parameters.Cipher == EncryptorParameters.SymetricCiphers.XCHACHA_30_POLY_1305 ||
			   parameters.Cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40_POLY_1305) {
				using ChaCha20Poly1305FileEncryptor xchacha = new ChaCha20Poly1305FileEncryptor((ChaCha20Poly1305EncryptorParameters) parameters);

				return xchacha.Encrypt(plain, handler, prefix);
			}

			throw new ApplicationException("Invalid cipher");
		}
		
		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, SecureString password, IEncryptorParameters parameters) {
			
			using FileEncryptorContextHandler handler = new FileEncryptorContextHandler();
			handler.PasswordString = password;

			return Encrypt(plain, handler, parameters);
		}

		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, SafeArrayHandle password, IEncryptorParameters parameters, int prefix = 0) {
			using FileEncryptorContextHandler handler = new FileEncryptorContextHandler();
			handler.PasswordBytes = password;
			return Encrypt(plain, handler, parameters, prefix);
		}
		

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, FileEncryptorContextHandler handler, IEncryptorParameters parameters) {
			try {
				if(parameters.Cipher == EncryptorParameters.SymetricCiphers.AES_256) {
					return AESFileEncryptor.Decrypt(cipher, handler, (AesEncryptorParameters) parameters);
				}

				if(parameters.Cipher == EncryptorParameters.SymetricCiphers.AES_GCM_256) {
					return AESGCMFileEncryptor.Decrypt(cipher, handler, (AesGcmEncryptorParameters) parameters);
				}

				if(parameters.Cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20_POLY_1305 ||
				   parameters.Cipher == EncryptorParameters.SymetricCiphers.XCHACHA_30_POLY_1305 ||
				   parameters.Cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40_POLY_1305) {
					using ChaCha20Poly1305FileEncryptor xchacha = new ChaCha20Poly1305FileEncryptor((ChaCha20Poly1305EncryptorParameters) parameters);

					return xchacha.Decrypt(cipher, handler);
				}

				throw new DataEncryptionException("Invalid cipher");
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}
		
		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, SecureString password, IEncryptorParameters parameters) {
			using FileEncryptorContextHandler handler = new FileEncryptorContextHandler();
			handler.PasswordString = password;
			return Decrypt(cipher, handler, parameters);
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, SafeArrayHandle password, IEncryptorParameters parameters) {
			using FileEncryptorContextHandler handler = new FileEncryptorContextHandler();
			handler.PasswordBytes = password;
			return Decrypt(cipher, handler, parameters);
		}
	}
}