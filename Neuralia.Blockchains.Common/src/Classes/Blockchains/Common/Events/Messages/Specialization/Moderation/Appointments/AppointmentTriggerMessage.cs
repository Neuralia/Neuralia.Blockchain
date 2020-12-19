using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments {
	public interface IAppointmentTriggerMessage : IModeratorBlockchainMessage, IAppointmentBlockchainMessage {
		DateTime Appointment { get; set; }
		SafeArrayHandle Key { get; set; }
	}

	public abstract class AppointmentTriggerMessage : ModeratorBlockchainMessage, IAppointmentTriggerMessage{

		public DateTime Appointment { get; set; }
		public SafeArrayHandle Key { get; set; }

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			this.Appointment = rehydrator.ReadDateTime();
			this.Key = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			dehydrator.Write(this.Appointment);
			dehydrator.WriteNonNullable(this.Key);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Appointment);
			nodesList.Add(this.Key);
			
			return nodesList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
		}
		
		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.APPOINTMENT_TRIGGER, 1, 0);
		}
	}
}