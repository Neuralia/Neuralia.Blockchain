using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1 {
	public abstract class ServerTriggerReply : NetworkMessage<IBlockchainEventsRehydrationFactory> {
		public enum SyncHandshakeStatuses : byte {
			Ok = 0,
			Synching = 1,
			NoTransactionDetails = 2,
			Refused = 3,
			Error = 4,
			Busy = 5
		}

		public SyncHandshakeStatuses Status { get; set; } = SyncHandshakeStatuses.Ok;

		public DateTime ChainInception { get; set; }

		public long DiskBlockHeight { get; set; }
		public long EarliestBlockHeight { get; set; }

		public int DigestHeight { get; set; }

		public NodeShareType ShareType { get; set; } = NodeShareType.None;

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.Status);

			dehydrator.Write(this.ChainInception);

			dehydrator.Write(this.DiskBlockHeight);
			dehydrator.Write(this.EarliestBlockHeight);
			dehydrator.Write(this.DigestHeight);
			dehydrator.Write(this.ShareType);
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.Status = rehydrator.ReadByteEnum<SyncHandshakeStatuses>();

			this.ChainInception = rehydrator.ReadDateTime();

			this.DiskBlockHeight = rehydrator.ReadLong();
			this.EarliestBlockHeight = rehydrator.ReadLong();
			this.DigestHeight = rehydrator.ReadInt();
			this.ShareType = rehydrator.ReadByteEnum<Enums.ChainSharingTypes>();
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add((byte) this.Status);
			nodeList.Add(this.ChainInception);

			nodeList.Add(this.DiskBlockHeight);
			nodeList.Add(this.EarliestBlockHeight);
			nodeList.Add(this.DigestHeight);
			nodeList.Add((byte) this.ShareType);

			return nodeList;
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (ChainSyncMessageFactoryIds.SERVER_TRIGGER_REPLY, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.CHAIN_SYNC;
		}
	}
}