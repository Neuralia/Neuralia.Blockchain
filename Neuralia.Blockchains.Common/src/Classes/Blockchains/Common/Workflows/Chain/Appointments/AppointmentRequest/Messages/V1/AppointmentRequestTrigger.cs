using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest.Messages.V1 {
	public class AppointmentRequestTrigger : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory>{

		
		public Guid?                         RequesterId    { get; set; }
		public int?                          RequesterIndex { get; set; }
		public DateTime?                     Appointment    { get; set; }
		public Enums.AppointmentRequestModes Mode { get; set; } = Enums.AppointmentRequestModes.Unknown;
		public BlockchainType                ChainType      { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.RequesterId);
			dehydrator.Write(this.RequesterIndex);
			dehydrator.Write(this.Appointment);
			dehydrator.Write((byte)this.Mode);
			dehydrator.Write(this.ChainType.Value);
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.RequesterId = rehydrator.ReadNullableGuid();
			this.RequesterIndex = rehydrator.ReadNullableInt();
			this.Appointment = rehydrator.ReadNullableDateTime();
			this.Mode = rehydrator.ReadByteEnum<Enums.AppointmentRequestModes>();
			this.ChainType = rehydrator.ReadUShort();
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (AppointmentRequestMessageFactoryIds.TRIGGER_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.APPOINTMENT_REQUEST;
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = new HashNodeList();

			nodesList.Add(base.GetStructuresArray());
			nodesList.Add(this.RequesterId);
			nodesList.Add(this.RequesterIndex);
			nodesList.Add(this.Appointment);
			nodesList.Add((byte)this.Mode);
			nodesList.Add(this.ChainType.Value);

			return nodesList;
		}
	}
}