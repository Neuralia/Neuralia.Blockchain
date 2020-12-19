using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Workflows.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest.Messages.V1 {
	public class AppointmentRequestServerReply : NetworkMessage<IBlockchainEventsRehydrationFactory> {

		public SafeArrayHandle Message { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(Message);
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.Message = (SafeArrayHandle)rehydrator.ReadArray();
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Message);
			
			return nodesList;
		}
		
		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (AppointmentRequestMessageFactoryIds.SERVER_TRIGGER_REPLY, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.APPOINTMENT_REQUEST;
		}
	}
}