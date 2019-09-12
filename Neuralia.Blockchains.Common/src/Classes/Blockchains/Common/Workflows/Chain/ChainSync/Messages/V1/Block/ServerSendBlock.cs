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
	public abstract class ServerSendBlock : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncDataResponse<BlockChannelsInfoSet<DataSlice>, DataSlice, long, BlockChannelUtils.BlockChannelTypes> {

		public bool HasNextInfo { get; set; }

		/// <summary>
		///     The ID of the next block in line
		/// </summary>
		public long NextBlockHeight { get; set; }

		/// <summary>
		///     The last block we have in our chain. we send it every time, as this number changes as we sync locally
		/// </summary>
		public long ChainBlockHeight { get; set; }

		public IByteArray NextBlockHash { get; set; }

		public BlockChannelsInfoSet<DataSliceSize> NextBlockChannelSizes { get; } = new BlockChannelsInfoSet<DataSliceSize>();

		/// <summary>
		///     How many tries have we attempted. we use this field to inform our peer, and play nice so they dont ban us for being
		///     abusive.
		/// </summary>
		public byte RequestAttempt { get; set; }

		public long Id { get; set; }
		public BlockChannelsInfoSet<DataSlice> Slices { get; } = new BlockChannelsInfoSet<DataSlice>();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Id);
			dehydrator.Write(this.RequestAttempt);

			this.Slices.Dehydrate(dehydrator);

			dehydrator.Write(this.ChainBlockHeight);

			dehydrator.Write(this.HasNextInfo);

			if(this.HasNextInfo) {
				dehydrator.Write(this.NextBlockHeight);
				dehydrator.WriteNonNullable(this.NextBlockHash);
				this.NextBlockChannelSizes.Dehydrate(dehydrator);
			}
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Id);
			nodesList.Add(this.RequestAttempt);

			nodesList.Add(this.Slices);

			nodesList.Add(this.ChainBlockHeight);

			nodesList.Add(this.HasNextInfo);

			if(this.HasNextInfo) {
				nodesList.Add(this.NextBlockHeight);
				nodesList.Add(this.NextBlockHash);
				nodesList.Add(this.NextBlockChannelSizes);
			}

			return nodesList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.Id = rehydrator.ReadLong();
			this.RequestAttempt = rehydrator.ReadByte();

			this.Slices.Rehydrate(rehydrator);

			this.ChainBlockHeight = rehydrator.ReadLong();

			this.HasNextInfo = rehydrator.ReadBool();

			if(this.HasNextInfo) {
				this.NextBlockHeight = rehydrator.ReadLong();
				this.NextBlockHash = rehydrator.ReadNonNullableArray();
				this.NextBlockChannelSizes.Rehydrate(rehydrator);
			}
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (ChainSyncMessageFactoryIds.SEND_BLOCK, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.CHAIN_SYNC;
		}

		protected override void DisposeAll() {
			base.DisposeAll();
			
		}
	}
}