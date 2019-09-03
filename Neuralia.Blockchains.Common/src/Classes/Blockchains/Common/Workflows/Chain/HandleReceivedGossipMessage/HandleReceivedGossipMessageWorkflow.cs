using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.HandleReceivedGossipMessage {
	public interface IReceiveGossipMessageWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     this is the base chain workflow to handle a gossip message that we received from our peers. It will validate the
	///     message
	///     and if all is good, then it will add it to our chain and forward it to other peers. if its a dud, we log it as such
	///     and stop
	/// </summary>
	/// <typeparam name="TRANSACTION_BLOCK_FACTORY"></typeparam>
	/// <typeparam name="BLOCKCHAIN_MODEL"></typeparam>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="BLOCKASSEMBLYPROVIDER"></typeparam>
	/// <typeparam name="WALLET_MANAGER"></typeparam>
	/// <typeparam name="USERWALLET"></typeparam>
	/// <typeparam name="WALLET_IDENTITY"></typeparam>
	/// <typeparam name="WALLET_KEY"></typeparam>
	/// <typeparam name="WALLET_KEY_HISTORY"></typeparam>
	/// <typeparam name="MESSAGE_FACTORY"></typeparam>
	/// <typeparam name="WORKFLOW_FACTORY"></typeparam>
	/// <typeparam name="CLIENT_WORKFLOW_FACTORY"></typeparam>
	/// <typeparam name="SERVER_WORKFLOW_FACTORY"></typeparam>
	/// <typeparam name="GOSSIP_WORKFLOW_FACTORY"></typeparam>
	/// <typeparam name="WORKFLOW_TRIGGER_MESSAGE"></typeparam>
	public abstract class HandleReceivedGossipMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IReceiveGossipMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		protected readonly PeerConnection Connection;

		protected readonly IBlockchainGossipMessageSet gossipMessageSet;

		public HandleReceivedGossipMessageWorkflow(CENTRAL_COORDINATOR centralCoordinator, IBlockchainGossipMessageSet gossipMessageSet, PeerConnection connection) : base(centralCoordinator) {
			this.gossipMessageSet = gossipMessageSet;
			this.Connection = connection;

			// allow only one at a time
			this.ExecutionMode = Workflow.ExecutingMode.Sequential;
		}

		protected override void PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {
			if(this.gossipMessageSet == null) {
				throw new ApplicationException("Gossip message must be valid");
			}

			if(this.gossipMessageSet.BaseMessage == null) {
				throw new ApplicationException("Gossip message transaction must be valid");
			}

			if(GlobalSettings.ApplicationSettings.MobileMode) {
				if(this.gossipMessageSet.BaseMessage.BaseEnvelope is IBlockEnvelope) {
					// in mobile mode, we simply do not handle any gossip mesasges
					Log.Information("Mobile nodes does not handle block gossip messages");

					return;
				}

				if(!Enums.BasicGossipPeerTypes.Contains(GlobalSettings.Instance.PeerType)) {
					// in mobile mode, we simply do not handle any gossip mesasges
					Log.Information("This mobile node does not handle gossip messages");

					return;
				}
			}

			// ok, the first step is to ensure the message is valid. otherwise we do not handle it any further
			var validationTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateValidationTask<bool>();

			ValidationResult valid = new ValidationResult();

			validationTask.SetAction((validationService, taskRoutingContext2) => {
				IRoutedTask validateEnvelopeContentTask = validationService.ValidateEnvelopedContent(this.gossipMessageSet.BaseMessage.BaseEnvelope, result => {
					valid = result;
				});

				taskRoutingContext2.AddChild(validateEnvelopeContentTask);
			});

			this.DispatchTaskSync(validationTask);

			// ok, if we can't validate a message, we are most probably out of sync. if we are not already syncing, let's request one.
			if(valid == ValidationResult.ValidationResults.CantValidate) {
				// we have a condition when we may be out of sync and if we are not already syncing, we should do it
				var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

				blockchainTask.SetAction((blockchainService, taskRoutingContext2) => {
					blockchainService.SynchronizeBlockchain(true);
				});

				// no need to wait, this can totally be async
				this.DispatchTaskAsync(blockchainTask);
			}

			long xxHash = this.gossipMessageSet.BaseHeader.Hash;

			var serializationTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<bool>();

			serializationTask.SetAction((serializationService, taskRoutingContext2) => {

				//check if we have already received it, and if we did, we update the validation status, since we just did so.
				bool isInCache = serializationService.CheckRegistryMessageInCache(xxHash, valid.Valid);

				// see if we should cache the message if it's a block
				if((valid == ValidationResult.ValidationResults.CantValidate) && this.gossipMessageSet.BaseMessage.BaseEnvelope is IBlockEnvelope unvalidatedBlockEnvelope) {

					// ok, its a block message. we can't validate it yet. If it is close enough from our chain height, we will cache it, so we can use it later.
					long currentBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
					int blockGossipCacheProximityLevel = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.BlockGossipCacheProximityLevel;

					if((unvalidatedBlockEnvelope.BlockId > currentBlockHeight) && (unvalidatedBlockEnvelope.BlockId <= (currentBlockHeight + blockGossipCacheProximityLevel))) {
						try {
							serializationService.CacheUnvalidatedBlockGossipMessage(unvalidatedBlockEnvelope, xxHash);
						} catch(Exception ex) {
							//just eat the exception if anything here, it's not so important
							Log.Error(ex, $"Failed to cache gossip block message for block Id {unvalidatedBlockEnvelope.BlockId}");
						}
					}
				}
			});

			this.DispatchTaskSync(serializationTask);

			if(valid.Result == ValidationResult.ValidationResults.Invalid) {

				// this is the end, we go no further with an invalid message

				//TODO: what should we do here? perhaps we should log it about the peer
				Log.Error($"Gossip message received by peer {this.Connection.ScopedAdjustedIp} was invalid.");
			} else if(valid == ValidationResult.ValidationResults.CantValidate) {
				// seems we could do nothing with it. we just let it go
				Log.Verbose($"Gossip message received by peer {this.Connection.ScopedAdjustedIp} but could not be validated. the message will be ignored.");
			} else if(valid == ValidationResult.ValidationResults.EmbededKeyValid) {
				// seems we could do nothing with it. we just let it go
				Log.Verbose($"Gossip message received by peer {this.Connection.ScopedAdjustedIp} could not be validated, but the embeded public key was valid.");

				// ok, in this case, we can at least forward it on the gossip network
				this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.ForwardValidGossipMessage(this.gossipMessageSet, this.Connection);

			} else if(valid.Valid) {
				// ok, if we get here, this message is fully valid!  first step, we instantiate the workflow for this gossip transaction
				var gossipMessageWorkflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.GossipWorkflowFactoryBase.CreateGossipResponseWorkflow(this.gossipMessageSet, this.Connection);

				if(gossipMessageWorkflow == null) {
					throw new ApplicationException("Invalid workflow triggered by the gossip message.");
				}

				// thats it, lets let it run
				this.centralCoordinator.PostWorkflow(gossipMessageWorkflow);

				// and since this is good or possibly valid, now we ensure it will get forwarded to our peers
				this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.ForwardValidGossipMessage(this.gossipMessageSet, this.Connection);

				// ok , we are done. good job :)
				Log.Verbose($"Gossip message received by peer {this.Connection.ScopedAdjustedIp} was valid and was processed properly.");
			}
		}
	}
}