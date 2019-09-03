using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.P2p.Connections;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.BlockReceivedGossip {

	/// <summary>
	///     This workflow is activated when we receive a gossip message informing us that a new
	///     transaction has been created.
	///     When we get here, the transaction is already validated, so we add it to our quarantine
	///     since we do not trust
	///     externally created transactions until they are confirmed
	/// </summary>
	/// <typeparam name="MESSAGE_FACTORY"></typeparam>
	public abstract class NewBlockReceivedGossipWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, GOSSIP_MESSAGE_SET, GOSSIP_WORKFLOW_TRIGGER_MESSAGE, EVENT_ENVELOPE_TYPE> : GossipChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, GOSSIP_MESSAGE_SET, GOSSIP_WORKFLOW_TRIGGER_MESSAGE, EVENT_ENVELOPE_TYPE>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where GOSSIP_MESSAGE_SET : IBlockchainGossipMessageSet<GOSSIP_WORKFLOW_TRIGGER_MESSAGE, EVENT_ENVELOPE_TYPE>
		where GOSSIP_WORKFLOW_TRIGGER_MESSAGE : class, IBlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>
		where EVENT_ENVELOPE_TYPE : IBlockEnvelope {

		public NewBlockReceivedGossipWorkflow(BlockchainType chainType, CENTRAL_COORDINATOR centralCoordinator, GOSSIP_MESSAGE_SET triggerMessage, PeerConnection peerConnectionn) : base(chainType, centralCoordinator, triggerMessage, peerConnectionn) {

		}

		protected override void PerformWork() {
			this.CheckShouldCancel();

			EVENT_ENVELOPE_TYPE blockEnvelope = this.triggerMessage.Message.Envelope;

			var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction((blockchainService, taskRoutingContext) => {

				blockchainService.InsertInterpretBlock(blockEnvelope.Contents.RehydratedBlock, blockEnvelope.Contents, true);

				// ok, by inserting a new block through the gossip network, it means we are at the top of the chain and we really just synced.
				// lets update our last synced time to reflect this

				this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastSync = DateTime.Now;

			}, (results, taskRoutingContext) => {

				if(results.Success) {

				} else {
					Log.Error(results.Exception, "Failed to install block from a received gossip message.");
				}
			});

			this.DispatchTaskAsync(blockchainTask);

			// we are done!
		}
	}
}