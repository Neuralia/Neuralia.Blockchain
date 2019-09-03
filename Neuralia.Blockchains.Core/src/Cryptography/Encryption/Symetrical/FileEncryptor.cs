using System;
using System.Security;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {
	public class FileEncryptor {

		public IByteArray Encrypt(IByteArray plain, SecureString password, EncryptorParameters parameters) {
			if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
				return AESFileEncryptor.Encrypt(plain, password, parameters);
			}

			if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
				XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor(parameters);

				return xchacha.Encrypt(plain, password);
			}

			throw new ApplicationException("Invalid cipher");
		}

		public IByteArray Encrypt(IByteArray plain, IByteArray password, EncryptorParameters parameters) {
			if(parameters.cipher == EncryptorParameters.SymetricCiphers.AES_256) {
				return AESFileEncryptor.Encrypt(plain, password, parameters);
			}

			if((parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) || (parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
				XChaChaFileEncryptor xchacha = new XChaChaFileEncryptor(parameters);

				return xchacha.Encrypt(plain, password);
			}

			throw new ApplicationException("Invalid cipher");
		}

		public IByteArray Decrypt(IByteArray cipher, SecureString password, EncryptorParameters parameters) {
			return this.Decrypt(cipher, 0, cipher.Length, password, parameters);
		}

		public IByteArray Decrypt(IByteArray cipher, IByteArray password, EncryptorParameters parameters) {
			return this.Decrypt(cipher, 0, cipher.Length, password, parameters);
		}

		public IByteArray Decrypt(IByteArray cipher, int offset, int length, SecureString password, EncryptorParameters parameters) {
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

		public IByteArray Decrypt(IByteArray cipher, int offset, int length, IByteArray password, EncryptorParameters parameters) {
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