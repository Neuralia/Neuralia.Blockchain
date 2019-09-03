using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages.V1 {
	public class ServerHandshakeConfirm<R> : PeerListHandlingMessage<R>
		where R : IRehydrationFactory {

		public enum HandshakeConfirmationStatuses : byte {
			Ok = 0,
			CanGoNoFurther = 1,
			Rejected = 2,
			Error = byte.MaxValue
		}

		public long nonce;

		public HandshakeConfirmationStatuses Status;

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.nonce);
			dehydrator.Write((byte) this.Status);

		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.nonce = rehydrator.ReadLong();
			this.Status = (HandshakeConfirmationStatuses) rehydrator.ReadByte();

		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add((byte) this.Status);
			nodesList.Add(this.nonce);

			return nodesList;
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (HandshakeMessageFactory<R>.SERVER_CONFIRM_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.HANDSHAKE;
		}
	}
}