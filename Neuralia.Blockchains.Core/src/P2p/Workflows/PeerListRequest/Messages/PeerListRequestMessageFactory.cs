using System;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages {
	public class PeerListRequestMessageFactory<R> : MessageFactory<R>
		where R : IRehydrationFactory {

		public const ushort TRIGGER_ID = 1;
		public const ushort SERVER_REPLY_ID = 2;

		public PeerListRequestMessageFactory(ServiceSet<R> serviceSet) : base(serviceSet) {
		}

		public override ITargettedMessageSet<R> Rehydrate(SafeArrayHandle data, TargettedHeader header, R rehydrationFactory) {
			using IDataRehydrator dr = DataSerializationFactory.CreateRehydrator(data);

			using SafeArrayHandle messageBytes = NetworkMessageSet.ExtractMessageBytes(dr);
			NetworkMessageSet.ResetAfterHeader(dr);

			using IDataRehydrator messageRehydrator = DataSerializationFactory.CreateRehydrator(messageBytes);

			ITargettedMessageSet<R> messageSet = null;

			try {
				if(data?.Length == 0) {
					throw new ApplicationException("null message");
				}

				short workflowType = 0;
				ComponentVersion<SimpleUShort> version = null;

				messageRehydrator.Peek(rehydrator => {
					workflowType = rehydrator.ReadShort();

					if(workflowType != WorkflowIDs.PEER_LIST_REQUEST) {
						throw new ApplicationException("Invalid workflow type");
					}

					version = rehydrator.Rehydrate<ComponentVersion<SimpleUShort>>();
				});

				switch(version.Type.Value) {
					case TRIGGER_ID:

						if(version == (1, 0)) {
							messageSet = this.CreatePeerListRequestWorkflowTriggerSet(header);
						}

						break;

					case SERVER_REPLY_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateServerPeerListRequestSet(header);
						}

						break;

					default:

						throw new ApplicationException("invalid message type");
				}

				if(messageSet?.BaseMessage == null) {
					throw new ApplicationException("Invalid message type or version");
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
		public TriggerMessageSet<PeerListRequestTrigger<R>, R> CreatePeerListRequestWorkflowTriggerSet(uint workflowCorrelationId) {
			TriggerMessageSet<PeerListRequestTrigger<R>, R> messageSet = this.MainMessageFactory.CreateTriggerMessageSet<TriggerMessageSet<PeerListRequestTrigger<R>, R>, PeerListRequestTrigger<R>>(workflowCorrelationId);

			return messageSet;
		}

		private TriggerMessageSet<PeerListRequestTrigger<R>, R> CreatePeerListRequestWorkflowTriggerSet(TargettedHeader triggerHeader = null) {
			TriggerMessageSet<PeerListRequestTrigger<R>, R> messageSet = this.MainMessageFactory.CreateTriggerMessageSet<TriggerMessageSet<PeerListRequestTrigger<R>, R>, PeerListRequestTrigger<R>>(triggerHeader);

			return messageSet;
		}

		public TargettedMessageSet<PeerListRequestServerReply<R>, R> CreateServerPeerListRequestSet(TargettedHeader triggerHeader = null) {
			return this.MainMessageFactory.CreateTargettedMessageSet<TargettedMessageSet<PeerListRequestServerReply<R>, R>, PeerListRequestServerReply<R>>(triggerHeader);
		}

	#endregion

	}
}