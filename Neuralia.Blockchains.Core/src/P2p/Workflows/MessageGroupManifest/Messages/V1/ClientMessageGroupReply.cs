using System.Collections.Generic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1 {
	public class ClientMessageGroupReply<R> : NetworkMessage<R>
		where R : IRehydrationFactory {

		public List<IByteArray> gossipMessageSets = new List<IByteArray>();
		
		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.gossipMessageSets.Count);

			foreach(IByteArray messageSet in this.gossipMessageSets) {
				dehydrator.WriteNonNullable(messageSet);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				this.gossipMessageSets.Add(rehydrator.ReadNonNullableArray());
			}
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (MessageGroupManifestMessageFactory<R>.CLIENT_MESSAGE_GROUP_REPLY_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.MESSAGE_GROUP_MANIFEST;
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			foreach(var entry in gossipMessageSets) {
				entry.Dispose();
			}
		}
	}
}