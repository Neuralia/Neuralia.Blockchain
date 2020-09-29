using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Core.Cryptography.POW.V1;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.POW.V1.Crypto {
	public class POWCryptoSet  : POWSetBase<CPUPOWRulesSet.Cryptos, IPOWCrypto> {
		

		public POWCryptoSet() {
		}

		public void EncryptStringToBytes(SafeArrayHandle message, SafeArrayHandle encrypted) {
			this.GetRollingEntry().EncryptStringToBytes(message, encrypted);
		}

		protected override IPOWCrypto CreateEntry(CPUPOWRulesSet.Cryptos tag) {
			IPOWCrypto powCrypto = null;
			switch(tag) {
				case CPUPOWRulesSet.Cryptos.AES_256:
					powCrypto = new POWAes256Crypto();
					
					break;
				case CPUPOWRulesSet.Cryptos.AES_GCM:
					powCrypto = new POWAesGCMCrypto();
					
					break;
			}

			return powCrypto;
		}
	}
}