using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.P2p.Connections;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.BlockchainMessageReceivedGossip {

	/// <summary>
	///     This workflow is activated when we receive a gossip message informing us that a new
	///     transaction has been created.
	///     When we get here, the transaction is already validated, so we add it to our quarantine
	///     since we do not trust
	///     externally created transactions until they are confirmed
	/// </summary>
	/// <typeparam name="MESSAGE_FACTORY"></typeparam>
	public abstract class NewBlockchainMessageReceivedGossipWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, GOSSIP_MESSAGE_SET, GOSSIP_WORKFLOW_TRIGGER_MESSAGE, EVENT_ENVELOPE_TYPE> : GossipChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, GOSSIP_MESSAGE_SET, GOSSIP_WORKFLOW_TRIGGER_MESSAGE, EVENT_ENVELOPE_TYPE>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where GOSSIP_MESSAGE_SET : IBlockchainGossipMessageSet<GOSSIP_WORKFLOW_TRIGGER_MESSAGE, EVENT_ENVELOPE_TYPE>
		where GOSSIP_WORKFLOW_TRIGGER_MESSAGE : class, IBlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>
		where EVENT_ENVELOPE_TYPE : IMessageEnvelope {

		public NewBlockchainMessageReceivedGossipWorkflow(BlockchainType chainType, CENTRAL_COORDINATOR centralCoordinator, GOSSIP_MESSAGE_SET triggerMessage, PeerConnection peerConnectionn) : base(chainType, centralCoordinator, triggerMessage, peerConnectionn) {

		}

		protected override void PerformWork() {

			this.CheckShouldCancel();

			//xxHasher64 hasher = new xxHasher64();

			EVENT_ENVELOPE_TYPE messageEnvelope = this.triggerMessage.Message.Envelope;

			var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction((blockchainService, taskRoutingContext) => {

				blockchainService.HandleBlockchainMessage(messageEnvelope.Contents.RehydratedMessage, messageEnvelope.Contents);
			}, (results, taskRoutingContext) => {

				// thats bad, we failed to add our transaction
				//TODO: do something to handle error here
			});

			this.DispatchTaskSync(blockchainTask);
		}
	}
}