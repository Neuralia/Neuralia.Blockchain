using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.AppointmentRequest.Messages {
	
	public static class AppointmentRequestMessageFactoryIds {
		public const ushort TRIGGER_ID = 1;
		public const ushort SERVER_TRIGGER_REPLY = 2;
	}

	public interface IAppointmentRequestMessageFactory {
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> Rehydrate(SafeArrayHandle data, TargettedHeader header, IBlockchainEventsRehydrationFactory rehydrationFactory);

		IBlockchainTriggerMessageSet CreateAppointmentRequestWorkflowTriggerSet(uint workflowCorrelationId);

		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateAppointmentRequestWorkflowTriggerServerReplySet(TargettedHeader triggerHeader = null);

		//
	}
	

	public class AppointmentRequestMessageFactory<APPOINTMENT_REQUEST_TRIGGER, SERVER_TRIGGER_REPLY> : ChainMessageFactory, IAppointmentRequestMessageFactory
		where APPOINTMENT_REQUEST_TRIGGER : AppointmentRequestTrigger, new()
		where SERVER_TRIGGER_REPLY : AppointmentRequestServerReply, new(){
		

		public AppointmentRequestMessageFactory(IMainChainMessageFactory mainChainMessageFactory, BlockchainServiceSet serviceSet) : base(mainChainMessageFactory, serviceSet) {
		}

		public override ITargettedMessageSet<IBlockchainEventsRehydrationFactory> Rehydrate(SafeArrayHandle data, TargettedHeader header, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			using IDataRehydrator dr = DataSerializationFactory.CreateRehydrator(data);

			using SafeArrayHandle messageBytes = NetworkMessageSet.ExtractMessageBytes(dr);
			NetworkMessageSet.ResetAfterHeader(dr);

			using IDataRehydrator messageRehydrator = DataSerializationFactory.CreateRehydrator(messageBytes);

			ITargettedMessageSet<IBlockchainEventsRehydrationFactory> messageSet = null;

			try {
				if(data?.Length == 0) {
					throw new ApplicationException("null message");
				}

				short workflowType = 0;
				ComponentVersion<SimpleUShort> version = null;

				messageRehydrator.Peek(rehydrator => {
					workflowType = rehydrator.ReadShort();

					if(workflowType != WorkflowIDs.APPOINTMENT_REQUEST) {
						throw new ApplicationException("Invalid workflow type");
					}

					version = rehydrator.Rehydrate<ComponentVersion<SimpleUShort>>();
				});

				switch(version.Type.Value) {
					case AppointmentRequestMessageFactoryIds.TRIGGER_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateAppointmentRequestWorkflowTriggerSet(header);
						}

						break;

					case AppointmentRequestMessageFactoryIds.SERVER_TRIGGER_REPLY:

						if(version == (1, 0)) {
							messageSet = this.CreateAppointmentRequestWorkflowTriggerServerReplySet(header);
						}

						break;

					default:

						throw new ApplicationException("invalid message type");
				}

				if(messageSet?.BaseMessage == null) {
					throw new ApplicationException("Invalid message type or major");
				}

				messageSet.Header = header; // set the header explicitely
				messageSet.RehydrateRest(dr, rehydrationFactory);
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Invalid data sent");
			}

			return messageSet;

		}

	#region Explicit Creation methods

		/// <summary>
		///     this is the client side trigger method, when we build a brand new one
		/// </summary>
		/// <param name="workflowCorrelationId"></param>
		/// <returns></returns>
		public IBlockchainTriggerMessageSet CreateAppointmentRequestWorkflowTriggerSet(uint workflowCorrelationId) {
			BlockchainTriggerMessageSet<APPOINTMENT_REQUEST_TRIGGER> messageSet = this.mainChainMessageFactory.CreateTriggerMessageSet<APPOINTMENT_REQUEST_TRIGGER>(workflowCorrelationId);

			return messageSet;
		}

		private IBlockchainTriggerMessageSet CreateAppointmentRequestWorkflowTriggerSet(TargettedHeader triggerHeader = null) {
			BlockchainTriggerMessageSet<APPOINTMENT_REQUEST_TRIGGER> messageSet = this.mainChainMessageFactory.CreateTriggerMessageSet<APPOINTMENT_REQUEST_TRIGGER>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateAppointmentRequestWorkflowTriggerServerReplySet(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<SERVER_TRIGGER_REPLY>(triggerHeader);

			return messageSet;
		}


	#endregion

	}
}