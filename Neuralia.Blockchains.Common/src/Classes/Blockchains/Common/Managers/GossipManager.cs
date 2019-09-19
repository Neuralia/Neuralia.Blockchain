using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.BlockInsertionTransaction;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.WalletSync;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
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

		protected readonly IBlockchainGuidService guidService;
		protected readonly IBlockchainTimeService timeService;

		private IEventPoolProvider chainEventPoolProvider;

		// the sync workflow we keep as a reference.
		private IClientChainSyncWorkflow chainSynchWorkflow;
		private DateTime? nextBlockchainSynchCheck;
		private DateTime? nextExpiredTransactionCheck;
		private DateTime? nextWalletSynchCheck;
		private ISyncWalletWorkflow synchWalletWorkflow;

		protected readonly ColoredRoutedTaskReceiver ColoredRoutedTaskReceiver;

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

		protected override void ProcessLoop(IGossipManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> workflow, TaskRoutingContext taskRoutingContext) {
			base.ProcessLoop(workflow, taskRoutingContext);
			
			// lets keep our database clean
			this.ColoredRoutedTaskReceiver.CheckTasks();
		}

		protected virtual void HandleMessages(IColoredTask task) {
			if(task is GossipMessageReceivedTask messageTask) {
				this.HandleGossipMessageReceived(messageTask);
			}
		}

		/// <summary>
		///     an external sourced gossip message was reiceved, lets handle it
		/// </summary>
		/// <param name="gossipMessageTask"></param>
		/// <exception cref="ApplicationException"></exception>
		protected virtual void HandleGossipMessageReceived(GossipMessageReceivedTask gossipMessageTask) {

			try {
				if(!(gossipMessageTask.gossipMessageSet is IBlockchainGossipMessageSet blockchainGossipMessageSet)) {
					return;
				}

				if(blockchainGossipMessageSet == null) {
					throw new ApplicationException("Gossip message must be valid");
				}

				if(blockchainGossipMessageSet.BaseMessage == null) {
					throw new ApplicationException("Gossip message transaction must be valid");
				}

				PeerConnection connection = gossipMessageTask.Connection;

				if(GlobalSettings.ApplicationSettings.MobileMode) {
					if(blockchainGossipMessageSet.BaseMessage.BaseEnvelope is IBlockEnvelope) {
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
				var validationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateValidationTask<bool>();

				ValidationResult valid = new ValidationResult();

				validationTask.SetAction((validationService, taskRoutingContext) => {
					IRoutedTask validateEnvelopeContentTask = validationService.ValidateEnvelopedContent(blockchainGossipMessageSet.BaseMessage.BaseEnvelope, result => {
						valid = result;
					});

					taskRoutingContext.AddChild(validateEnvelopeContentTask);
				}, (result, taskRoutingContext) => {
					if(result.Success) {
						// ok, if we can't validate a message, we are most probably out of sync. if we are not already syncing, let's request one.
						if(valid == ValidationResult.ValidationResults.CantValidate) {
							// we have a condition when we may be out of sync and if we are not already syncing, we should do it
							var blockchainTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

							blockchainTask.SetAction((blockchainService, taskRoutingContext2) => {
								blockchainService.SynchronizeBlockchain(true);
							});

							// no need to wait, this can totally be async
							this.DispatchTaskAsync(blockchainTask);
						}

						long xxHash = blockchainGossipMessageSet.BaseHeader.Hash;

						var serializationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<bool>();

						serializationTask.SetAction((serializationService, taskRoutingContext2) => {

							//check if we have already received it, and if we did, we update the validation status, since we just did so.
							bool isInCache = serializationService.CheckRegistryMessageInCache(xxHash, valid.Valid);

							// see if we should cache the message if it's a block
							if((valid == ValidationResult.ValidationResults.CantValidate) && blockchainGossipMessageSet.BaseMessage.BaseEnvelope is IBlockEnvelope unvalidatedBlockEnvelope) {

								// ok, its a block message. we can't validate it yet. If it is close enough from our chain height, we will cache it, so we can use it later.
								long currentBlockHeight = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
								int blockGossipCacheProximityLevel = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.BlockGossipCacheProximityLevel;

								if((unvalidatedBlockEnvelope.BlockId > currentBlockHeight) && (unvalidatedBlockEnvelope.BlockId <= (currentBlockHeight + blockGossipCacheProximityLevel))) {
									try {
										serializationService.CacheUnvalidatedBlockGossipMessage(unvalidatedBlockEnvelope, xxHash);
									} catch(Exception ex) {
										//just eat the exception if anything here, it's not so important
										Log.Error(ex, $"Failed to cache gossip block message for block Id {unvalidatedBlockEnvelope.BlockId}");
									}
								}
							}
						}, (result2, taskRoutingContext2) => {
							if(result2.Success) {
								if(valid.Result == ValidationResult.ValidationResults.Invalid) {

									// this is the end, we go no further with an invalid message

									//TODO: what should we do here? perhaps we should log it about the peer
									Log.Error($"Gossip message received by peer {connection.ScoppedAdjustedIp} was invalid.");
								} else if(valid == ValidationResult.ValidationResults.CantValidate) {
									// seems we could do nothing with it. we just let it go
									Log.Verbose($"Gossip message received by peer {connection.ScoppedAdjustedIp} but could not be validated. the message will be ignored.");
								} else if(valid == ValidationResult.ValidationResults.EmbededKeyValid) {
									// seems we could do nothing with it. we just let it go
									Log.Verbose($"Gossip message received by peer {connection.ScoppedAdjustedIp} could not be validated, but the embeded public key was valid.");

									// ok, in this case, we can at least forward it on the gossip network
									this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.ForwardValidGossipMessage(blockchainGossipMessageSet, connection);

								} else if(valid.Valid) {

									// and since this is good or possibly valid, now we ensure it will get forwarded to our peers
									this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.ForwardValidGossipMessage(blockchainGossipMessageSet, connection);

									// ok, if we get here, this message is fully valid!  first step, we instantiate the workflow for this gossip transaction
									var blockchainTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

									if(blockchainGossipMessageSet.BaseMessage is IGossipWorkflowTriggerMessage gossipWorkflowTriggerMessage) {

										Action<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, TaskRoutingContext> action = null;

										if((blockchainGossipMessageSet.BaseMessage.WorkflowType == GossipWorkflowIDs.TRANSACTION_RECEIVED)) {

											action = (blockchainService, taskRoutingContext3) => {
												try {
													blockchainService.InsertGossipTransaction((ITransactionEnvelope) blockchainGossipMessageSet.BaseMessage.BaseEnvelope);
												} catch(Exception ex) {
													Log.Error(ex, "Failed to insert transaction from a received gossip message.");
												}
											};
										} else if((blockchainGossipMessageSet.BaseMessage.WorkflowType == GossipWorkflowIDs.BLOCK_RECEIVED)) {

											IBlockEnvelope blockEnvelope = (IBlockEnvelope) blockchainGossipMessageSet.BaseMessage.BaseEnvelope;

											action = (blockchainService, taskRoutingContext3) => {
												try {
													blockchainService.InsertInterpretBlock(blockEnvelope.Contents.RehydratedBlock, blockEnvelope.Contents, true);
												} catch(Exception ex) {
													Log.Error(ex, "Failed to install block from a received gossip message.");
												}
											};
										} else if((blockchainGossipMessageSet.BaseMessage.WorkflowType == GossipWorkflowIDs.MESSAGE_RECEIVED)) {
											IMessageEnvelope messageEnvelope = (IMessageEnvelope) blockchainGossipMessageSet.BaseMessage.BaseEnvelope;

											action = (blockchainService, taskRoutingContext3) => {
												try {
													blockchainService.HandleBlockchainMessage(messageEnvelope.Contents.RehydratedMessage, messageEnvelope.Contents);
												} catch(Exception ex) {
													Log.Error(ex, "Failed to handle message from a received gossip message.");
												}
											};
										} else {
											throw new ArgumentOutOfRangeException("Invalid gossip message type");
										}

										blockchainTask.SetAction(action, (result3, executionContext3) => {
											
											// clean up when we are done
											//blockchainGossipMessageSet.BaseMessage.Dispose();
										});

										this.DispatchTaskNoReturnAsync(blockchainTask);

										// ok , we are done. good job :)
										Log.Verbose($"Gossip message received by peer {connection.ScoppedAdjustedIp} was valid and was processed properly.");
									}
								}
							}
						});

						this.DispatchTaskAsync(serializationTask);
					}
				});

				this.DispatchTaskAsync(validationTask);
			} catch(Exception ex) {
				Log.Error(ex, "Failed to process gossip message.");
			}
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