using System;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;

namespace Neuralia.Blockchains.Core.Tools {
	public static class FileEncryptorUtils {
		public static IEncryptorParameters GenerateEncryptionParameters(ChainConfigurations chainConfiguration) {

			if(chainConfiguration.WalletEncryptionFormat == EncryptorParameters.SymetricCiphers.AES_256) {
				return AESFileEncryptor.GenerateEncryptionParameters();
			}

			if(chainConfiguration.WalletEncryptionFormat == EncryptorParameters.SymetricCiphers.AES_GCM_256) {
				return AESGCMFileEncryptor.GenerateEncryptionParameters();
			}
			
			if(chainConfiguration.WalletEncryptionFormat == EncryptorParameters.SymetricCiphers.XCHACHA_20_POLY_1305 ||
			   chainConfiguration.WalletEncryptionFormat == EncryptorParameters.SymetricCiphers.XCHACHA_30_POLY_1305 ||
			   chainConfiguration.WalletEncryptionFormat == EncryptorParameters.SymetricCiphers.XCHACHA_40_POLY_1305) {
				return ChaCha20Poly1305FileEncryptor.GenerateEncryptionParameters(chainConfiguration.WalletEncryptionFormat);
			}

			throw new ApplicationException("invalid cipher");
		}
	}
}