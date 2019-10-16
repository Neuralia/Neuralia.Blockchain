using System;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;

namespace Neuralia.Blockchains.Core.Tools {
	public static class FileEncryptorUtils {
		public static IEncryptorParameters GenerateEncryptionParameters(AppSettingsBase appSettingsBase) {

			if(appSettingsBase.WalletEncryptionFormat == (byte) EncryptorParameters.SymetricCiphers.AES_256) {
				return AESFileEncryptor.GenerateEncryptionParameters();
			}
			
			if(appSettingsBase.WalletEncryptionFormat == (byte) EncryptorParameters.SymetricCiphers.AES_GCM_256) {
				return AESGCMFileEncryptor.GenerateEncryptionParameters();
			}

			if((appSettingsBase.WalletEncryptionFormat == (byte) EncryptorParameters.SymetricCiphers.XCHACHA_20) || (appSettingsBase.WalletEncryptionFormat == (byte) EncryptorParameters.SymetricCiphers.XCHACHA_40)) {
				return XChaChaFileEncryptor.GenerateEncryptionParameters();
			}

			throw new ApplicationException("invalid cipher");
		}
	}
}