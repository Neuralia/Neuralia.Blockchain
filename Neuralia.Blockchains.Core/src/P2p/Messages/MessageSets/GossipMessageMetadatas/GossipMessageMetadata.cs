using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.MessageSets.GossipMessageMetadatas {
	public class GossipMessageMetadata : IGossipMessageMetadata {

		public GossipMessageMetadata() {
			this.Type = 1;
		}

		public GossipMessageMetadata(IGossipMessageMetadataDetails gossipMessageMetadataDetails, BlockchainType blockchainType) {
			this.GossipMessageMetadataDetails = gossipMessageMetadataDetails;
			this.Type = gossipMessageMetadataDetails.Type;
			this.BlockchainType = blockchainType;
		}

		public byte Type { get; protected set; }
		public BlockchainType BlockchainType { get; set; }
		public IGossipMessageMetadataDetails GossipMessageMetadataDetails { get; set; }

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			this.Type = rehydrator.ReadByte();
			this.BlockchainType = rehydrator.ReadUShort();

			bool isNMetadataNull = rehydrator.ReadBool();

			if(!isNMetadataNull) {

				// lets invoke the right rehydratio factory for the required chain
				if(ServerMessageGroupManifestWorkflow.ChainRehydrationFactories.ContainsKey(this.BlockchainType)) {
					this.GossipMessageMetadataDetails = ServerMessageGroupManifestWorkflow.ChainRehydrationFactories[this.BlockchainType].RehydrateGossipMessageMetadataDetails(this.Type, rehydrator);
				}
			}
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.Type);
			dehydrator.Write(this.BlockchainType.Value);

			dehydrator.Write(this.GossipMessageMetadataDetails == null);

			this.GossipMessageMetadataDetails?.Dehydrate(dehydrator);
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Type);
			hashNodeList.Add(this.BlockchainType.Value);
			hashNodeList.Add(this.GossipMessageMetadataDetails);

			return hashNodeList;
		}
	}
}