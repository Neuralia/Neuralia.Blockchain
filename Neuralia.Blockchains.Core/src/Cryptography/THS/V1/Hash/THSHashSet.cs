using System;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Hash {
	public class THSHashSet : THSSetBase<THSRulesSet.Hashes, ITHSHash> {

		public SafeArrayHandle Hash(SafeArrayHandle message) {

			return this.GetRollingEntry().Hash(message);
		}

		public SafeArrayHandle Hash512(SafeArrayHandle message) {

			int startIndex = this.rollingindex;

			while(true) {
				ITHSHash entry = this.GetRollingEntry();

				if(entry.HashType == 512) {
					return entry.Hash(message);
				}

				if(startIndex == this.rollingindex) {
					break;
				}
			}

			throw new ApplicationException("Did not find any 512 bit hash algorithm");
		}

		protected override ITHSHash CreateEntry(THSRulesSet.Hashes tag) {
			ITHSHash thsHash = null;

			switch(tag) {
				case THSRulesSet.Hashes.SHA2_256:
					thsHash = new THSSha2256Hash();

					break;
				case THSRulesSet.Hashes.SHA3_256:
					thsHash = new THSSha3256Hash();

					break;
				case THSRulesSet.Hashes.SHA2_512:
					thsHash = new THSSha2512Hash();

					break;
				case THSRulesSet.Hashes.SHA3_512:
					thsHash = new THSSha3512Hash();

					break;
				case THSRulesSet.Hashes.XX_HASH:
					thsHash = new THSxxHash();

					break;

				default:
					throw new ArgumentException();
			}

			return thsHash;
		}
	}
}