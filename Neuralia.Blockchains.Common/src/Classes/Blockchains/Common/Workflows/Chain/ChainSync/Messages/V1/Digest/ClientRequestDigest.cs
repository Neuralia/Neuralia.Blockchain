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
	public abstract class ClientRequestDigest : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataRequest<DigestChannelsInfoSet<DataSliceInfo>, DataSliceInfo, int, int> {
		public int Id { get; set; }

		public DigestChannelsInfoSet<DataSliceInfo> SlicesInfo { get; } = new DigestChannelsInfoSet<DataSliceInfo>();

		/// <summary>
		///     How many tries have we attempted. we use this field to inform our peer, and play nice so they dont ban us for being
		///     abusive.
		/// </summary>
		public byte RequestAttempt { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9(this.Id);
			adaptiveSet.Dehydrate(dehydrator);
			
			dehydrator.Write(this.RequestAttempt);

			this.SlicesInfo.Dehydrate(dehydrator);
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Id);
			nodesList.Add(this.RequestAttempt);

			nodesList.Add(this.SlicesInfo);

			return nodesList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9();
			adaptiveSet.Rehydrate(rehydrator);
			this.Id = (int)adaptiveSet.Value;
			
			this.RequestAttempt = rehydrator.ReadByte();

			this.SlicesInfo.Rehydrate(rehydrator);
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (ChainSyncMessageFactoryIds.REQUEST_DIGEST, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.CHAIN_SYNC;
		}
	}
}