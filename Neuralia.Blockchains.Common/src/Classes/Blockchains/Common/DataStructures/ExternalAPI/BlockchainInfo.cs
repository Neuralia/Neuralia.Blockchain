using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {
	public class BlockchainInfo {
		public long BlockId { get; set; }
		public string BlockHash { get; set; }
		public long PublicBlockId { get; set; }
		public string BlockTimestamp { get; set; }

		public int DigestId { get; set; }
		public string DigestHash { get; set; }
		public long DigestBlockId { get; set; }
		public string DigestTimestamp { get; set; }
		public long PublicDigestId { get; set; }
	}
}