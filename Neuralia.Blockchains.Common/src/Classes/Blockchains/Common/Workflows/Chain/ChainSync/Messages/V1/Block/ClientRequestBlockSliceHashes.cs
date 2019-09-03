using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Block {
	public abstract class ClientRequestBlockSliceHashes : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncSliceHashesRequest<long> {

		public List<Dictionary<BlockChannelUtils.BlockChannelTypes, int>> Slices { get; } = new List<Dictionary<BlockChannelUtils.BlockChannelTypes, int>>();

		public long Id { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Id);
			dehydrator.Write((ushort) this.Slices.Count);

			foreach(var entry in this.Slices) {

				dehydrator.Write((byte) entry.Count);

				foreach(var entry2 in entry) {

					dehydrator.Write((byte) entry2.Key);
					dehydrator.Write(entry2.Value);
				}
			}
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Id);
			nodesList.Add(this.Slices.Count);

			foreach(var entry in this.Slices) {
				nodesList.Add((byte) entry.Count);

				foreach(var entry2 in entry.OrderBy(e => e.Key)) {

					nodesList.Add((byte) entry2.Key);
					nodesList.Add(entry2.Value);
				}
			}

			return nodesList;
		}

		public byte RequestAttempt { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.Id = rehydrator.ReadLong();
			int count = rehydrator.ReadUShort();

			this.Slices.Clear();

			for(int i = 0; i < count; i++) {

				int count2 = rehydrator.ReadByte();

				var channels = new Dictionary<BlockChannelUtils.BlockChannelTypes, int>();

				for(int j = 0; j < count2; j++) {

					BlockChannelUtils.BlockChannelTypes key = (BlockChannelUtils.BlockChannelTypes) rehydrator.ReadByte();
					int length = rehydrator.ReadInt();

					channels.Add(key, length);
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