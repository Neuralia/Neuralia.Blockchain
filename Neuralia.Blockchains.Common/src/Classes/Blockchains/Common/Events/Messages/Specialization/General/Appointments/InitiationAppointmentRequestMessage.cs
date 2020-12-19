using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments {

	public interface IInitiationAppointmentRequestMessage : IAppointmentRequestMessage {
		Enums.AccountTypes WalletAccountType { get; set; } 
		SafeArrayHandle IdentityPublicKey { get; set; }

	}

	public abstract class InitiationAppointmentRequestMessage : AppointmentRequestMessage, IInitiationAppointmentRequestMessage {

		public Enums.AccountTypes WalletAccountType { get; set; } = Enums.AccountTypes.User;
		public SafeArrayHandle IdentityPublicKey { get; set; }

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);
			
			this.WalletAccountType = rehydrator.ReadByteEnum<Enums.AccountTypes>();
			this.IdentityPublicKey = (SafeArrayHandle)rehydrator.ReadNonNullableArray();

		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			dehydrator.Write((byte)this.WalletAccountType);
			dehydrator.WriteNonNullable(this.IdentityPublicKey);

		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.WalletAccountType);
			nodesList.Add(this.IdentityPublicKey);

			return nodesList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("WalletAccountType", this.WalletAccountType);
			jsonDeserializer.SetProperty("IdentityPublicKey", this.IdentityPublicKey);
		}
		
		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.INITIATION_APPOINTMENT_REQUESTED, 1, 0);
		}
	}
}