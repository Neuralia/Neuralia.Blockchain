using System.Collections.Generic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1 {
	public class MessageGroupManifestServerReply<R> : NetworkMessage<R>
		where R : IRehydrationFactory {
		public readonly List<bool> messageApprovals = new List<bool>();

		public int sessionId;
		
		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.sessionId);
			dehydrator.Write(this.messageApprovals.Count);

			foreach(bool acceptation in this.messageApprovals) {
				dehydrator.Write(acceptation);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.sessionId = rehydrator.ReadInt();
			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				this.messageApprovals.Add(rehydrator.ReadBool());
			}
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (MessageGroupManifestMessageFactory<R>.SERVER_REPLY_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.MESSAGE_GROUP_MANIFEST;
		}
	}
}