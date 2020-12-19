using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Digest {
	public class ServerSendDigestInfo : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncInfoResponse<DigestChannelsInfoSet<DataSliceSize>, DataSliceSize, int, int> {
		public int Id { get; set; }
		public DigestChannelsInfoSet<DataSliceSize> SlicesSize { get; } = new DigestChannelsInfoSet<DataSliceSize>();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9(this.Id);
			adaptiveSet.Dehydrate(dehydrator);

			this.SlicesSize.Dehydrate(dehydrator);
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Id);

			nodeList.Add(this.SlicesSize);

			return nodeList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9();
			adaptiveSet.Rehydrate(rehydrator);
			this.Id = (int) adaptiveSet.Value;

			this.SlicesSize.Rehydrate(rehydrator);
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (ChainSyncMessageFactoryIds.SEND_DIGEST_INFO, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.CHAIN_SYNC;
		}
	}
}