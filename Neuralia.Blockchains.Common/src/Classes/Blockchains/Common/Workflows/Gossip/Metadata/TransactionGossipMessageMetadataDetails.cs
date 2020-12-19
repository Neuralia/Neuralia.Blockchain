using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets.GossipMessageMetadatas;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.Metadata {
	public class TransactionGossipMessageMetadataDetails : IGossipMessageMetadataDetails {
		
		public const int TYPE = 3;
		public TransactionGossipMessageMetadataDetails() {
		}

		public TransactionGossipMessageMetadataDetails(bool isPresentation) {
			this.IsPresentation = isPresentation;
		}

		public bool IsPresentation { get; set; }
		

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.IsPresentation = rehydrator.ReadBool();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.IsPresentation);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.IsPresentation);

			return hashNodeList;
		}

		public byte Type => TYPE;
	}
}