using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Block {
	public abstract class ClientRequestBlock : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataRequest<BlockChannelsInfoSet<DataSliceInfo>, DataSliceInfo, long, BlockChannelUtils.BlockChannelTypes> {

		/// <summary>
		///     Should the response also contain the data of the next block in line?
		/// </summary>
		public bool IncludeNextInfo { get; set; }

		public long Id { get; set; }

		public BlockChannelsInfoSet<DataSliceInfo> SlicesInfo { get; } = new BlockChannelsInfoSet<DataSliceInfo>();

		/// <summary>
		///     How many tries have we attempted. we use this field to inform our peer, and play nice so they dont ban us for being
		///     abusive.
		/// </summary>
		public byte RequestAttempt { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Id);
			dehydrator.Write(this.IncludeNextInfo);
			dehydrator.Write(this.RequestAttempt);

			this.SlicesInfo.Dehydrate(dehydrator);
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Id);
			nodesList.Add(this.IncludeNextInfo);
			nodesList.Add(this.RequestAttempt);

			nodesList.Add(this.SlicesInfo);

			return nodesList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.Id = rehydrator.ReadLong();
			this.IncludeNextInfo = rehydrator.ReadBool();
			this.RequestAttempt = rehydrator.ReadByte();

			this.SlicesInfo.Rehydrate(rehydrator);
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (ChainSyncMessageFactoryIds.REQUEST_BLOCK, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.CHAIN_SYNC;
		}
	}
}