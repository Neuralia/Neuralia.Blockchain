using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.Metadata;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Threading;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common {

	public interface ICoordinatorTaskDispatcher : ITaskRouter, INetworkRouter {

		BlockchainServiceSet BlockchainServiceSet { get; }

		void PostSystemEvent(SystemMessageTask messageTask, CorrelationContext correlationContext = null);
		void PostSystemEvent(BlockchainSystemEventType eventType, CorrelationContext correlationContext = null);
		void PostSystemEvent(BlockchainSystemEventType eventType, object[] parameters, CorrelationContext correlationContext = null);
		void PostSystemEvent(SystemEventGenerator generator, CorrelationContext correlationContext = null);

		void PostSystemEventImmediate(SystemMessageTask messageTask, CorrelationContext correlationContext = null);
		void PostSystemEventImmediate(BlockchainSystemEventType eventType, CorrelationContext correlationContext = null);
		void PostSystemEventImmediate(BlockchainSystemEventType eventType, object[] parameters, CorrelationContext correlationContext = null);
		void PostSystemEventImmediate(SystemEventGenerator generator, CorrelationContext correlationContext = null);
	}

	public interface ICentralCoordinator : ILoopThread, ICoordinatorTaskDispatcher {
		BlockchainType ChainId { get; }
		string ChainName { get; }

		Task<bool> CheckAvailableMemory();
		
		FileSystemWrapper FileSystem { get; }

		bool IsShuttingDown { get; }

		ChainConfigurations ChainSettings { get; }
		string GetWalletBaseDirectoryPath();
		event Action<ConcurrentBag<Task>> ShutdownRequested;
		event Action ShutdownStarting;

		
		event Func<LockContext, Task> BlockchainSynced;
		event Func<LockContext, Task> WalletSynced;
		Task TriggerWalletSyncedEvent(LockContext lockContext);
		Task TriggerBlockchainSyncedEvent(LockContext lockContext);

		Task RequestFullSync(LockContext lockContext, bool force = false);
		Task RequestBlockchainSync(bool force = false);
		Task RequestWalletSync(bool force = false);
		Task RequestWalletSync(IBlock block, bool force = false, bool? allowGrowth = null);
		Task RequestWalletSync(List<IBlock> blocks, bool force, bool mobileForce, bool? allowGrowth = null);

		Task PauseChain();
		Task ResumeChain();

		NLog.IPassthroughLogger Log { get; }
	}

	public interface ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ILoopThread<CentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>>, ICentralCoordinator
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		CHAIN_COMPONENT_PROVIDER ChainComponentProvider { get; }

		IChainDalCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDalCreationFactory { get; }
		bool IsChainSynchronizing { get; }

		bool IsChainSynchronized { get; }

		bool IsChainLikelySynchronized { get; }

		Task<bool> IsWalletSynced(LockContext lockContext);

		void PostNewGossipMessage(IBlockchainGossipMessageSet gossipMessageSet);

		void PostWorkflow(IWorkflow workflow);

		void PostImmediateWorkflow(IWorkflow<IBlockchainEventsRehydrationFactory> workflow);

		Task InitializeContents(IChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainComponentsInjection, LockContext lockContext);
	}

	public class ChainRuntimeConfiguration {

		public readonly Dictionary<string, Enums.ServiceExecutionTypes> ServiceExecutionTypes = new Dictionary<string, Enums.ServiceExecutionTypes>();
	}

	/// <summary>
	///     this is the main head of the transaction chain ecosystem. it manages all services and their coexistance.null Always
	///     create via the ChainCreationFactory
	///     one of the main functions of the coordinator is too coordinate and maintain workflows.
	/// </summary>
	public abstract class CentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : LoopThread<CentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>>, ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		protected readonly ChainRuntimeConfiguration chainRuntimeConfiguration;

		public NLog.IPassthroughLogger Log => this.ChainComponentProvider.LoggingProviderBase;
		
		protected readonly ColoredRoutedTaskReceiver ColoredRoutedTaskReceiver;
		protected readonly FileSystemWrapper fileSystem;

		protected readonly Dictionary<string, (IRoutedTaskRoutingThread service, Enums.ServiceExecutionTypes executionType)> services = new Dictionary<string, (IRoutedTaskRoutingThread service, Enums.ServiceExecutionTypes executionType)>();

		protected readonly WorkflowCoordinator<IWorkflow, IBlockchainEventsRehydrationFactory> workflowCoordinator;
		private ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> centralCoordinatorImplementation;

		private IBlockChainInterface chainInterface;

		public CentralCoordinator(BlockchainType chainId, BlockchainServiceSet serviceSet, ChainRuntimeConfiguration chainRuntimeConfiguration, FileSystemWrapper fileSystem) {
			this.BlockchainServiceSet = serviceSet;
			this.chainRuntimeConfiguration = chainRuntimeConfiguration;
			this.fileSystem = fileSystem;

			if(this.fileSystem == null) {
				this.fileSystem = FileSystemWrapper.CreatePhysical();
			}

			//TODO: are these maximum values correct?
			// ensure we have at least 2 workflow spaces per peer and a little more for the rest
			int maximumWorkflows = (GlobalSettings.ApplicationSettings.MaxPeerCount * 15) + 15;

			int? maximumThreadCounts = GlobalSettings.ApplicationSettings.GetChainConfiguration(chainId).MaxWorkflowParallelCount;

			if(maximumThreadCounts.HasValue) {
				maximumWorkflows = Math.Min(maximumWorkflows, maximumThreadCounts.Value);
			}

			this.workflowCoordinator = new WorkflowCoordinator<IWorkflow, IBlockchainEventsRehydrationFactory>(serviceSet, maximumWorkflows);

			this.ColoredRoutedTaskReceiver = new ColoredRoutedTaskReceiver(this.HandleMessages);

			this.ChainId = chainId;

			// lets make sure we capture and handle important exceptions!!
			AppDomain.CurrentDomain.FirstChanceException += this.CurrentDomainOnFirstChanceException;
		}

		protected virtual void CurrentDomainOnFirstChanceException(object sender, FirstChanceExceptionEventArgs e) {
			if(e.Exception is BlockchainException bcex && (bcex.BlockchainType == this.ChainId)) {
				if(bcex is UnrecognizedElementException ueex) {
					this.Log.Fatal(ueex, ueex.Message);
					this.PostSystemEventImmediate(SystemEventGenerator.RequireNodeUpdate(ueex.BlockchainType.Value, ueex.ChainName), new CorrelationContext());

					//TODO: what else. should we stop the chain?
				} else if(bcex is ReportableException rex) {
					// this is an important message we must report to the user
					this.PostSystemEventImmediate(SystemEventGenerator.RaiseAlert(rex), new CorrelationContext());
				}
			}
		}

		public async Task<bool> CheckAvailableMemory() {
			if(!(await MemoryCheck.Instance.CheckAvailableMemory(GlobalSettings.ApplicationSettings).ConfigureAwait(false))) {
				this.UrgentShutdown();

				return false;
			}

			return true;
		}

		private Timer urgentShutdownTimer;
		
		protected virtual void UrgentShutdown() {
			//TODO: what else. should we stop the chain?
			this.PostSystemEventImmediate(SystemEventGenerator.RequestShutdown());

			if(this.urgentShutdownTimer == null) {
				
				// try to clear some memory
				
				try {
					this.ChainComponentProvider.ChainDataWriteProviderBase.ClearBlocksCache();
				} catch {
					// do nothing
				}
				
				try {
					this.ChainComponentProvider.WalletProviderBase.ClearSynthesizedBlocksCache();
				} catch {
					// do nothing
				}
				try {
					this.ChainComponentProvider.ChainMiningProviderBase.ClearElectionBlockCache();
				} catch {
					// do nothing
				}

				//TODO: what else can we clear here?
				
				// this below might be a bad idea
				// try {
				// 	this.ChainComponentProvider.ChainNetworkingProviderBase.UrgentClearConnections();
				// } catch {
				// 	// do nothing
				// }
				
				GC.Collect(2, GCCollectionMode.Forced, true, true);
				
				// start a timer so we can force a shutdown if it takes too long
				Repeater.Repeat(() => {
					this.urgentShutdownTimer = new Timer(state => {

						Environment.Exit(1);

					}, this, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
				});
			}

			// we really want to shutdown early
			this.RequestShutdown().WaitAndUnwrapException();
		}

		public event Func<LockContext, Task> BlockchainSynced;
		public event Func<LockContext, Task> WalletSynced;

		public async Task TriggerWalletSyncedEvent(LockContext lockContext) {
			if(this.WalletSynced != null) {
				await this.WalletSynced(lockContext).ConfigureAwait(false);
			}
		}

		public async Task TriggerBlockchainSyncedEvent(LockContext lockContext) {
			if(this.BlockchainSynced != null) {
				await this.BlockchainSynced(lockContext).ConfigureAwait(false);
			}

		}

		public async Task RequestFullSync(LockContext lockContext, bool force = false) {

			if(!this.IsChainLikelySynchronized) {
				await this.RequestBlockchainSync(force).ConfigureAwait(false);
			} else if(!await this.IsWalletSynced(lockContext).ConfigureAwait(false)) {
				await this.RequestWalletSync(force).ConfigureAwait(false);
			}
		}

		public Task RequestBlockchainSync(bool force = false) {
			BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, bool, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainTask = this.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction(async (service, taskRoutingContext2, lc) => {
				await service.SynchronizeBlockchain(force, lc).ConfigureAwait(false);
			});

			blockchainTask.Caller = null;

			return this.RouteTask(blockchainTask);
		}

		public Task RequestWalletSync(bool force = false) {
			BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, bool, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainTask = this.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction(async (service, taskRoutingContext2, lc) => {
				await service.SynchronizeWallet(force, lc).ConfigureAwait(false);
			});

			blockchainTask.Caller = null;

			return this.RouteTask(blockchainTask);
		}

		public Task RequestWalletSync(IBlock block, bool force = false, bool? allowGrowth = null) {
			BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, bool, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainTask = this.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction(async (service, taskRoutingContext2, lc) => {
				await service.SynchronizeWallet(block, force, lc, allowGrowth).ConfigureAwait(false);
			});

			blockchainTask.Caller = null;

			return this.RouteTask(blockchainTask);
		}

		public Task RequestWalletSync(List<IBlock> blocks, bool force, bool mobileForce, bool? allowGrowth = null) {
			BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, bool, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainTask = this.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction(async (service, taskRoutingContext2, lc) => {
				await service.SynchronizeWallet(blocks, force, mobileForce, lc, allowGrowth).ConfigureAwait(false);
			});

			blockchainTask.Caller = null;

			return this.RouteTask(blockchainTask);
		}

		public Task PauseChain() {
			return this.ChainComponentProvider.WalletProviderBase.Pause();
		}

		public Task ResumeChain() {
			return this.ChainComponentProvider.WalletProviderBase.Resume();
		}

		public IChainDalCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDalCreationFactory => this.ChainComponentProvider.ChainFactoryProviderBase.ChainDalCreationFactoryBase;

		public BlockchainType ChainId { get; }
		public string ChainName => BlockchainTypes.GetBlockchainTypeName(this.ChainId);

		public CHAIN_COMPONENT_PROVIDER ChainComponentProvider { get; private set; }

		public ChainConfigurations ChainSettings => this.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

		public FileSystemWrapper FileSystem => this.fileSystem;

		public override async Task Start() {

			this.IsShuttingDown = false;
			await base.Start().ConfigureAwait(false);

			await this.StartWorkers().ConfigureAwait(false);

			this.IsShuttingDown = false;
		}

		public override async Task Stop() {
			try {
				if(this.IsStarted && !this.IsShuttingDown) {
					this.IsShuttingDown = true;

					ConcurrentBag<Task> waitFlags = new ConcurrentBag<Task>();

					if(this.ShutdownRequested != null) {
						try {
							this.ShutdownRequested(waitFlags);
						} catch {
							
						}
					}

					// lets wait for any wait flags to complete
					if(waitFlags.Any()) {
						int waitTime = 30;

						this.Log.Information($"We are preparing to stop chain {BlockchainTypes.GetBlockchainTypeName(this.ChainId)}, but we received {waitFlags.Count} requests to wait by pending services. we will wait up to {waitTime} seconds");
						Task.WaitAll(waitFlags.ToArray(), TimeSpan.FromSeconds(waitTime));
						this.Log.Information($"We are done waiting for pending services for chain  {BlockchainTypes.GetBlockchainTypeName(this.ChainId)}. Proceeding with shutdown...");
					}

					this.Log.Information($"We are shutting down chain '{BlockchainTypes.GetBlockchainTypeName(this.ChainId)}'...");

					if(this.ShutdownStarting != null) {
						try {
							this.ShutdownStarting();
						} catch {
							
						}
					}

					// if we had existing tasks, either break, or stop them all
					try {
						await this.StopWorkers().ConfigureAwait(false);
					} catch {
						
					}

				}
			} finally {
				await base.Stop().ConfigureAwait(false);
			}
		}

		public string GetWalletBaseDirectoryPath() {
			return this.ChainComponentProvider.WalletProviderBase.GetSystemFilesDirectoryPath();
		}

		/// <summary>
		///     insert a new workflow in our workflow ecosystem
		/// </summary>
		/// <param name="workflow"></param>
		public void PostWorkflow(IWorkflow workflow) {

			this.workflowCoordinator.AddWorkflow(workflow).WaitAndUnwrapException();
		}

		/// <summary>
		///     insert a new workflow in our workflow ecosystem. start the workflow immediately
		/// </summary>
		/// <param name="workflow"></param>
		public void PostImmediateWorkflow(IWorkflow<IBlockchainEventsRehydrationFactory> workflow) {
			this.workflowCoordinator.AddImmediateWorkflow(workflow).WaitAndUnwrapException();
		}

		public virtual async Task<bool> RouteTask(IRoutedTask task, string destination) {

			if(string.IsNullOrWhiteSpace(destination)) {
				throw new ApplicationException("A task must hav a destination set");
			}

			InternalRoutedTask routingTask = (InternalRoutedTask) task;
			routingTask.RoutingStatus = RoutedTask.RoutingStatuses.Dispatched;
			routingTask.Destination = destination;

			if(task.Destination == Enums.INTERFACE) {
				this.chainInterface.ReceiveTask(task);

				return true;
			}

			if(this.services.ContainsKey(task.Destination)) {
				if(this.services[task.Destination].executionType == Enums.ServiceExecutionTypes.Threaded) {
					this.services[task.Destination].service.ReceiveTask(task);
				} else if(this.services[task.Destination].executionType == Enums.ServiceExecutionTypes.Synchronous) {
					this.services[task.Destination].service.ReceiveTaskSynchronous(task);
				} else if(this.services[task.Destination].executionType == Enums.ServiceExecutionTypes.None) {
					this.Log.Verbose($"Service {task.Destination} is deactivated and the task will be ignored.");
				} else {
					throw new ApplicationException("Execution type is not supported");
				}

				return true;
			}

			return false;
		}

		public virtual Task<bool> RouteTask(IRoutedTask task) {
			return this.RouteTask(task, task.Destination);
		}

		/// <summary>
		///     determine if we are in a wallet transaction and if the task is from this very active thread
		/// </summary>
		/// <param name="task"></param>
		/// <returns></returns>
		public async Task<bool> IsWalletProviderTransaction(IRoutedTask task) {
			if(task is InternalRoutedTask innternalRoutedTask) {
				return (innternalRoutedTask.Caller != null) && this.ChainComponentProvider.WalletProviderBase.IsActiveTransactionThread(innternalRoutedTask.CallerThreadId);
			}

			return false;
		}

		/// <summary>
		///     This method allows us to post a new gossip message to our peers.
		/// </summary>
		/// <remarks>USE WITH CAUTION!!!!   peers can blacklist us if we abuse it.</remarks>
		/// <param name="gossipMessageSet"></param>
		public void PostNewGossipMessage(IBlockchainGossipMessageSet gossipMessageSet) {

			this.ChainComponentProvider.ChainNetworkingProviderBase.PostNewGossipMessage(gossipMessageSet);
		}

		public async Task<bool> IsWalletSynced(LockContext lockContext) {

			bool? walletSynced = await this.ChainComponentProvider.WalletProviderBase.SyncedNoWait(lockContext).ConfigureAwait(false);

			return walletSynced.HasValue && walletSynced.Value;

		}

		public bool IsChainSynchronizing { get; set; }

		public bool IsChainSynchronized => this.ChainComponentProvider.ChainStateProviderBase.IsChainSynced;

		public bool IsChainLikelySynchronized => this.ChainComponentProvider.ChainStateProviderBase.IsChainLikelySynchronized;

		public virtual async Task InitializeContents(IChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainComponentsInjection, LockContext lockContext) {
			if(this.ChainId == BlockchainTypes.Instance.None) {
				throw new ApplicationException("The chain ID must be set and can not be 0");
			}

			this.BuildServices(chainComponentsInjection);

			if(this.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase == null) {
				throw new ApplicationException("workflow factory must be set and can not be null");
			}

			// make sure we initialize our providers
			await this.ChainComponentProvider.Initialize(lockContext).ConfigureAwait(false);

			// now register our gossip message info analyzer
			if(!ServerMessageGroupManifestWorkflow.GossipMetadataAnalysers.ContainsKey(this.ChainId)) {
				ServerMessageGroupManifestWorkflow.GossipMetadataAnalysers.Add(this.ChainId, new MessageGroupGossipMetadataAnalyser<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>(this));
			}

			if(!ServerMessageGroupManifestWorkflow.ChainRehydrationFactories.ContainsKey(this.ChainId)) {
				ServerMessageGroupManifestWorkflow.ChainRehydrationFactories.Add(this.ChainId, this.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);
			}

			// ensure we track the accounts we need
			this.ChainComponentProvider.AccountSnapshotsProviderBase.StartTrackingConfigAccounts();
		}

		/// <summary>
		///     Route a network message to the proper workflow, or if applicable, instantiate a new one
		/// </summary>
		/// <param name="header"></param>
		/// <param name="data"></param>
		/// <param name="connection"></param>
		/// <param name="messageSet"></param>
		public void RouteNetworkMessage(IRoutingHeader header, SafeArrayHandle data, PeerConnection connection) {
			MessageReceivedTask messageTask = new MessageReceivedTask(header, data, connection);
			this.ColoredRoutedTaskReceiver.ReceiveTask(messageTask);
		}

		/// <summary>
		///     a receive gossip messages and redirect to proper facilities
		/// </summary>
		/// <param name="gossipMessageSet"></param>
		/// <param name="connection"></param>
		/// <exception cref="ApplicationException"></exception>
		public void RouteNetworkGossipMessage(IGossipMessageSet gossipMessageSet, PeerConnection connection) {

			((IGossipManager) this.services[Enums.GOSSIP_SERVICE].service).receiveGossipMessage(gossipMessageSet, connection);
		}

		public BlockchainServiceSet BlockchainServiceSet { get; }

		public bool IsShuttingDown { get; private set; }
		public event Action<ConcurrentBag<Task>> ShutdownRequested;
		public event Action ShutdownStarting;

		protected virtual async Task HandleMessages(IColoredTask task) {
			if(task is MessageReceivedTask messageTask) {
				await this.HandleMesageReceived(messageTask).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     a network message was received for our chain, let's check it and either trigger a new workflow, or route it to its
		///     owner
		/// </summary>
		/// <param name="messageTask"></param>
		/// <exception cref="ApplicationException"></exception>
		protected virtual async Task HandleMesageReceived(MessageReceivedTask messageTask) {

			try {
				if(messageTask.header.ChainId != this.ChainId) {
					throw new ApplicationException("a message was forwarded for a chain ID we do not support.");
				}

				if(messageTask.header is GossipHeader) {
					throw new ApplicationException("Gossip messages can not be received through this facility.");
				}

				if(messageTask.header is TargettedHeader targettedHeader) {
					// this is a targeted header, its meant only for us

					IBlockchainTargettedMessageSet messageSet = (IBlockchainTargettedMessageSet) this.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.Rehydrate(messageTask.data, targettedHeader, this.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

					WorkflowTracker<IWorkflow, IBlockchainEventsRehydrationFactory> workflowTracker = new WorkflowTracker<IWorkflow, IBlockchainEventsRehydrationFactory>(messageTask.Connection, messageSet.Header.WorkflowCorrelationId, messageSet.Header.WorkflowSessionId, messageSet.Header.OriginatorId, this.ChainComponentProvider.ChainNetworkingProviderBase.MyClientUuid, this.workflowCoordinator);

					if(messageSet.Header.IsWorkflowTrigger && messageSet is IBlockchainTriggerMessageSet triggerMessageSet) {
						// route the message
						if(triggerMessageSet.BaseMessage is IWorkflowTriggerMessage workflowTriggerMessage) {

							if(!workflowTracker.WorkflowExists()) {
								// create a new workflow
								ITargettedNetworkingWorkflow<IBlockchainEventsRehydrationFactory> workflow = (ITargettedNetworkingWorkflow<IBlockchainEventsRehydrationFactory>) this.ChainComponentProvider.ChainFactoryProviderBase.ServerWorkflowFactoryBase.CreateResponseWorkflow(triggerMessageSet, messageTask.Connection);

								await this.workflowCoordinator.AddWorkflow(workflow).ConfigureAwait(false);
							}
						} else {
							// this means we did not pass the trigger filter above, it could be an evil trigger and we default
							throw new ApplicationException("An invalid trigger was sent");
						}
					} else {
						if(messageSet.BaseMessage is IWorkflowTriggerMessage<IBlockchainEventsRehydrationFactory>) {
							throw new ApplicationException("We have a cognitive dissonance here. The trigger flag is not set, but the message type is a workflow trigger");
						}

						if(messageSet.Header.IsWorkflowTrigger) {
							throw new ApplicationException("We have a cognitive dissonance here. The trigger flag is set, but the message type is not a workflow trigger");
						}

						// forward the message to the right Verified workflow
						// this method wlil ensure we get the right workflow id for our connection

						// now we verify if this message originator was us

						if(workflowTracker.GetActiveWorkflow() is ITargettedNetworkingWorkflow<IBlockchainEventsRehydrationFactory> workflow) {

							workflow.ReceiveNetworkMessage(messageSet);
						} else {
							this.Log.Verbose($"The message references a workflow correlation ID '{messageSet.Header.WorkflowCorrelationId}' and session Id '{messageSet.Header.WorkflowSessionId}' which does not exist");
						}
					}
				}
			} finally {
				messageTask.data?.Return();
			}
		}

		protected ITargettedNetworkingWorkflow<IBlockchainEventsRehydrationFactory> GetActiveWorkflow(PeerConnection peerConnection, uint correlationId, uint? sessionId, Guid originatorId) {
			WorkflowId workflowId = new NetworkWorkflowId(peerConnection.ClientUuid, correlationId, sessionId);

			// now we verify if this message originator was us. if it was, we override the client ID
			if(originatorId == this.ChainComponentProvider.ChainNetworkingProviderBase.MyClientUuid) {
				workflowId = new NetworkWorkflowId(this.ChainComponentProvider.ChainNetworkingProviderBase.MyClientUuid, correlationId, sessionId);
			}

			return this.workflowCoordinator.GetWorkflow(workflowId) as ITargettedNetworkingWorkflow<IBlockchainEventsRehydrationFactory>;

		}

		/// <summary>
		///     jai pese le bouteon ""
		///     Ensure that the children provide the proper service implementations
		/// </summary>
		protected void BuildServices(IChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainComponentsInjection) {

			this.ChainComponentProvider = chainComponentsInjection.ChainComponentProvider;

			this.chainInterface = chainComponentsInjection.chainInterface;

			this.SetServices(chainComponentsInjection);
		}

		protected void AddService(string serviceName, IRoutedTaskRoutingThread service) {

			Enums.ServiceExecutionTypes executionTypes = Enums.ServiceExecutionTypes.Threaded;

			// check if we have a mode override
			if(this.chainRuntimeConfiguration?.ServiceExecutionTypes.ContainsKey(serviceName) ?? false) {
				executionTypes = this.chainRuntimeConfiguration.ServiceExecutionTypes[serviceName];
			}

			if(executionTypes == Enums.ServiceExecutionTypes.Threaded) {
				service.Synchronous = false;
			} else if(executionTypes == Enums.ServiceExecutionTypes.Synchronous) {
				service.Synchronous = true;
			} else if(executionTypes == Enums.ServiceExecutionTypes.None) {
				service.Synchronous = true;
			}

			this.services.Add(serviceName, (service, executionTypes));
		}

		protected virtual void SetServices(IChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainComponentsInjection) {
			this.AddService(Enums.BLOCKCHAIN_SERVICE, chainComponentsInjection.blockchainManager);
			this.AddService(Enums.GOSSIP_SERVICE, chainComponentsInjection.gossipManager);
		}

		protected virtual async Task StartWorkers(bool killExistingTasks = true) {
			try {

				if(!killExistingTasks && this.services.Values.Any(s => !s.service.IsCompleted)) {
					throw new ApplicationException("There are still some running tasks in the list. Cancel them first");
				}

				await this.StopWorkers().ConfigureAwait(false);

				foreach(KeyValuePair<string, (IRoutedTaskRoutingThread service, Enums.ServiceExecutionTypes executionType)> service in this.services) {
					if(service.Value.executionType == Enums.ServiceExecutionTypes.Threaded) {
						await service.Value.service.Start().ConfigureAwait(false);
					} else if(service.Value.executionType == Enums.ServiceExecutionTypes.Synchronous) {
						// we will be using a synchronous model. let's set it up
						await service.Value.service.StartSync().ConfigureAwait(false);
					} else if(service.Value.executionType == Enums.ServiceExecutionTypes.None) {
						// this service is deactivated. it does nothing

					}
				}
			} catch(Exception ex) {
				this.Log.Error(ex, "Failed to start services");

				throw ex;
			}
		}

		protected virtual async Task StopWorkers() {
			try {
				// if we had existing tasks, either break, or stop them all
				foreach(KeyValuePair<string, (IRoutedTaskRoutingThread service, Enums.ServiceExecutionTypes executionType)> service in this.services) {
					if(service.Value.executionType == Enums.ServiceExecutionTypes.Threaded) {
						await service.Value.service.Stop().ConfigureAwait(false);
					} else if(service.Value.executionType == Enums.ServiceExecutionTypes.Synchronous) {
						await service.Value.service.StopSync().ConfigureAwait(false);
					}
				}

				Task.WaitAll(this.services.Where(ts => (ts.Value.service.IsCompleted == false) && ts.Value.service.IsStarted && (ts.Value.executionType == Enums.ServiceExecutionTypes.Threaded)).Select(ts => ts.Value.service.Task).ToArray(), 1000 * 20);
			} catch(Exception ex) {
				this.Log.Error(ex, "Failed to stop controllers");

				throw ex;
			}
		}

		public Task RequestShutdown() {

			this.chainInterface.RequestShutdown();

			return this.Stop();
		}

		protected override Task ProcessLoop(LockContext lockContext) {
			this.CheckShouldCancel();

			return this.ColoredRoutedTaskReceiver.CheckTasks();
		}

		protected override async Task DisposeAllAsync() {

			await this.Stop().ConfigureAwait(false);

			// and any providers
			this.ChainComponentProvider?.Dispose();

			// dispose them all
			foreach((IRoutedTaskRoutingThread service, Enums.ServiceExecutionTypes executionType) in this.services.Values) {
				try {
					service.Dispose();
				} catch {
				}
			}

			this.workflowCoordinator.Dispose();

		}

		public class MessageReceivedTask : ColoredTask {
			public readonly PeerConnection Connection;
			public readonly SafeArrayHandle data;
			public readonly IRoutingHeader header;

			public MessageReceivedTask(IRoutingHeader header, SafeArrayHandle data, PeerConnection connection) {
				this.data = data;
				this.Connection = connection;
				this.header = header;
			}
		}

	#region System events

		public void PostSystemEvent(SystemMessageTask messageTask, CorrelationContext correlationContext = null) {
			// for now, only the interface is interrested in system messages
			this.chainInterface.ReceiveChainMessageTask(messageTask);
		}

		/// <summary>
		///     Post a system event
		/// </summary>
		/// <param name="eventType"></param>
		public void PostSystemEvent(BlockchainSystemEventType eventType, CorrelationContext correlationContext = null) {
			this.PostSystemEvent(eventType, null, correlationContext);
		}

		public void PostSystemEvent(BlockchainSystemEventType eventType, object[] parameters, CorrelationContext correlationContext = null) {
			//TODO refactor the post system events system
			SystemMessageTask systemMessage = new SystemMessageTask(eventType, parameters, correlationContext);

			this.PostSystemEvent(systemMessage);
		}

		public void PostSystemEvent(SystemEventGenerator generator, CorrelationContext correlationContext = null) {
			if(generator == null) {
				return;
			}

			//TODO refactor the post system events system
			SystemMessageTask systemMessage = new SystemMessageTask(generator.EventType, generator.Parameters, correlationContext);

			this.PostSystemEvent(systemMessage);
		}

		public void PostSystemEventImmediate(SystemMessageTask messageTask, CorrelationContext correlationContext = null) {
			// for now, only the interface is interrested in system messages
			this.chainInterface.ReceiveChainMessageTaskImmediate(messageTask);
		}

		/// <summary>
		///     Post a system event
		/// </summary>
		/// <param name="eventType"></param>
		public void PostSystemEventImmediate(BlockchainSystemEventType eventType, CorrelationContext correlationContext = null) {
			this.PostSystemEventImmediate(eventType, null, correlationContext);
		}

		public void PostSystemEventImmediate(BlockchainSystemEventType eventType, object[] parameters, CorrelationContext correlationContext = null) {
			//TODO refactor the post system events system
			SystemMessageTask systemMessage = new SystemMessageTask(eventType, parameters, correlationContext);

			this.PostSystemEventImmediate(systemMessage);
		}

		public void PostSystemEventImmediate(SystemEventGenerator generator, CorrelationContext correlationContext = null) {
			if(generator == null) {
				return;
			}

			//TODO refactor the post system events system
			SystemMessageTask systemMessage = new SystemMessageTask(generator.EventType, generator.Parameters, correlationContext);

			this.PostSystemEventImmediate(systemMessage);
		}

	#endregion

	}

}