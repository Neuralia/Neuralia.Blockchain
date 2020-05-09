using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Block {
	public class ServerRequestBlockSliceHashes : NetworkMessage<IBlockchainEventsRehydrationFactory>, ISyncSliceHashesResponse<BlockId> {

		public int SlicesHash { get; set; }

		public BlockId Id { get; set; } = new BlockId();

		public List<int> SliceHashes { get; } = new List<int>();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.Id.Dehydrate(dehydrator);

			dehydrator.Write(this.SlicesHash);
			dehydrator.Write((ushort) this.SliceHashes.Count);

			foreach(int entry in this.SliceHashes) {
				dehydrator.Write(entry);
			}

		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = new HashNodeList();

			nodesList.Add(base.GetStructuresArray());

			nodesList.Add(this.Id);

			nodesList.Add(this.SlicesHash);
			nodesList.Add(this.SliceHashes.Count);

			foreach(int entry in this.SliceHashes.OrderBy(s => s)) {
				nodesList.Add(entry);
			}

			return nodesList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.Id.Rehydrate(rehydrator);
			this.SlicesHash = rehydrator.ReadInt();

			int count = rehydrator.ReadUShort();

			this.SliceHashes.Clear();

			for(int i = 0; i < count; i++) {

				int hash = rehydrator.ReadInt();

				this.SliceHashes.Add(hash);
			}
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (ChainSyncMessageFactoryIds.SEND_BLOCK_SLICE_HASHES, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.CHAIN_SYNC;
		}
	}
}