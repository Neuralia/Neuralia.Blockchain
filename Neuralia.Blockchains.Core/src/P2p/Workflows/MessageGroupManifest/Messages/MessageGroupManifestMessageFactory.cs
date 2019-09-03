using System;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages {
	public class MessageGroupManifestMessageFactory<R> : MessageFactory<R>
		where R : IRehydrationFactory {
		public const ushort TRIGGER_ID = 1;
		public const ushort SERVER_REPLY_ID = 2;
		public const ushort CLIENT_MESSAGE_GROUP_REPLY_ID = 3;

		public MessageGroupManifestMessageFactory(ServiceSet<R> serviceSet) : base(serviceSet) {
		}

		public override ITargettedMessageSet<R> RehydrateMessage(IByteArray data, TargettedHeader header, R rehydrationFactory) {
			IDataRehydrator dr = DataSerializationFactory.CreateRehydrator(data);
			IByteArray messageBytes = NetworkMessageSet.ExtractMessageBytes(dr);
			NetworkMessageSet.ResetAfterHeader(dr);
			IDataRehydrator messageRehydrator = DataSerializationFactory.CreateRehydrator(messageBytes);

			ITargettedMessageSet<R> messageSet = null;

			try {
				if(data?.Length == 0) {
					throw new ApplicationException("null message");
				}

				short workflowType = 0;
				ComponentVersion<SimpleUShort> version = null;

				messageRehydrator.Peek(rehydrator => {
					workflowType = rehydrator.ReadShort();

					if(workflowType != WorkflowIDs.MESSAGE_GROUP_MANIFEST) {
						throw new ApplicationException("Invalid workflow type");
					}

					version = rehydrator.Rehydrate<ComponentVersion<SimpleUShort>>();
				});

				switch(version.Type.Value) {
					case TRIGGER_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateMessageGroupManifestWorkflowTriggerSet(header);
						}

						break;

					case SERVER_REPLY_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateServerMessageGroupManifestSet(header);
						}

						break;

					case CLIENT_MESSAGE_GROUP_REPLY_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateClientMessageGroupReplySet(header);
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
				Log.Error(ex, "Invalid data sent");
			}

			return messageSet;
		}

	#region Explicit Creation methods

		/// <summary>
		///     this is the client side trigger method, when we build a brand new one
		/// </summary>
		/// <param name="workflowCorrelationId"></param>
		/// <returns></returns>
		public TriggerMessageSet<MessageGroupManifestTrigger<R>, R> CreateMessageGroupManifestWorkflowTriggerSet(uint workflowCorrelationId) {
			var messageSet = this.MainMessageFactory.CreateTriggerMessageSet<TriggerMessageSet<MessageGroupManifestTrigger<R>, R>, MessageGroupManifestTrigger<R>>(workflowCorrelationId);

			return messageSet;
		}

		private TriggerMessageSet<MessageGroupManifestTrigger<R>, R> CreateMessageGroupManifestWorkflowTriggerSet(TargettedHeader triggerHeader) {
			var messageSet = this.MainMessageFactory.CreateTriggerMessageSet<TriggerMessageSet<MessageGroupManifestTrigger<R>, R>, MessageGroupManifestTrigger<R>>(triggerHeader);

			return messageSet;
		}

		public TargettedMessageSet<MessageGroupManifestServerReply<R>, R> CreateServerMessageGroupManifestSet(TargettedHeader triggerHeader = null) {
			return this.MainMessageFactory.CreateTargettedMessageSet<TargettedMessageSet<MessageGroupManifestServerReply<R>, R>, MessageGroupManifestServerReply<R>>(triggerHeader);
		}

		public TargettedMessageSet<ClientMessageGroupReply<R>, R> CreateClientMessageGroupReplySet(TargettedHeader triggerHeader = null) {
			var messageSet = this.MainMessageFactory.CreateTargettedMessageSet<TargettedMessageSet<ClientMessageGroupReply<R>, R>, ClientMessageGroupReply<R>>(triggerHeader);

			return messageSet;
		}

	#endregion

	}
}