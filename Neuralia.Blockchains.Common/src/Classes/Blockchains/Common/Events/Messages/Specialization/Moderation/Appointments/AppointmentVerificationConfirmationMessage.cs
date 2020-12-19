using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments {
	public interface IAppointmentVerificationConfirmationMessage : IModeratorBlockchainMessageCompressed, IAppointmentBlockchainMessage {
		List<AppointmentVerificationConfirmationMessage.AppointmentRequester> Requesters { get; }
		DateTime AppointmentTimestamp { get; set; }
		int VerificationSpan { get; set; }
	}

	public abstract class AppointmentVerificationConfirmationMessage : ModeratorBlockchainMessageCompressed, IAppointmentVerificationConfirmationMessage{

		public List<AppointmentRequester> Requesters { get; } = new List<AppointmentRequester>();
		public DateTime AppointmentTimestamp { get; set; }
		public int VerificationSpan { get; set; }
		
		public abstract class AppointmentRequester : ISerializableCombo {
			public Guid RequesterId { get; set; }
			public SafeArrayHandle AppointmentConfirmationCode { get; set; }
			
			public void Rehydrate(IDataRehydrator rehydrator) {

				this.RequesterId = rehydrator.ReadGuid();
				
				this.AppointmentConfirmationCode = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			}
			
			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.RequesterId);
				dehydrator.WriteNonNullable(this.AppointmentConfirmationCode);
			}

			public HashNodeList GetStructuresArray() {
				HashNodeList nodesList = new HashNodeList();
				
				nodesList.Add(this.RequesterId);
				nodesList.Add(this.AppointmentConfirmationCode);

				return nodesList;
			}

			public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			
				jsonDeserializer.SetProperty("RequesterId", this.RequesterId);
				jsonDeserializer.SetProperty("AppointmentId", this.AppointmentConfirmationCode);
			}
		}

		protected abstract AppointmentRequester CreateAppointmentRequester();
		
		protected override void RehydrateCompressedContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateCompressedContents(rehydrator, rehydrationFactory);

			this.AppointmentTimestamp = rehydrator.ReadDateTime();
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			tool.Rehydrate(rehydrator);
			this.VerificationSpan = tool.Value;
			
			this.Requesters.Clear();

			tool.Rehydrate(rehydrator);
			int count = tool.Value;

			for(int i = 0; i < count; i++) {
				var requester = this.CreateAppointmentRequester();

				requester.Rehydrate(rehydrator);
				
				this.Requesters.Add(requester);
			}
		}

		protected override void DehydrateCompressedContents(IDataDehydrator dehydrator) {
			base.DehydrateCompressedContents(dehydrator);
			dehydrator.Write(this.AppointmentTimestamp);
			
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			tool.Value = this.VerificationSpan;
			tool.Dehydrate(dehydrator);
			
			tool.Value = this.Requesters.Count;
			tool.Dehydrate(dehydrator);
			
			foreach(var entry in this.Requesters) {
				entry.Dehydrate(dehydrator);
			}
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.AppointmentTimestamp);
			nodesList.Add(this.VerificationSpan);
			
			nodesList.Add(this.Requesters.Count);
			foreach(var entry in this.Requesters) {
				nodesList.Add(entry);
			}

			return nodesList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("AppointmentTimestamp", this.AppointmentTimestamp);
			jsonDeserializer.SetProperty("VerificationSpan", this.VerificationSpan);
			
			jsonDeserializer.SetArray("Requesters", this.Requesters);
		}
		
		
		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.APPOINTMENT_VERIFICATION_CONFIRMATION, 1, 0);
		}
	}
}