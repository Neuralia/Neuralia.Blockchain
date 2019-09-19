using System;
using System.Security;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {
	public class FileEncryptor {

		public SafeArrayHandle Encrypt(SafeArrayHandle plain, SecureString password, EncryptorParameters parameters) {
			if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
				return AESFileEncryptor.Encrypt(plain, password, parameters);
			}

			if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
				XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor(parameters);

				return xchacha.Encrypt(plain, password);
			}

			throw new ApplicationException("Invalid cipher");
		}

		public SafeArrayHandle Encrypt(SafeArrayHandle plain, SafeArrayHandle password, EncryptorParameters parameters) {
			if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
				return AESFileEncryptor.Encrypt(plain, password, parameters);
			}

			if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
				XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor(parameters);

				return xchacha.Encrypt(plain, password);
			}

			throw new ApplicationException("Invalid cipher");
		}

		public SafeArrayHandle Decrypt(SafeArrayHandle cipher, SecureString password, EncryptorParameters parameters) {
			return this.Decrypt(cipher, 0, cipher.Length, password, parameters);
		}

		public SafeArrayHandle Decrypt(SafeArrayHandle cipher, SafeArrayHandle password, EncryptorParameters parameters) {
			return this.Decrypt(cipher, 0, cipher.Length, password, parameters);
		}

		public SafeArrayHandle Decrypt(SafeArrayHandle cipher, int offset, int length, SecureString password, EncryptorParameters parameters) {
			try {
				if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
					return AESFileEncryptor.Decrypt(cipher, password, parameters);
				}

				if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
					XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor(parameters);

					return xchacha.Decrypt(cipher, password);
				}

				throw new DataEncryptionException("Invalid cipher");
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public SafeArrayHandle Decrypt(SafeArrayHandle cipher, int offset, int length, SafeArrayHandle password, EncryptorParameters parameters) {
			try {
				if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
					return AESFileEncryptor.Decrypt(cipher, password, parameters);
				}

				if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
					XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor(parameters);

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