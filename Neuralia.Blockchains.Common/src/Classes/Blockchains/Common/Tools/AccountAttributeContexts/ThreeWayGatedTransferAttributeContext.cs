using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.AccountAttributeContexts {
	public abstract class ThreeWayGatedTransferAttributeContext : TransferAttributeContextBase {
		public enum Roles : byte {
			Sender = 1,
			Receiver = 2,
			Verifier = 3
		}

		public Roles Role { get; set; } = Roles.Sender;

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.Role = rehydrator.ReadByteEnum<Roles>();
		}

		protected override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.Role);
		}
	}
}