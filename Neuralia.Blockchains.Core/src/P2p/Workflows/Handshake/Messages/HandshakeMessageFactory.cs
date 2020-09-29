using System;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages {
	public class HandshakeMessageFactory<R> : MessageFactory<R>
		where R : IRehydrationFactory {

		public const ushort TRIGGER_ID = 1;
		public const ushort SERVER_HANDSHAKE_ID = 2;
		public const ushort CLIENT_CONFIRM_ID = 3;
		public const ushort SERVER_CONFIRM_ID = 4;
		public const ushort CLIENT_READY_ID = 5;

		public HandshakeMessageFactory(ServiceSet<R> serviceSet) : base(serviceSet) {
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

					if(workflowType != WorkflowIDs.HANDSHAKE) {
						throw new ApplicationException("Invalid workflow type");
					}

					version = rehydrator.Rehydrate<ComponentVersion<SimpleUShort>>();
				});

				switch(version.Type.Value) {
					case TRIGGER_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateHandshakeWorkflowTriggerSet(header);
						}

						break;

					case SERVER_HANDSHAKE_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateServerHandshakeSet(header);
						}

						break;

					case CLIENT_CONFIRM_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateClientConfirmSet(header);
						}

						break;

					case SERVER_CONFIRM_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateServerConfirmSet(header);
						}

						break;

					case CLIENT_READY_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateClientReadySet(header);
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
		public TriggerMessageSet<HandshakeTrigger<R>, R> CreateHandshakeWorkflowTriggerSet(uint workflowCorrelationId) {
			TriggerMessageSet<HandshakeTrigger<R>, R> messageSet = this.MainMessageFactory.CreateTriggerMessageSet<TriggerMessageSet<HandshakeTrigger<R>, R>, HandshakeTrigger<R>>(workflowCorrelationId);

			return messageSet;
		}

		/// <summary>
		///     this is the rebuild trigger for the server side, where we copy the one we received by the tcp connection
		/// </summary>
		/// <param name="triggerHeader"></param>
		/// <returns></returns>
		protected TriggerMessageSet<HandshakeTrigger<R>, R> CreateHandshakeWorkflowTriggerSet(TargettedHeader triggerHeader) {
			TriggerMessageSet<HandshakeTrigger<R>, R> messageSet = this.MainMessageFactory.CreateTriggerMessageSet<TriggerMessageSet<HandshakeTrigger<R>, R>, HandshakeTrigger<R>>(triggerHeader);

			return messageSet;
		}

		public TargettedMessageSet<ServerHandshake<R>, R> CreateServerHandshakeSet(TargettedHeader triggerHeader = null) {
			return this.MainMessageFactory.CreateTargettedMessageSet<TargettedMessageSet<ServerHandshake<R>, R>, ServerHandshake<R>>(triggerHeader);
		}

		public TargettedMessageSet<ClientHandshakeConfirm<R>, R> CreateClientConfirmSet(TargettedHeader triggerHeader = null) {
			TargettedMessageSet<ClientHandshakeConfirm<R>, R> messageSet = this.MainMessageFactory.CreateTargettedMessageSet<TargettedMessageSet<ClientHandshakeConfirm<R>, R>, ClientHandshakeConfirm<R>>(triggerHeader);

			return messageSet;
		}

		public TargettedMessageSet<ServerHandshakeConfirm<R>, R> CreateServerConfirmSet(TargettedHeader triggerHeader = null) {
			return this.MainMessageFactory.CreateTargettedMessageSet<TargettedMessageSet<ServerHandshakeConfirm<R>, R>, ServerHandshakeConfirm<R>>(triggerHeader);
		}

		public TargettedMessageSet<ClientReady<R>, R> CreateClientReadySet(TargettedHeader triggerHeader = null) {
			return this.MainMessageFactory.CreateTargettedMessageSet<TargettedMessageSet<ClientReady<R>, R>, ClientReady<R>>(triggerHeader);
		}

	#endregion

	}
}