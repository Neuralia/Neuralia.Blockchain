using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.BlockchainMessageReceivedGossip.Messages.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.TransactionReceivedGossip.Messages.V1;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages {
	public interface IMainChainMessageFactory : IGossipMessageFactory<IBlockchainEventsRehydrationFactory> {

		IChainSyncMessageFactory GetChainSyncMessageFactory();

		IBlockchainGossipMessageSet CreateTransactionCreatedGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>(ITransactionEnvelope envelope)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : TransactionCreatedGossipMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, ITransactionEnvelope, new();

		IBlockchainGossipMessageSet CreateTransactionCreatedGossipMessageSet(GossipHeader header);
		IBlockchainGossipMessageSet CreateTransactionCreatedGossipMessageSet(ITransactionEnvelope envelope);

		IBlockchainGossipMessageSet CreateBlockchainMessageCreatedGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>(IMessageEnvelope envelope)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainMessageCreatedGossipMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IMessageEnvelope, new();

		IBlockchainGossipMessageSet CreateBlockchainMessageCreatedGossipMessageSet(GossipHeader header);
		IBlockchainGossipMessageSet CreateBlockchainMessageCreatedGossipMessageSet(IMessageEnvelope envelope);

		IBlockchainGossipMessageSet CreateBlockCreatedGossipMessageSet(GossipHeader header);

		/// <summary>
		///     here we ensure that the messsage is properly hashed.
		/// </summary>
		/// <returns>The xHash of the cryptographic hash itself</returns>
		/// <param name="gossipMessageSet"></param>
		void HashGossipMessage(IBlockchainGossipMessageSet gossipMessageSet);

		void CopyTargettedHeaderInfo(TargettedHeader newHeader, TargettedHeader triggerHeader);

		BlockchainTargettedMessageSet<T> CreateTargettedMessageSet<T>()
			where T : NetworkMessage<IBlockchainEventsRehydrationFactory>, new();

		BlockchainTargettedMessageSet<T> CreateTargettedMessageSet<T>(TargettedHeader original)
			where T : NetworkMessage<IBlockchainEventsRehydrationFactory>, new();

		BlockchainTriggerMessageSet<T> CreateTriggerMessageSet<T>(uint workflowCorrelationId, uint? workflowSessionId = null)
			where T : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory>, new();

		BlockchainTriggerMessageSet<T> CreateTriggerMessageSet<T>(TargettedHeader original)
			where T : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory>, new();

		GOSSIP_MESSAGE_SET CreateGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>(EVENT_ENVELOPE_TYPE envelope)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IEnvelope;

		GOSSIP_MESSAGE_SET CreateGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_TYPE, EVENT_ENVELOPE_TYPE, EVENT_ENVELOPE_IMPLEMENTATION_TYPE>(EVENT_TYPE event_entry)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IEnvelope<EVENT_TYPE>
			where EVENT_ENVELOPE_IMPLEMENTATION_TYPE : class, EVENT_ENVELOPE_TYPE, new()
			where EVENT_TYPE : class, IBinarySerializable;

		GOSSIP_MESSAGE_SET CreateEmptyGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>()
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IEnvelope;

		GOSSIP_MESSAGE_SET CreateGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_TYPE, EVENT_ENVELOPE_TYPE>(GossipHeader header)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IEnvelope<EVENT_TYPE>
			where EVENT_TYPE : class, IBinarySerializable;

		M CreateMessageSet<M, T, H>()
			where M : INetworkMessageSet2<T, H, IBlockchainEventsRehydrationFactory>, new()
			where T : NetworkMessage<IBlockchainEventsRehydrationFactory>, new()
			where H : RoutingHeader, new();
	}

	public abstract class MainChainMessageFactory : GossipMessageFactory<IBlockchainEventsRehydrationFactory>, IMainChainMessageFactory {

		protected readonly BlockchainType chainType;

		private readonly Dictionary<BlockchainType, IMainChainMessageFactory> messageFactories = new Dictionary<BlockchainType, IMainChainMessageFactory>();

		public MainChainMessageFactory(BlockchainType chainType, BlockchainServiceSet serviceSet) : base(serviceSet) {
			this.chainType = chainType;
		}

		public BlockchainType ChainType => this.chainType;

		public abstract IChainSyncMessageFactory GetChainSyncMessageFactory();

		public override ITargettedMessageSet<IBlockchainEventsRehydrationFactory> RehydrateMessage(SafeArrayHandle data, TargettedHeader header, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			//TODO: should there be anything here?
			IDataRehydrator dr = DataSerializationFactory.CreateRehydrator(data);

			SafeArrayHandle messageBytes = NetworkMessageSet.ExtractMessageBytes(dr);
			NetworkMessageSet.ResetAfterHeader(dr);
			IDataRehydrator messageRehydrator = DataSerializationFactory.CreateRehydrator(messageBytes);

			short workflowType = 0;

			messageRehydrator.Peek(rehydrator => {
				workflowType = rehydrator.ReadShort();
			});

			if(workflowType == 0) {
				throw new ApplicationException("Invalid workflow type");
			}

			switch(workflowType) {
				case WorkflowIDs.CHAIN_SYNC:

					return this.GetChainSyncMessageFactory().RehydrateMessage(data, header, rehydrationFactory);

				default:

					throw new ApplicationException("Workflow message factory not found");
			}
		}

		public override IGossipMessageSet RehydrateGossipMessage(SafeArrayHandle data, GossipHeader header, IBlockchainEventsRehydrationFactory rehydrationFactory) {

			IDataRehydrator dr = DataSerializationFactory.CreateRehydrator(data);

			SafeArrayHandle messageBytes = NetworkMessageSet.ExtractMessageBytes(dr);
			NetworkMessageSet.ResetAfterHeader(dr);

			short workflowType = 0;
			ComponentVersion<SimpleUShort> version = null;

			IDataRehydrator messageRehydrator = DataSerializationFactory.CreateRehydrator(messageBytes);

			workflowType = messageRehydrator.ReadShort();
			version = messageRehydrator.Rehydrate<ComponentVersion<SimpleUShort>>();

			//TODO: check versioning here. improve it

			if(version.Type != GossipWorkflowTriggerMessageConstants.GOSSIP_MESSAGE_TRIGGER) {
				throw new ApplicationException("Invalid gossip message trigger type");
			}

			IBlockchainGossipMessageSet messageSet = null;

			switch(workflowType) {
				case GossipWorkflowIDs.TRANSACTION_RECEIVED:

					if(version == (1, 0)) {
						messageSet = this.CreateTransactionCreatedGossipMessageSet(header);
					}

					break;

				case GossipWorkflowIDs.BLOCK_RECEIVED:

					if(version == (1, 0)) {
						messageSet = this.CreateBlockCreatedGossipMessageSet(header);
					}

					break;

				case GossipWorkflowIDs.MESSAGE_RECEIVED:

					if(version == (1, 0)) {
						messageSet = this.CreateBlockchainMessageCreatedGossipMessageSet(header);
					}

					break;

				default:

					throw new ApplicationException("Invalid trigger message received");
			}

			if(messageSet?.BaseMessage == null) {
				throw new ApplicationException("Invalid message type or version");
			}

			((IBlockchainGossipMessageRWSet) messageSet).RWBaseHeader = header; // set the header explicitely
			messageSet.RehydrateRest(dr, rehydrationFactory);

			// do not call the base here, or it will loop forever since the base calls this class
			return messageSet;
		}

		public abstract IBlockchainGossipMessageSet CreateTransactionCreatedGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>(ITransactionEnvelope transaction)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : TransactionCreatedGossipMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, ITransactionEnvelope, new();

		public abstract IBlockchainGossipMessageSet CreateTransactionCreatedGossipMessageSet(GossipHeader header);
		public abstract IBlockchainGossipMessageSet CreateTransactionCreatedGossipMessageSet(ITransactionEnvelope transaction);

		public abstract IBlockchainGossipMessageSet CreateBlockchainMessageCreatedGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>(IMessageEnvelope message)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainMessageCreatedGossipMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IMessageEnvelope, new();

		public abstract IBlockchainGossipMessageSet CreateBlockchainMessageCreatedGossipMessageSet(GossipHeader header);
		public abstract IBlockchainGossipMessageSet CreateBlockchainMessageCreatedGossipMessageSet(IMessageEnvelope message);

		public abstract IBlockchainGossipMessageSet CreateBlockCreatedGossipMessageSet(GossipHeader header);

		/// <summary>
		///     here we ensure that the messsage is properly hashed
		/// </summary>
		/// <param name="gossipMessageSet"></param>
		public void HashGossipMessage(IBlockchainGossipMessageSet gossipMessageSet) {

			HashingUtils.HashGossipMessageSet(gossipMessageSet);
		}

		/// <summary>
		///     make sure a new header carries the routing connection from its trigger header
		/// </summary>
		/// <param name="newHeader"></param>
		/// <param name="triggerHeader"></param>
		public void CopyTargettedHeaderInfo(TargettedHeader newHeader, TargettedHeader triggerHeader) {
			if(triggerHeader != null) {
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
		public BlockchainTargettedMessageSet<T> CreateTargettedMessageSet<T>()
			where T : NetworkMessage<IBlockchainEventsRehydrationFactory>, new() {
			var messageSet = this.MainMessageFactory.CreateTargettedMessageSet<BlockchainTargettedMessageSet<T>, T>();

			messageSet.Header.chainId = this.chainType.Value;

			return messageSet;
		}

		public BlockchainTargettedMessageSet<T> CreateTargettedMessageSet<T>(TargettedHeader original)
			where T : NetworkMessage<IBlockchainEventsRehydrationFactory>, new() {
			var messageSet = this.MainMessageFactory.CreateTargettedMessageSet<BlockchainTargettedMessageSet<T>, T>(original);

			messageSet.Header.chainId = this.chainType.Value;

			return messageSet;
		}
		
		public BlockchainTriggerMessageSet<T> CreateTriggerMessageSet<T>(uint workflowCorrelationId, uint? workflowSessionId = null)
			where T : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory>, new() {
			var messageSet = this.MainMessageFactory.CreateTriggerMessageSet<BlockchainTriggerMessageSet<T>, T>(workflowCorrelationId);

			messageSet.Header.chainId = this.chainType.Value;

			return messageSet;
		}

		public BlockchainTriggerMessageSet<T> CreateTriggerMessageSet<T>(TargettedHeader original)
			where T : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory>, new() {
			var messageSet = this.MainMessageFactory.CreateTriggerMessageSet<BlockchainTriggerMessageSet<T>, T>(original);

			messageSet.Header.chainId = this.chainType.Value;

			return messageSet;
		}

		/// <summary>
		///     create a brand new gossip message
		/// </summary>
		/// <typeparam name="GOSSIP_MESSAGE_SET"></typeparam>
		/// <typeparam name="GOSSIP_MESSAGE_TYPE"></typeparam>
		/// <typeparam name="EVENT_ENVELOPE_TYPE"></typeparam>
		/// <typeparam name=""></typeparam>
		/// <returns></returns>
		public virtual GOSSIP_MESSAGE_SET CreateGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>(EVENT_ENVELOPE_TYPE envelope)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IEnvelope {

			GOSSIP_MESSAGE_SET messageSet = this.CreateEmptyGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>();

			messageSet.Message.Envelope = envelope;

			return messageSet;
		}

		/// <summary>
		///     create a brand new gossip message
		/// </summary>
		/// <typeparam name="GOSSIP_MESSAGE_SET"></typeparam>
		/// <typeparam name="GOSSIP_MESSAGE_TYPE"></typeparam>
		/// <typeparam name="EVENT_ENVELOPE_TYPE"></typeparam>
		/// <typeparam name=""></typeparam>
		/// <returns></returns>
		public virtual GOSSIP_MESSAGE_SET CreateGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_TYPE, EVENT_ENVELOPE_TYPE, EVENT_ENVELOPE_IMPLEMENTATION_TYPE>(EVENT_TYPE event_entry)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IEnvelope<EVENT_TYPE>
			where EVENT_ENVELOPE_IMPLEMENTATION_TYPE : class, EVENT_ENVELOPE_TYPE, new()
			where EVENT_TYPE : class, IBinarySerializable {

			GOSSIP_MESSAGE_SET messageSet = this.CreateEmptyGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>();

			EVENT_ENVELOPE_TYPE envelope = new EVENT_ENVELOPE_IMPLEMENTATION_TYPE();
			envelope.Contents = event_entry;
			messageSet.Message.Envelope = envelope;

			return messageSet;
		}

		public virtual GOSSIP_MESSAGE_SET CreateEmptyGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>()
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IEnvelope {

			GOSSIP_MESSAGE_SET messageSet = this.CreateMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, GossipHeader>();

			//since we are creating and sending the message, we want to get it back, so we can track that it was accepted and forwarded by our peers
			messageSet.Header.NetworkOptions.SetOption((byte) GossipHeader.GossipNetworkMessageOptions.ReturnMeMessage);

			// this is important, let them know which chain we are
			messageSet.Header.chainId = this.chainType.Value;

			return messageSet;
		}

		public virtual GOSSIP_MESSAGE_SET CreateGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_TYPE, EVENT_ENVELOPE_TYPE>(GossipHeader header)
			where GOSSIP_MESSAGE_SET : BlockchainGossipMessageSet<GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>, new()
			where GOSSIP_MESSAGE_TYPE : BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>, new()
			where EVENT_ENVELOPE_TYPE : class, IEnvelope<EVENT_TYPE>
			where EVENT_TYPE : class, IBinarySerializable {

			GOSSIP_MESSAGE_SET messageSet = this.CreateEmptyGossipMessageSet<GOSSIP_MESSAGE_SET, GOSSIP_MESSAGE_TYPE, EVENT_ENVELOPE_TYPE>();

			this.CopyGossipHeaderHeaderInfo(messageSet.Header, header);

			return messageSet;
		}

		/// <summary>
		///     create a basic message set
		/// </summary>
		/// <returns></returns>
		public M CreateMessageSet<M, T, H>()
			where M : INetworkMessageSet2<T, H, IBlockchainEventsRehydrationFactory>, new()
			where T : NetworkMessage<IBlockchainEventsRehydrationFactory>, new()
			where H : RoutingHeader, new() {
			M messageSet = new M();
			messageSet.ReceivedTime = this.serviceSet.TimeService.CurrentRealTime;

			messageSet.Message = new T();
			messageSet.Header = new H {SentTime = this.serviceSet.TimeService.CurrentRealTime};

			// initialize any header variables here

			return messageSet;
		}

		public void RegisterChainMessageFactory(BlockchainType chainType, IMainChainMessageFactory messageFactory) {
			this.messageFactories.Add(chainType, messageFactory);
		}
	}
}