using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments {
	
	public interface IAppointmentSliceMessage : IModeratorBlockchainMessageCompressed, IAppointmentBlockchainMessage {
		DateTime             Appointment { get; set; }
		int                  Range       { get; set; }
		int                  Slice       { get; set; }
		(int start, int end) ComputeRange();
	}
	
	public abstract class AppointmentSliceMessage : ModeratorBlockchainMessageCompressed, IAppointmentSliceMessage{
		public DateTime Appointment { get; set; }
		public int Range { get; set; }
		public int Slice { get; set; }

		protected override void RehydrateCompressedContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateCompressedContents(rehydrator, rehydrationFactory);

			this.Appointment = rehydrator.ReadDateTime();
			
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			tool.Rehydrate(rehydrator);
			this.Range = tool.Value;
			
			tool.Rehydrate(rehydrator);
			this.Slice = tool.Value;
		}

		protected override void DehydrateCompressedContents(IDataDehydrator dehydrator) {
			base.DehydrateCompressedContents(dehydrator);
			dehydrator.Write(this.Appointment);
			
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			tool.Value = this.Range;
			tool.Dehydrate(dehydrator);
			
			tool.Value = this.Slice;
			tool.Dehydrate(dehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Appointment);
			nodesList.Add(this.Range);
			nodesList.Add(this.Slice);
			
			return nodesList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("AppointmentTimestamp", this.Appointment);
			
			jsonDeserializer.SetProperty("Slice", this.Range);
			jsonDeserializer.SetProperty("Total", this.Slice);
			
		}

		/// <summary>
		/// get the effective range included in this message
		/// </summary>
		/// <returns></returns>
		public (int start, int end) ComputeRange() {
			return (((this.Slice - 1) * this.Range) +1, this.Slice * this.Range);
		}

	}
}