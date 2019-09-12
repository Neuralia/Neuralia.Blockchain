using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Block {
	public class ServerSendBlockInfo : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncInfoResponse<BlockChannelsInfoSet<DataSliceSize>, DataSliceSize, long, BlockChannelUtils.BlockChannelTypes> {
		public IByteArray BlockHash { get; set; }

		/// <summary>
		///     The last block we have in our chain. we send it every time, as this number changes as we sync locally
		/// </summary>
		public long ChainBlockHeight { get; set; }

		public bool HasBlockDetails { get; set; }
		public long Id { get; set; }

		public BlockChannelsInfoSet<DataSliceSize> SlicesSize { get; } = new BlockChannelsInfoSet<DataSliceSize>();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Id);

			dehydrator.Write(this.ChainBlockHeight);

			dehydrator.Write(this.HasBlockDetails);

			if(this.HasBlockDetails) {
				dehydrator.WriteNonNullable(this.BlockHash);
				this.SlicesSize.Dehydrate(dehydrator);
			}

		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = new HashNodeList();

			nodesList.Add(base.GetStructuresArray());

			nodesList.Add(this.Id);

			nodesList.Add(this.ChainBlockHeight);

			nodesList.Add(this.HasBlockDetails);

			if(this.HasBlockDetails) {
				nodesList.Add(this.BlockHash);
			}

			nodesList.Add(this.SlicesSize);

			return nodesList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.Id = rehydrator.ReadLong();

			this.ChainBlockHeight = rehydrator.ReadLong();

			this.HasBlockDetails = rehydrator.ReadBool();

			if(this.HasBlockDetails) {
				this.BlockHash = rehydrator.ReadNonNullableArray();
				this.SlicesSize.Rehydrate(rehydrator);
			}
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (ChainSyncMessageFactoryIds.SEND_BLOCK_INFO, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.CHAIN_SYNC;
		}
		
		protected override void DisposeAll() {
			base.DisposeAll();
			
		}
	}
}