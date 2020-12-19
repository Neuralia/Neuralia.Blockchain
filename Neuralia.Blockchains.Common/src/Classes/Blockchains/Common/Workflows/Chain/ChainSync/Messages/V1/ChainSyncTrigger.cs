using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1 {
	public abstract class ChainSyncTrigger : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory> {

		public DateTime ChainInception { get; set; }

		public long DiskBlockHeight { get; set; }
		public int DigestHeight { get; set; }

		public NodeShareType ShareType { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.ChainInception);
			dehydrator.Write(this.DiskBlockHeight);
			dehydrator.Write(this.DigestHeight);
			dehydrator.Write(this.ShareType);
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.ChainInception = rehydrator.ReadDateTime();
			this.DiskBlockHeight = rehydrator.ReadLong();
			this.DigestHeight = rehydrator.ReadInt();
			this.ShareType = rehydrator.ReadByte();
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();
			nodeList.Add(this.ChainInception);
			nodeList.Add(this.DiskBlockHeight);
			nodeList.Add(this.DigestHeight);
			nodeList.Add((byte) this.ShareType);

			return nodeList;
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (ChainSyncMessageFactoryIds.TRIGGER_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.CHAIN_SYNC;
		}
	}
}