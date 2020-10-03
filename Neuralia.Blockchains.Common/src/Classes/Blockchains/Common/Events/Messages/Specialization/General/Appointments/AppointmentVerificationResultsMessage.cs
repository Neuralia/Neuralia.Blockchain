using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments {
	
	public interface IAppointmentVerificationResultsMessage : IBlockchainMessage {
		DateTime Appointment { get; set; }
		List<AppointmentVerificationResultsMessage.RequesterResultEntry> Applicants  { get; }
	}

	public abstract class AppointmentVerificationResultsMessage : BlockchainMessage, IAppointmentVerificationResultsMessage{

		public DateTime Appointment { get; set; }
		public List<RequesterResultEntry> Applicants  { get; } = new List<RequesterResultEntry>();

		public class RequesterResultEntry : IBinarySerializable, ITreeHashable {

			public int Index { get; set; }
			public bool ConditionVerification  { get; set; }
			public Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> Results { get; set; }= new Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle>();

			public DateTime? CodeRequestTimestamp { get; set; }
			public DateTime TriggerTimestamp { get; set; }
			public DateTime CompletedTimestamp { get; set; }
			public int SecretCode { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {
				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				
				tool.Rehydrate(rehydrator);
				this.Index = (int)tool.Value;

				this.ConditionVerification = rehydrator.ReadBool();
				
				this.CodeRequestTimestamp = rehydrator.ReadNullableDateTime();
				this.TriggerTimestamp = rehydrator.ReadDateTime();
				this.CompletedTimestamp = rehydrator.ReadDateTime();
				this.SecretCode = rehydrator.ReadInt();
				
				int count = rehydrator.ReadInt();

				this.Results.Clear();
				for(int i = 0; i < count; i++) {
					
					tool.Rehydrate(rehydrator);
					var key = (Enums.AppointmentsResultTypes)tool.Value;
					var value = (SafeArrayHandle)rehydrator.ReadArray();
					
					this.Results.Add(key, value);
				}
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				
				AdaptiveLong1_9 tool = new AdaptiveLong1_9();

				tool.Value = this.Index;
				tool.Dehydrate(dehydrator);

				dehydrator.Write(this.ConditionVerification);
				
				dehydrator.Write(this.CodeRequestTimestamp);
				dehydrator.Write(this.TriggerTimestamp);
				dehydrator.Write(this.CompletedTimestamp);
				dehydrator.Write(this.SecretCode);
				
				dehydrator.Write(this.Results.Count);

				foreach((Enums.AppointmentsResultTypes key, SafeArrayHandle value) in this.Results.OrderBy(r => (int)r.Key)) {

					tool.Value = (int)key;
					tool.Dehydrate(dehydrator);
					
					dehydrator.Write(value);
				}
			}

			public HashNodeList GetStructuresArray() {
				HashNodeList nodesList = new HashNodeList();

				nodesList.Add(this.Index);
				nodesList.Add(this.ConditionVerification);
				nodesList.Add(this.CodeRequestTimestamp);
				nodesList.Add(this.TriggerTimestamp);
				nodesList.Add(this.CompletedTimestamp);
				nodesList.Add(this.CompletedTimestamp);
				
				nodesList.Add(this.Results.Count);

				foreach(var result in this.Results.OrderBy(r => (int)r.Key)) {
					nodesList.Add((byte)result.Key);
					nodesList.Add(result.Value);
				}

				return nodesList;
			}
		}
		
		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			this.Appointment = rehydrator.ReadDateTime();
			int count = rehydrator.ReadInt();

			this.Applicants.Clear();
			for(int i = 0; i < count; i++) {
				RequesterResultEntry applicant = new RequesterResultEntry();
				applicant.Rehydrate(rehydrator);
				this.Applicants.Add(applicant);
			}
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			dehydrator.Write(this.Appointment);
			
			dehydrator.Write(this.Applicants.Count);

			foreach(var applicant in this.Applicants.OrderBy(a => a.Index)) {
				applicant.Dehydrate(dehydrator);
			}
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Appointment);
			nodesList.Add(this.Applicants.Count);

			foreach(var applicant in this.Applicants) {
				nodesList.Add(applicant);
			}

			return nodesList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("Appointment", this.Appointment);
			jsonDeserializer.SetArray("Applicants", this.Applicants);
		}
		
		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.APPOINTMENT_VERIFICATION_RESULTS, 1, 0);
		}
	}
}