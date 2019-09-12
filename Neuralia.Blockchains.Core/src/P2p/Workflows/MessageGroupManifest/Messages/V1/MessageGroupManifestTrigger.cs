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
		
		public int sessionId;

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.sessionId);

			dehydrator.Write(this.messageInfos.Count);

			foreach(var info in this.messageInfos) {
				info.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.sessionId = rehydrator.ReadInt();

			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				var gossipGroupMessageInfo = new GossipGroupMessageInfo<R>();
				gossipGroupMessageInfo.Rehydrate(rehydrator);
				this.messageInfos.Add(gossipGroupMessageInfo);
			}
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = new HashNodeList();

			nodesList.Add(base.GetStructuresArray());
			nodesList.Add(this.sessionId);

			nodesList.Add(this.messageInfos.Count);

			foreach(var entry in this.messageInfos.OrderBy(e => e.Hash)) {
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