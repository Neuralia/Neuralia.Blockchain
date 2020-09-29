using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Core.Cryptography.POW.V1;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.POW.V1.Hash {
	public class POWHashSet : POWSetBase<CPUPOWRulesSet.Hashes, IPOWHash> {


		public POWHashSet() {
		}

		public SafeArrayHandle Hash(SafeArrayHandle message) {
			
			return this.GetRollingEntry().Hash(message);
		}
		public SafeArrayHandle Hash512(SafeArrayHandle message) {

			var startIndex = this.rollingindex;
			while(true) {
				var entry = this.GetRollingEntry();

				if(entry.HashType == 512) {
					return entry.Hash(message);
				}

				if(startIndex == this.rollingindex) {
					break;
				}
			}
			throw new ApplicationException("Did not find any 512 bit hash algorithm");
		}

		protected override IPOWHash CreateEntry(CPUPOWRulesSet.Hashes tag) {
			IPOWHash powHash = null;
			switch(tag) {
				case CPUPOWRulesSet.Hashes.SHA2_512:
					powHash = new POWSha2512Hash();

					break;
				case CPUPOWRulesSet.Hashes.SHA3_512:
					powHash = new POWSha3512Hash();

					break;
			}

			return powHash;
		}
	}
}