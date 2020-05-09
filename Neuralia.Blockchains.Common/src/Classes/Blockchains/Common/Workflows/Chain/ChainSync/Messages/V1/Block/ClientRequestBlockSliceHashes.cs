using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Block {
	public abstract class ClientRequestBlockSliceHashes : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncSliceHashesRequest<BlockId> {

		public List<Dictionary<BlockChannelUtils.BlockChannelTypes, int>> Slices { get; } = new List<Dictionary<BlockChannelUtils.BlockChannelTypes, int>>();

		public BlockId Id { get; set; } = new BlockId();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.Id.Dehydrate(dehydrator);
			dehydrator.Write((ushort) this.Slices.Count);

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9();

			foreach(Dictionary<BlockChannelUtils.BlockChannelTypes, int> entry in this.Slices) {

				dehydrator.Write((byte) entry.Count);

				foreach(KeyValuePair<BlockChannelUtils.BlockChannelTypes, int> entry2 in entry) {

					dehydrator.Write((byte) entry2.Key);

					adaptiveSet.Value = entry2.Value;
					adaptiveSet.Dehydrate(dehydrator);
				}
			}
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Id);
			nodesList.Add(this.Slices.Count);

			foreach(Dictionary<BlockChannelUtils.BlockChannelTypes, int> entry in this.Slices) {
				nodesList.Add((byte) entry.Count);

				foreach(KeyValuePair<BlockChannelUtils.BlockChannelTypes, int> entry2 in entry.OrderBy(e => e.Key)) {

					nodesList.Add((byte) entry2.Key);
					nodesList.Add(entry2.Value);
				}
			}

			return nodesList;
		}

		public byte RequestAttempt { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.Id.Rehydrate(rehydrator);
			int count = rehydrator.ReadUShort();

			this.Slices.Clear();

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9();

			for(int i = 0; i < count; i++) {

				int count2 = rehydrator.ReadByte();

				Dictionary<BlockChannelUtils.BlockChannelTypes, int> channels = new Dictionary<BlockChannelUtils.BlockChannelTypes, int>();

				for(int j = 0; j < count2; j++) {

					BlockChannelUtils.BlockChannelTypes key = (BlockChannelUtils.BlockChannelTypes) rehydrator.ReadByte();

					adaptiveSet.Rehydrate(rehydrator);

					channels.Add(key, (int) adaptiveSet.Value);
				}

				this.Slices.Add(channels);
			}
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (ChainSyncMessageFactoryIds.REQUEST_BLOCK_SLICE_HASHES, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.CHAIN_SYNC;
		}
	}
}