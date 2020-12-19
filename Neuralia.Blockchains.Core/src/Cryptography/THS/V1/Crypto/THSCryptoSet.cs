using System;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Crypto {
	public class THSCryptoSet : THSSetBase<THSRulesSet.Cryptos, ITHSCrypto> {

		public void EncryptStringToBytes(SafeArrayHandle message, SafeArrayHandle encrypted) {
			this.GetRollingEntry().EncryptStringToBytes(message, encrypted);
		}

		protected override ITHSCrypto CreateEntry(THSRulesSet.Cryptos tag) {
			ITHSCrypto thsCrypto = null;

			switch(tag) {
				case THSRulesSet.Cryptos.AES_256:
					thsCrypto = new THSAes256Crypto();

					break;
				case THSRulesSet.Cryptos.AES_GCM:
					thsCrypto = new THSAesGCMCrypto();

					break;
				case THSRulesSet.Cryptos.XCHACHA_20:
					thsCrypto = new THSXChacha20Crypto();

					break;
				case THSRulesSet.Cryptos.XCHACHA_40:
					thsCrypto = new THSXChacha40Crypto();

					break;

				default:
					throw new ArgumentException();
			}

			return thsCrypto;
		}
	}
}