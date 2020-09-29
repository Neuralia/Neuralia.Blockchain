using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets.GossipMessageMetadatas;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.Metadata {

	/// <summary>
	///     a special metadata type to convey the block Id in the gossip message
	/// </summary>
	public class BlockchainMessageGossipMessageMetadataDetails : IGossipMessageMetadataDetails {
		public const int TYPE = 2;

		public BlockchainMessageGossipMessageMetadataDetails() {
		}

		public BlockchainMessageGossipMessageMetadataDetails(long blockId) {
			this.BlockId = blockId;
		}

		public long BlockId { get; set; }

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.BlockId = rehydrator.ReadLong();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.BlockId);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.BlockId);

			return hashNodeList;
		}

		public byte Type => TYPE;
	}
}