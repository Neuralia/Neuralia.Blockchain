using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments {
	public interface IAppointmentRequestMessage : IAppointmentBlockchainMessage {
		Guid RequesterId { get; set; }
		SafeArrayHandle ContactPublicKey { get; set; }
		int PreferredRegion { get; set; } 
		
	}

	public abstract class AppointmentRequestMessage : BlockchainMessage, IAppointmentRequestMessage {

		public Guid RequesterId { get; set; } = Guid.Empty;
		public SafeArrayHandle ContactPublicKey { get; set; }
		public int PreferredRegion { get; set; } 
		
		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			this.RequesterId = rehydrator.ReadGuid();
			this.ContactPublicKey = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			tool.Rehydrate(rehydrator);
			this.PreferredRegion = (int)tool.Value;
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			dehydrator.Write(this.RequesterId);
			dehydrator.WriteNonNullable(this.ContactPublicKey);
			
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			tool.Value = this.PreferredRegion;
			tool.Dehydrate(dehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.RequesterId);
			nodesList.Add(this.ContactPublicKey);
			nodesList.Add(this.PreferredRegion);

			return nodesList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("RequesterId", this.RequesterId);
			jsonDeserializer.SetProperty("PublicKey", this.ContactPublicKey);
			jsonDeserializer.SetProperty("PreferredRegion", this.PreferredRegion);
		}
		
		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.APPOINTMENT_REQUESTED, 1, 0);
		}
	}
}