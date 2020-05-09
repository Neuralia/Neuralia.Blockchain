using System;
using System.Security;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {

	public static class FileEncryptor {

		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, SecureString password, IEncryptorParameters parameters) {
			if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
				return AESFileEncryptor.Encrypt(plain, password, (AesEncryptorParameters) parameters);
			}

			if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_GCM_256) {
				return AESGCMFileEncryptor.Encrypt(plain, password, (AesGcmEncryptorParameters) parameters);
			}

			if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
				XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor((XChachaEncryptorParameters) parameters);

				return xchacha.Encrypt(plain, password);
			}

			throw new ApplicationException("Invalid cipher");
		}

		public static SafeArrayHandle Encrypt(SafeArrayHandle plain, SafeArrayHandle password, IEncryptorParameters parameters) {
			if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
				return AESFileEncryptor.Encrypt(plain, password, (AesEncryptorParameters) parameters);
			}

			if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_GCM_256) {
				return AESGCMFileEncryptor.Encrypt(plain, password, (AesGcmEncryptorParameters) parameters);
			}

			if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
				XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor((XChachaEncryptorParameters) parameters);

				return xchacha.Encrypt(plain, password);
			}

			throw new ApplicationException("Invalid cipher");
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, SecureString password, IEncryptorParameters parameters) {
			return Decrypt(cipher, 0, cipher.Length, password, parameters);
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, SafeArrayHandle password, IEncryptorParameters parameters) {
			return Decrypt(cipher, 0, cipher.Length, password, parameters);
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, int offset, int length, SecureString password, IEncryptorParameters parameters) {
			try {
				if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
					return AESFileEncryptor.Decrypt(cipher, password, (AesEncryptorParameters) parameters);
				}

				if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_GCM_256) {
					return AESGCMFileEncryptor.Decrypt(cipher, password, (AesGcmEncryptorParameters) parameters);
				}

				if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
					XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor((XChachaEncryptorParameters) parameters);

					return xchacha.Decrypt(cipher, password);
				}

				throw new DataEncryptionException("Invalid cipher");
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle cipher, int offset, int length, SafeArrayHandle password, IEncryptorParameters parameters) {
			try {
				if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
					return AESFileEncryptor.Decrypt(cipher, password, (AesEncryptorParameters) parameters);
				}

				if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_GCM_256) {
					return AESGCMFileEncryptor.Decrypt(cipher, password, (AesGcmEncryptorParameters) parameters);
				}

				if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
					XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor((XChachaEncryptorParameters) parameters);

					return xchacha.Decrypt(cipher, password);
				}

				throw new DataEncryptionException("Invalid cipher");
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}
	}
}