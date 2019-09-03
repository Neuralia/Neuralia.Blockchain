using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets.GossipMessageMetadatas;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1 {

	public interface IGossipGroupMessageInfo : ITreeHashable {
		long Hash { get; set; }

		IGossipMessageMetadata GossipMessageMetadata { get; set; }
	}

	public class GossipGroupMessageInfo<R> : IGossipGroupMessageInfo
		where R : IRehydrationFactory {

		public long Hash { get; set; }
		public IGossipMessageMetadata GossipMessageMetadata { get; set; }

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Hash);
			hashNodeList.Add(this.GossipMessageMetadata);

			return hashNodeList;
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			this.Hash = rehydrator.ReadLong();

			bool isNMetadataNull = rehydrator.ReadBool();

			if(!isNMetadataNull) {

				this.GossipMessageMetadata = new GossipMessageMetadata();
				this.GossipMessageMetadata.Rehydrate(rehydrator);
			}
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.Hash);

			dehydrator.Write(this.GossipMessageMetadata == null);

			this.GossipMessageMetadata?.Dehydrate(dehydrator);
		}
	}
}