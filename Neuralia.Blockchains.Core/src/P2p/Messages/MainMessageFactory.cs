using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages;
using Neuralia.Blockchains.Core.P2p.Workflows.PeerListRequest.Messages;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages {
	public interface IMainMessageFactory : IGossipMessageFactory {
	}

	public interface IMainMessageFactory<R> : IGossipMessageFactory<R>, IMainMessageFactory
		where R : IRehydrationFactory {

		IRoutingHeader RehydrateMessageHeader(SafeArrayHandle data);

		/// <summary>
		///     make sure a new header carries the routing connection from its trigger header
		/// </summary>
		/// <param name="newHeader"></param>
		/// <param name="triggerHeader"></param>
		void CopyTargettedHeaderInfo(TargettedHeader newHeader, TargettedHeader triggerHeader);

		/// <summary>
		///     create a new messageset, and insert the provided header into the message
		/// </summary>
		/// <param name="header"></param>
		/// <returns></returns>
		M CreateTargettedMessageSet<M, T>()
			where M : TargettedMessageSet<T, R>, new()
			where T : NetworkMessage<R>, new();

		M CreateTargettedMessageSet<M, T>(TargettedHeader original)
			where M : TargettedMessageSet<T, R>, new()
			where T : NetworkMessage<R>, new();

		M CreateTriggerMessageSet<M, T>(uint workflowCorrelationId, uint? workflowSessionId = null)
			where M : TriggerMessageSet<T, R>, new()
			where T : WorkflowTriggerMessage<R>, new();

		M CreateTriggerMessageSet<M, T>(TargettedHeader original)
			where M : TriggerMessageSet<T, R>, new()
			where T : WorkflowTriggerMessage<R>, new();

		void RegisterChainMessageFactory(BlockchainType chainType, IGossipMessageFactory<R> messageFactory);
	}

	public class MainMessageFactory<R> : GossipMessageFactory<R>, IMainMessageFactory<R>
		where R : IRehydrationFactory {

		private readonly Dictionary<BlockchainType, IGossipMessageFactory<R>> messageFactories = new Dictionary<BlockchainType, IGossipMessageFactory<R>>();

		public MainMessageFactory(ServiceSet<R> serviceSet) : base(serviceSet) {
		}

		public void RegisterChainMessageFactory(BlockchainType chainType, IGossipMessageFactory<R> messageFactory) {
			this.messageFactories.Add(chainType, messageFactory);
		}

		public override ITargettedMessageSet<R> RehydrateMessage(SafeArrayHandle data, TargettedHeader header, R rehydrationFactory) {
			using(IDataRehydrator dr = DataSerializationFactory.CreateRehydrator(data)) {

				SafeArrayHandle messageBytes = NetworkMessageSet.ExtractMessageBytes(dr);
				NetworkMessageSet.ResetAfterHeader(dr);

				short workflowType = 0;
				using(IDataRehydrator messageRehydrator = DataSerializationFactory.CreateRehydrator(messageBytes)) {

					workflowType = messageRehydrator.ReadShort();
				}

				if(workflowType == 0) {
					throw new ApplicationException("Invalid workflow type");
				}

				switch(workflowType) {
					case WorkflowIDs.HANDSHAKE:

						return new HandshakeMessageFactory<R>(this.serviceSet).RehydrateMessage(data, header, rehydrationFactory);

					case WorkflowIDs.PEER_LIST_REQUEST:

						return new PeerListRequestMessageFactory<R>(this.serviceSet).RehydrateMessage(data, header, rehydrationFactory);

					case WorkflowIDs.MESSAGE_GROUP_MANIFEST:

						return new MessageGroupManifestMessageFactory<R>(this.serviceSet).RehydrateMessage(data, header, rehydrationFactory);

					default:

						throw new ApplicationException("Workflow message factory not found");
				}
			}
		}

		/// <summary>
		///     Gossip messages are separated for security reasons. Here we delegate the rehydration to the right chain
		/// </summary>
		/// <param name="data"></param>
		/// <param name="header"></param>
		/// <returns></returns>
		public override IGossipMessageSet RehydrateGossipMessage(SafeArrayHandle data, GossipHeader header, R rehydrationFactory) {

			if(header.ChainId == BlockchainTypes.Instance.None) {
				throw new ApplicationException("null chains not supported");
			}

			return this.messageFactories[header.chainId].RehydrateGossipMessage(data, header, rehydrationFactory);
		}

		public virtual IRoutingHeader RehydrateMessageHeader(SafeArrayHandle data) {
			using(IDataRehydrator dr = DataSerializationFactory.CreateRehydrator(data)) {

				IRoutingHeader header = null;

				// skip the initial network optionsBase
				byte networkOptions = dr.ReadByte();
				(int headerSize, int _, int _) = dr.ReadSectionSize();

				var version = dr.RehydrateRewind<ComponentVersion<SimpleUShort>>();

				switch(version.Type.Value.Value) {
					case 1:

						if(version == (1, 0)) {
							header = new TargettedHeader();
						}

						break;

					case 2:

						if(version == (1, 0)) {
							header = new GossipHeader();
						}

						break;
				}

				if(header == null) {
					throw new ApplicationException("Invalid message header type");
				}

				header.Rehydrate(dr);

				// make sure we set this here, externally since the value is outside the reading ability of the header rehydrator
				header.NetworkOptions = networkOptions;

				return header;
			}
		}

		/// <summary>
		///     make sure a new header carries the routing connection from its trigger header
		/// </summary>
		/// <param name="newHeader"></param>
		/// <param name="triggerHeader"></param>
		public void CopyTargettedHeaderInfo(TargettedHeader newHeader, TargettedHeader triggerHeader) {
			if(triggerHeader != null) {
				newHeader.NetworkOptions.Value = triggerHeader.NetworkOptions.Value;
				newHeader.WorkflowCorrelationId = triggerHeader.WorkflowCorrelationId;
				newHeader.WorkflowSessionId = triggerHeader.WorkflowSessionId;
				newHeader.OriginatorId = triggerHeader.OriginatorId;
			}
		}

		/// <summary>
		///     create a new messageset, and insert the provided header into the message
		/// </summary>
		/// <param name="header"></param>
		/// <returns></returns>
		public M CreateTargettedMessageSet<M, T>()
			where M : TargettedMessageSet<T, R>, new()
			where T : NetworkMessage<R>, new() {
			M messageSet = this.CreateMessageSet<M, T, TargettedHeader>();

			return messageSet;
		}

		public M CreateTargettedMessageSet<M, T>(TargettedHeader original)
			where M : TargettedMessageSet<T, R>, new()
			where T : NetworkMessage<R>, new() {
			M messageSet = this.CreateTargettedMessageSet<M, T>();

			this.CopyTargettedHeaderInfo(messageSet.Header, original);

			return messageSet;
		}

		public M CreateTriggerMessageSet<M, T>(uint workflowCorrelationId, uint? workflowSessionId = null)
			where M : TriggerMessageSet<T, R>, new()
			where T : WorkflowTriggerMessage<R>, new() {
			M messageSet = this.CreateMessageSet<M, T, TargettedHeader>();

			messageSet.Header.WorkflowCorrelationId = workflowCorrelationId;
			messageSet.Header.WorkflowSessionId = workflowSessionId;
			messageSet.Header.OriginatorId = this.NetworkingService.ConnectionStore.MyClientUuid; // our identifying id

			// always true for workflow triggers
			messageSet.Header.options.SetOption((byte) RoutingHeader.Options.WorkflowTrigger);

			return messageSet;
		}

		public M CreateTriggerMessageSet<M, T>(TargettedHeader original)
			where M : TriggerMessageSet<T, R>, new()
			where T : WorkflowTriggerMessage<R>, new() {
			M messageSet = this.CreateMessageSet<M, T, TargettedHeader>();

			this.CopyTargettedHeaderInfo(messageSet.Header, original);

			// always true for workflow triggers
			messageSet.Header.options.SetOption((byte) RoutingHeader.Options.WorkflowTrigger);

			return messageSet;
		}

		/// <summary>
		///     create a basic message set
		/// </summary>
		/// <returns></returns>
		protected M CreateMessageSet<M, T, H>()
			where M : INetworkMessageSet2<T, H, R>, new()
			where T : NetworkMessage<R>, new()
			where H : RoutingHeader, new() {
			M messageSet = new M();
			messageSet.ReceivedTime = this.TimeService.CurrentRealTime;

			messageSet.Message = new T();
			messageSet.Header = new H();

			// initialize any header variables here
			messageSet.Header.SentTime = this.TimeService.CurrentRealTime;

			return messageSet;
		}
	}
}