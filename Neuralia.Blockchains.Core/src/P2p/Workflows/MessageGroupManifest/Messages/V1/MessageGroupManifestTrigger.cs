using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1 {
	public class MessageGroupManifestTrigger<R> : WorkflowTriggerMessage<R>
		where R : IRehydrationFactory {

		public readonly List<GossipGroupMessageInfo<R>> messageInfos = new List<GossipGroupMessageInfo<R>>();

		public int targettedMessageCount;

		/// <summary>
		///     if we have messages in this list, then it means there were no gossip messages, and thus the communication ends
		///     there
		/// </summary>
		/// <returns></returns>
		public List<IByteArray> targettedMessageSets = new List<IByteArray>();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.targettedMessageCount);

			dehydrator.Write(this.messageInfos.Count);

			foreach(var info in this.messageInfos) {
				info.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.targettedMessageSets.Count);

			foreach(IByteArray message in this.targettedMessageSets) {
				dehydrator.WriteNonNullable(message);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.targettedMessageCount = rehydrator.ReadInt();

			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				var gossipGroupMessageInfo = new GossipGroupMessageInfo<R>();
				gossipGroupMessageInfo.Rehydrate(rehydrator);
				this.messageInfos.Add(gossipGroupMessageInfo);
			}

			count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				this.targettedMessageSets.Add(rehydrator.ReadNonNullableArray());
			}
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = new HashNodeList();

			nodesList.Add(base.GetStructuresArray());
			nodesList.Add(this.targettedMessageCount);

			nodesList.Add(this.messageInfos.Count);

			foreach(var entry in this.messageInfos.OrderBy(e => e.Hash)) {
				nodesList.Add(entry);
			}

			nodesList.Add(this.targettedMessageSets.Count);

			foreach(IByteArray entry in this.targettedMessageSets) {
				nodesList.Add(entry);
			}

			return nodesList;
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (MessageGroupManifestMessageFactory<R>.TRIGGER_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.MESSAGE_GROUP_MANIFEST;
		}
	}
}