using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments {
	
	public interface IAppointmentSliceMessage : IModeratorBlockchainMessage {
		DateTime Appointment { get; set; }
		int Range { get; set; }
		int Slice { get; set; }
		(long start, long end) ComputeRange();
	}
	
	public abstract class AppointmentSliceMessage : ModeratorBlockchainMessage, IAppointmentSliceMessage{
		public DateTime Appointment { get; set; }
		public int Range { get; set; }
		public int Slice { get; set; }

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			this.Appointment = rehydrator.ReadDateTime();
			
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			tool.Rehydrate(rehydrator);
			this.Range = (int)tool.Value;
			
			tool.Rehydrate(rehydrator);
			this.Slice = (int)tool.Value;
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);
			dehydrator.Write(this.Appointment);
			
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
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
		public (long start, long end) ComputeRange() {
			return (((this.Slice - 1) * (long)this.Range)+1, this.Slice * (long)this.Range);
		}

	}
}