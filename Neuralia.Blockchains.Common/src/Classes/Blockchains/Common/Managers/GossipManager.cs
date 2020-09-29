using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.WalletSync;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {

	public interface IGossipManager : IManagerBase {
		void receiveGossipMessage(IGossipMessageSet blockchainGossipMessageSet, PeerConnection connection);
	}

	public interface IGossipManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IManagerBase<IGossipManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IGossipManager
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     This is the blockchain maintenance thread. There to take care of our chain and handle it's state.
	/// </summary>
	public abstract class GossipManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ManagerBase<IGossipManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IGossipManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly ColoredRoutedTaskReceiver ColoredRoutedTaskReceiver;

		protected readonly IBlockchainGuidService guidService;
		protected readonly IBlockchainTimeService timeService;

		private IEventPoolProvider chainEventPoolProvider;

		// the sync workflow we keep as a reference.
		private IClientChainSyncWorkflow chainSynchWorkflow;
		private DateTime? nextBlockchainSynchCheck;
		private DateTime? nextExpiredTransactionCheck;
		private DateTime? nextWalletSynchCheck;
		private ISyncWalletWorkflow synchWalletWorkflow;

		public GossipManager(CENTRAL_COORDINATOR CentralCoordinator) : base(CentralCoordinator, 1, 500) {
			this.timeService = CentralCoordinator.BlockchainServiceSet.BlockchainTimeService;
			this.guidService = CentralCoordinator.BlockchainServiceSet.BlockchainGuidService;

			this.ColoredRoutedTaskReceiver = new ColoredRoutedTaskReceiver(this.HandleMessages);
		}

		protected new CENTRAL_COORDINATOR CentralCoordinator => base.CentralCoordinator;

		public void receiveGossipMessage(IGossipMessageSet blockchainGossipMessageSet, PeerConnection connection) {
			GossipMessageReceivedTask gossipMessageTask = new GossipMessageReceivedTask(blockchainGossipMessageSet, connection);
			this.ColoredRoutedTaskReceiver.ReceiveTask(gossipMessageTask);
		}

		protected override async Task ProcessLoop(IGossipManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
			await base.ProcessLoop(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);

			// lets keep our database clean
			await this.ColoredRoutedTaskReceiver.CheckTasks().ConfigureAwait(false);
		}

		protected virtual async Task HandleMessages(IColoredTask task) {
			if(task is GossipMessageReceivedTask messageTask) {
				await this.HandleGossipMessageReceived(messageTask).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     an external sourced gossip message was reiceved, lets handle it
		/// </summary>
		/// <param name="gossipMessageTask"></param>
		/// <exception cref="ApplicationException"></exception>
		protected virtual async Task HandleGossipMessageReceived(GossipMessageReceivedTask gossipMessageTask) {

			LockContext lockContext = null;

			if((gossipMessageTask == null) || !(gossipMessageTask.gossipMessageSet is IBlockchainGossipMessageSet blockchainGossipMessageSet)) {
				return;
			}

			try {

				if(blockchainGossipMessageSet == null) {
					throw new ApplicationException("Gossip message must be valid");
				}

				if(blockchainGossipMessageSet.BaseMessage == null) {
					throw new ApplicationException("Gossip message transaction must be valid");
				}

				PeerConnection connection = gossipMessageTask.Connection;

				if(GlobalSettings.ApplicationSettings.SynclessMode) {
					if(blockchainGossipMessageSet.BaseMessage.BaseEnvelope is IBlockEnvelope) {
						// in mobile mode, we simply do not handle any gossip messages
						NLog.Default.Information("Mobile nodes does not handle block gossip messages");

						return;
					}

					if(!GlobalSettings.Instance.NodeInfo.GossipAccepted) {
						// some apps do not handle gossip at all
						NLog.Default.Information("This node does not handle gossip messages");

						return;
					}
				}

				// ok, the first step is to ensure the message is valid. otherwise we do not handle it any further

				ValidationResult valid = await ValidateEnvelopedContent(blockchainGossipMessageSet, lockContext).ConfigureAwait(false);

				// ok, if we can't validate a message, we are most probably out of sync. if we are not already syncing, let's request one.
				if(valid == ValidationResult.ValidationResults.CantValidate) {
					// we have a condition when we may be out of sync and if we are not already syncing, we should do it
					BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, bool, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

					blockchainTask.SetAction(async (blockchainService, taskRoutingContext2, lc) => {
						await blockchainService.SynchronizeBlockchain(true, lc).ConfigureAwait(false);
					});

					// no need to wait, this can totally be async
					await this.DispatchTaskAsync(blockchainTask, lockContext).ConfigureAwait(false);
				}

				long xxHash = blockchainGossipMessageSet.BaseHeader.Hash;

				//check if we have already received it, and if we did, we update the validation status, since we just did so.
				bool isInCache = await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.CheckRegistryMessageInCache(xxHash, valid.Valid).ConfigureAwait(false);

				if((valid != ValidationResult.ValidationResults.Valid) && blockchainGossipMessageSet.BaseMessage.BaseEnvelope is IBlockEnvelope invalidatedBlockEnvelope) {

					// since this was useless, we can now free the block Id to sync otherwise
					this.FreeLockedBlock(invalidatedBlockEnvelope.BlockId, true, lockContext);
				}

				// see if we should cache the message if it's a block
				if((valid == ValidationResult.ValidationResults.CantValidate) && blockchainGossipMessageSet.BaseMessage.BaseEnvelope is IBlockEnvelope unvalidatedBlockEnvelope) {

					// ok, its a block message. we can't validate it yet. If it is close enough from our chain height, we will cache it, so we can use it later.
					long currentBlockHeight = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
					int blockGossipCacheProximityLevel = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.BlockGossipCacheProximityLevel;

					if((unvalidatedBlockEnvelope.BlockId > currentBlockHeight) && (unvalidatedBlockEnvelope.BlockId <= (currentBlockHeight + blockGossipCacheProximityLevel))) {
						try {
							await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.CacheUnvalidatedBlockGossipMessage(unvalidatedBlockEnvelope, xxHash).ConfigureAwait(false);
						} catch(Exception ex) {
							//just eat the exception if anything here, it's not so important
							NLog.Default.Error(ex, $"Failed to cache gossip block message for block Id {unvalidatedBlockEnvelope.BlockId}");
						}
					}
				}

				Task ForwardValidGossipMessage() {
					return this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.ForwardValidGossipMessage(blockchainGossipMessageSet, connection);
				}

				if(valid.Result == ValidationResult.ValidationResults.Invalid) {

					// this is the end, we go no further with an invalid message

					//TODO: what should we do here? perhaps we should log it about the peer
					NLog.Default.Error($"Gossip message received by peer {connection.ScoppedAdjustedIp} was invalid. errors: {valid}");
					await this.RequestSync(lockContext).ConfigureAwait(false);
				} else if(valid == ValidationResult.ValidationResults.CantValidate) {
					// seems we could do nothing with it. we just let it go
					NLog.Default.Verbose($"Gossip message received by peer {connection.ScoppedAdjustedIp} but could not be validated. the message will be ignored. errors: {valid}");

					await this.RequestSync(lockContext).ConfigureAwait(false);
				} else if(valid == ValidationResult.ValidationResults.EmbededKeyValid) {
					// seems we could do nothing with it. we just let it go
					NLog.Default.Verbose($"Gossip message received by peer {connection.ScoppedAdjustedIp} could not be validated, but the embeded public key was valid. errors: {valid}");

					// ok, in this case, we can at least forward it on the gossip network
					await ForwardValidGossipMessage().ConfigureAwait(false);

				} else if(valid.Valid) {

					// and since this is good or possibly valid, now we ensure it will get forwarded to our peers
					await ForwardValidGossipMessage().ConfigureAwait(false);

					// ok, if we get here, this message is fully valid!  first step, we instantiate the workflow for this gossip transaction

					if(blockchainGossipMessageSet.BaseMessage is IGossipWorkflowTriggerMessage gossipWorkflowTriggerMessage) {

						// run this reception async, so we dont lock up the gossip manager in any case. we dont await because we want to continue.
						Task task = Task.Run(async () => {

							LockContext lc = null;
							bool blockInserted = false;

							try {
								NLog.Default.Verbose($"Gossip message received by peer {connection.ScoppedAdjustedIp} was valid and is about to be processed.");

								if(blockchainGossipMessageSet.BaseMessage.WorkflowType == GossipWorkflowIDs.TRANSACTION_RECEIVED) {

									await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.InsertGossipTransaction((ITransactionEnvelope) blockchainGossipMessageSet.BaseMessage.BaseEnvelope, lc).ConfigureAwait(false);

								} else if(blockchainGossipMessageSet.BaseMessage.WorkflowType == GossipWorkflowIDs.BLOCK_RECEIVED) {

									IBlockEnvelope blockEnvelope = (IBlockEnvelope) blockchainGossipMessageSet.BaseMessage.BaseEnvelope;

									NLog.Default.Verbose($"Inserting block {blockEnvelope.BlockId} received by gossip message.");
									await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.InsertInterpretBlock(blockEnvelope.Contents.RehydratedBlock, blockEnvelope.Contents, true, lc).ConfigureAwait(false);

									blockInserted = true;
								} else if(blockchainGossipMessageSet.BaseMessage.WorkflowType == GossipWorkflowIDs.MESSAGE_RECEIVED) {
									IMessageEnvelope messageEnvelope = (IMessageEnvelope) blockchainGossipMessageSet.BaseMessage.BaseEnvelope;
									
									await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.HandleBlockchainMessage(messageEnvelope.Contents.RehydratedEvent, messageEnvelope, lc).ConfigureAwait(false);
								} else {
									throw new InvalidOperationException("Invalid gossip message type");
								}

								// ok , we are done. good job :)
								NLog.Default.Verbose($"Gossip message received by peer {connection.ScoppedAdjustedIp} was valid and was processed properly.");
							} catch(Exception ex) {

								NLog.Default.Error(ex, "Failed to process gossip message that was found as valid.");

							} finally {
								if(blockchainGossipMessageSet.BaseMessage is IGossipWorkflowTriggerMessage gossipWorkflowTriggerMessage2 && (blockchainGossipMessageSet.BaseMessage.WorkflowType == GossipWorkflowIDs.BLOCK_RECEIVED) && blockchainGossipMessageSet.BaseMessage.BaseEnvelope is IBlockEnvelope blockEnvelope) {

									// try to remove the lock on the block
									this.FreeLockedBlock(blockEnvelope.BlockId, !blockInserted, lc);
								}
							}

						}, this.CancelToken);
					}
				}
			} catch(Exception ex) {

				NLog.Default.Error(ex, "Failed to process gossip message.");

				if(blockchainGossipMessageSet.BaseMessage.BaseEnvelope is IBlockEnvelope blockEnvelope) {

					// since this was useless, we can now free the block Id to sync otherwise
					this.FreeLockedBlock(blockEnvelope.BlockId, true, lockContext);
				}
			}
		}

		protected virtual async Task<ValidationResult> ValidateEnvelopedContent(IBlockchainGossipMessageSet blockchainGossipMessageSet, LockContext lockContext) {
			ValidationResult valid = new ValidationResult();
			
			await this.CentralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateEnvelopedContent(blockchainGossipMessageSet.BaseMessage.BaseEnvelope, true, result => {
				valid = result;
			}, lockContext).ConfigureAwait(false);

			return valid;
		}
		/// <summary>
		///     free a block id from sync lock. also request a sync or not if error happened
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="failed"></param>
		private void FreeLockedBlock(BlockId blockId, bool failed, LockContext lockContext) {
			this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.FreeLockedBlock(blockId);

			// now control the sync. if we succeeded, then we reset the sync timer. otherwise, we nullify it and ask for a sync immediately.
			this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastSync = failed ? DateTimeEx.MinValue : DateTimeEx.CurrentTime;

			if(failed) {
				this.RequestSync(lockContext).WaitAndUnwrapException();
			}
		}

		private Task RequestSync(LockContext lockContext) {
			BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, bool, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction(async (service, taskRoutingContext2, lc) => {
				await service.SynchronizeBlockchain(true, lc).ConfigureAwait(false);
			});

			return this.DispatchTaskNoReturnAsync(blockchainTask, lockContext);
		}

		public class GossipMessageReceivedTask : ColoredTask {
			public readonly PeerConnection Connection;
			public readonly IGossipMessageSet gossipMessageSet;

			public GossipMessageReceivedTask(IGossipMessageSet blockchainGossipMessageSet, PeerConnection connection) {
				this.gossipMessageSet = blockchainGossipMessageSet;
				this.Connection = connection;
			}
		}
	}
}