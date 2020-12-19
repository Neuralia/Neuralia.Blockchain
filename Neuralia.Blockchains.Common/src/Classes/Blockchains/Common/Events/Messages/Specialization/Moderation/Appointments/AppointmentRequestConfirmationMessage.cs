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
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments {
	public interface IAppointmentRequestConfirmationMessage : IModeratorBlockchainMessageCompressed, IAppointmentBlockchainMessage{
		List<AppointmentRequestConfirmationMessage.AppointmentRequester> Requesters           { get; }
		DateTime                                                         AppointmentTimestamp { get; set; }
		int                                                              Preparation          { get; set; }
		int                                                              Finalization         { get; set; }
	}

	public abstract class AppointmentRequestConfirmationMessage : ModeratorBlockchainMessageCompressed, IAppointmentRequestConfirmationMessage {

		public List<AppointmentRequester> Requesters           { get; } = new List<AppointmentRequester>();
		public DateTime                   AppointmentTimestamp { get; set; }
		public int                        Preparation          { get; set; }
		public int                        Finalization         { get; set; }

		public abstract class AppointmentRequester : ISerializableCombo {
			public Guid RequesterId { get; set; }
			
			/// <summary>
			/// The assigned public appointment index in the session
			/// </summary>
			public int Index { get; set; }
			
			/// <summary>
			/// the encrypted secret appointmentId Guid
			/// </summary>
			public SafeArrayHandle AppointmentId { get; set; }
			
			public void Rehydrate(IDataRehydrator rehydrator) {

				this.RequesterId = rehydrator.ReadGuid();
				
				this.AppointmentId = (SafeArrayHandle)rehydrator.ReadArray();
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				tool.Rehydrate(rehydrator);
				this.Index = tool.Value;
			}
			
			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.RequesterId);
				dehydrator.Write(this.AppointmentId);
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				tool.Value = this.Index;
				tool.Dehydrate(dehydrator);
			}

			public HashNodeList GetStructuresArray() {
				HashNodeList nodesList = new HashNodeList();

				nodesList.Add(this.RequesterId);
				nodesList.Add(this.AppointmentId);
				nodesList.Add(this.Index);

				return nodesList;
			}

			public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			
				jsonDeserializer.SetProperty("RequesterId", this.RequesterId);
				jsonDeserializer.SetProperty("AppointmentId", this.AppointmentId);
				jsonDeserializer.SetProperty("Index", this.Index);
			}
		}

		protected abstract AppointmentRequester CreateAppointmentRequester();
		
		protected override void RehydrateCompressedContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateCompressedContents(rehydrator, rehydrationFactory);

			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();

			this.AppointmentTimestamp = rehydrator.ReadDateTime();
			
			tool.Rehydrate(rehydrator);
			this.Preparation = tool.Value;
			
			tool.Rehydrate(rehydrator);
			this.Finalization = tool.Value;
			
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
			
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();

			dehydrator.Write(this.AppointmentTimestamp);
			
			tool.Value = this.Preparation;
			tool.Dehydrate(dehydrator);
			
			tool.Value = this.Finalization;
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
			nodesList.Add(this.Preparation);
			nodesList.Add(this.Finalization);
			
			nodesList.Add(this.Requesters.Count);
			foreach(var entry in this.Requesters) {
				nodesList.Add(entry);
			}

			return nodesList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("AppointmentTimestamp", this.AppointmentTimestamp);
			jsonDeserializer.SetProperty("Preparation", this.Preparation);
			jsonDeserializer.SetProperty("Finalization", this.Finalization);
			
			jsonDeserializer.SetArray("Requesters", this.Requesters);
		}
		
		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.APPOINTMENT_REQUEST_CONFIRMATION, 1, 0);
		}
	}
}