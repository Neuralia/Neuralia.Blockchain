using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.ExceptionServices;
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
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common {

	public interface ICoordinatorTaskDispatcher : ITaskRouter, INetworkRouter {

		BlockchainServiceSet BlockchainServiceSet { get; }

		void PostSystemEvent(SystemMessageTask messageTask, CorrelationContext? correlationContext = null);
		void PostSystemEvent(BlockchainSystemEventType eventType, CorrelationContext? correlationContext = null);
		void PostSystemEvent(BlockchainSystemEventType eventType, object[] parameters, CorrelationContext? correlationContext = null);
		void PostSystemEvent(SystemEventGenerator generator, CorrelationContext? correlationContext = null);
		
		void PostSystemEventImmediate(SystemMessageTask messageTask, CorrelationContext? correlationContext = null);
		void PostSystemEventImmediate(BlockchainSystemEventType eventType, CorrelationContext? correlationContext = null);
		void PostSystemEventImmediate(BlockchainSystemEventType eventType, object[] parameters, CorrelationContext? correlationContext = null);
		void PostSystemEventImmediate(SystemEventGenerator generator, CorrelationContext? correlationContext = null);
		
		
	}

	public interface ICentralCoordinator : ILoopThread, ICoordinatorTaskDispatcher {
		BlockchainType ChainId { get; }
		string ChainName { get; }

		IFileSystem FileSystem { get; }

		bool IsShuttingDown { get; }

		ChainConfigurations ChainSettings { get; }
		string GetWalletBaseDirectoryPath();
		event Action<ConcurrentBag<Task>> ShutdownRequested;
		event Action ShutdownStarting;

		event Action BlockchainSynced;
		event Action WalletSynced;
		void TriggerWalletSyncedEvent();
		void TriggerBlockchainSyncedEvent();

		void RequestFullSync(bool force = false);
		void RequestBlockchainSync(bool force = false);
		void RequestWalletSync(bool force = false);
		void RequestWalletSync(IBlock block, bool force = false, bool? allowGrowth = null);
		
		void Pause();
		void Resume();
	}

	public interface ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ILoopThread<CentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>>, ICentralCoordinator
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		CHAIN_COMPONENT_PROVIDER ChainComponentProvider { get; }

		IChainDalCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDalCreationFactory { get; }

		bool IsWalletSynced { get; }
		bool IsChainSynchronizing { get; }

		bool IsChainSynchronized { get; }

		bool IsChainLikelySynchronized { get; }

		void PostNewGossipMessage(IBlockchainGossipMessageSet gossipMessageSet);

		void PostWorkflow(IWorkflow<IBlockchainEventsRehydrationFactory> workflow);

		void PostImmediateWorkflow(IWorkflow<IBlockchainEventsRehydrationFactory> workflow);

		void InitializeContents(IChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainComponentsInjection);
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

		protected readonly ColoredRoutedTaskReceiver ColoredRoutedTaskReceiver;
		protected readonly IFileSystem fileSystem;

		protected readonly Dictionary<string, (IRoutedTaskRoutingThread service, Enums.ServiceExecutionTypes executionType)> services = new Dictionary<string, (IRoutedTaskRoutingThread service, Enums.ServiceExecutionTypes executionType)>();

		protected readonly WorkflowCoordinator<IWorkflow<IBlockchainEventsRehydrationFactory>, IBlockchainEventsRehydrationFactory> workflowCoordinator;
		private ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> centralCoordinatorImplementation;

		private IBlockChainInterface chainInterface;

		public CentralCoordinator(BlockchainType chainId, BlockchainServiceSet serviceSet, ChainRuntimeConfiguration chainRuntimeConfiguration, IFileSystem fileSystem) {
			this.BlockchainServiceSet = serviceSet;
			this.chainRuntimeConfiguration = chainRuntimeConfiguration;
			this.fileSystem = fileSystem;

			if(this.fileSystem == null) {
				this.fileSystem = new FileSystem();
			}

			// ensure we have at least 2 workflow spaces per peer and a little more for the rest
			int maximumWorkflows = (GlobalSettings.ApplicationSettings.MaxPeerCount * 7) + 10;

			var maximumThreadCounts = GlobalSettings.ApplicationSettings.GetChainConfiguration(chainId).MaxWorkflowParallelCount;

			if(maximumThreadCounts.HasValue) {
				maximumWorkflows = Math.Min(maximumWorkflows, maximumThreadCounts.Value);
			}

			this.workflowCoordinator = new WorkflowCoordinator<IWorkflow<IBlockchainEventsRehydrationFactory>, IBlockchainEventsRehydrationFactory>(serviceSet, maximumWorkflows);

			this.ColoredRoutedTaskReceiver = new ColoredRoutedTaskReceiver(this.HandleMessages);

			this.ChainId = chainId;
			
			// lets make sure we capture and handle important exceptions!!
			AppDomain.CurrentDomain.FirstChanceException += (object sender, FirstChanceExceptionEventArgs e) => {

				if(e.Exception is UnrecognizedElementException ex && ex.BlockchainType == chainId) {
					Log.Fatal(ex, ex.Message);
					this.PostSystemEventImmediate(SystemEventGenerator.RequireNodeUpdate(ex.BlockchainType.Value, ex.ChainName), new CorrelationContext());
					//TODO: what else. should we stop the chain?
				}
			};
		}

		public event Action BlockchainSynced;
		public event Action WalletSynced;

		public void TriggerWalletSyncedEvent() {
			this.WalletSynced?.Invoke();
		}

		public void TriggerBlockchainSyncedEvent() {
			this.BlockchainSynced?.Invoke();
		}

		public void RequestFullSync(bool force = false) {

			if(!this.IsChainLikelySynchronized) {
				this.RequestBlockchainSync(force);
			}
			else if(!this.IsWalletSynced) {
				this.RequestWalletSync(force);
			}
		}
		
		public void RequestBlockchainSync(bool force = false) {
			var blockchainTask = this.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction((service, taskRoutingContext2) => {
				service.SynchronizeBlockchain(force);
			});

			blockchainTask.Caller = null;
			this.RouteTask(blockchainTask);
		}

		public void RequestWalletSync(bool force = false) {
			var blockchainTask = this.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction((service, taskRoutingContext2) => {
				service.SynchronizeWallet(force);
			});

			blockchainTask.Caller = null;
			this.RouteTask(blockchainTask);
		}

		public void RequestWalletSync(IBlock block, bool force = false, bool? allowGrowth = null) {
			var blockchainTask = this.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

			blockchainTask.SetAction((service, taskRoutingContext2) => {
				service.SynchronizeWallet(block, force, allowGrowth);
			});

			blockchainTask.Caller = null;
			this.RouteTask(blockchainTask);
		}

		public void Pause() {
			this.ChainComponentProvider.WalletProviderBase.Pause();
		}

		public void Resume() {
			this.ChainComponentProvider.WalletProviderBase.Resume();
		}

		public IChainDalCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDalCreationFactory => this.ChainComponentProvider.ChainFactoryProviderBase.ChainDalCreationFactoryBase;

		public BlockchainType ChainId { get; }
		public string ChainName => BlockchainTypes.GetBlockchainTypeName(this.ChainId);

		public CHAIN_COMPONENT_PROVIDER ChainComponentProvider { get; private set; }

		public ChainConfigurations ChainSettings => this.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

		public IFileSystem FileSystem => this.fileSystem;

		public override void Start() {

			this.IsShuttingDown = false;
			base.Start();

			this.StartWorkers();

			this.IsShuttingDown = false;
		}

		public override void Stop() {
			try {
				if(this.IsStarted && !this.IsShuttingDown) {
					this.IsShuttingDown = true;

					var waitFlags = new ConcurrentBag<Task>();
					this.ShutdownRequested?.Invoke(waitFlags);

					// lets wait for any wait flags to complete
					if(waitFlags.Any()) {
						int waitTime = 20;

						Log.Information($"We are preparing to stop chain {BlockchainTypes.GetBlockchainTypeName(this.ChainId)}, but we received {waitFlags.Count()} requests to wait by pending services. we will wait up to {waitTime} seconds");
						Task.WaitAll(waitFlags.ToArray(), TimeSpan.FromSeconds(waitTime));
						Log.Information($"We are done waiting for pending services for chain  {BlockchainTypes.GetBlockchainTypeName(this.ChainId)}. Proceeding with shutdown...");
					}

					Log.Information($"We are shutting down chain '{BlockchainTypes.GetBlockchainTypeName(this.ChainId)}'...");
					this.ShutdownStarting?.Invoke();

					// if we had existing tasks, either break, or stop them all
					this.StopWorkers();

				}
			} finally {
				base.Stop();
			}
		}

		public string GetWalletBaseDirectoryPath() {
			return this.ChainComponentProvider.WalletProviderBase.GetSystemFilesDirectoryPath();
		}

		/// <summary>
		///     insert a new workflow in our workflow ecosystem
		/// </summary>
		/// <param name="workflow"></param>
		public void PostWorkflow(IWorkflow<IBlockchainEventsRehydrationFactory> workflow) {

			this.workflowCoordinator.AddWorkflow(workflow);
		}

		/// <summary>
		///     insert a new workflow in our workflow ecosystem. start the workflow immediately
		/// </summary>
		/// <param name="workflow"></param>
		public void PostImmediateWorkflow(IWorkflow<IBlockchainEventsRehydrationFactory> workflow) {
			this.workflowCoordinator.AddImmediateWorkflow(workflow);
		}

		public virtual bool RouteTask(IRoutedTask task, string destination) {

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
					Log.Verbose($"Service {task.Destination} is deactivated and the task will be ignored.");
				} else {
					throw new ApplicationException("Execution type is not supported");
				}

				return true;
			}

			return false;
		}

		public virtual bool RouteTask(IRoutedTask task) {
			return this.RouteTask(task, task.Destination);
		}

		/// <summary>
		///     determine if we are in a wallet transaction and if the task is from this very active thread
		/// </summary>
		/// <param name="task"></param>
		/// <returns></returns>
		public bool IsWalletProviderTransaction(IRoutedTask task) {
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

		public bool IsWalletSynced {
			get {
				var walletSynced = this.ChainComponentProvider.WalletProviderBase.SyncedNoWait;

				return walletSynced.HasValue && walletSynced.Value;
			}

		}
		
		public bool IsChainSynchronizing { get; set; }

		public bool IsChainSynchronized => this.ChainComponentProvider.ChainStateProviderBase.IsChainSynced;

		public bool IsChainLikelySynchronized => this.ChainComponentProvider.ChainStateProviderBase.IsChainLikelySynchronized;

		public virtual void InitializeContents(IChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainComponentsInjection) {
			if(this.ChainId == BlockchainTypes.Instance.None) {
				throw new ApplicationException("The chain ID must be set and can not be 0");
			}

			this.BuildServices(chainComponentsInjection);

			if(this.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase == null) {
				throw new ApplicationException("workflow factory must be set and can not be null");
			}

			// make sure we initialize our wallet provider
			this.ChainComponentProvider.WalletProviderBase.Initialize();

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
		/// <param name="messageSet"></param>
		/// <param name="data"></param>
		/// <param name="connection"></param>
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
			
			((IGossipManager)this.services[Enums.GOSSIP_SERVICE].service).receiveGossipMessage(gossipMessageSet, connection);
		}

		public BlockchainServiceSet BlockchainServiceSet { get; }

		public bool IsShuttingDown { get; private set; }
		public event Action<ConcurrentBag<Task>> ShutdownRequested;
		public event Action ShutdownStarting;

		protected virtual void HandleMessages(IColoredTask task) {
			if(task is MessageReceivedTask messageTask) {
				this.HandleMesageReceived(messageTask);
			} 
		}

		/// <summary>
		///     a network message was received for our chain, let's check it and either trigger a new workflow, or route it to its
		///     owner
		/// </summary>
		/// <param name="messageTask"></param>
		/// <exception cref="ApplicationException"></exception>
		protected virtual void HandleMesageReceived(MessageReceivedTask messageTask) {

			try {
				if(messageTask.header.ChainId != this.ChainId) {
					throw new ApplicationException("a message was forwarded for a chain ID we do not support.");
				}

				if(messageTask.header is GossipHeader) {
					throw new ApplicationException("Gossip messages can not be received through this facility.");
				}

				if(messageTask.header is TargettedHeader targettedHeader) {
					// this is a targeted header, its meant only for us

					IBlockchainTargettedMessageSet messageSet = (IBlockchainTargettedMessageSet) this.ChainComponentProvider.ChainFactoryProviderBase.MessageFactoryBase.RehydrateMessage(messageTask.data, targettedHeader, this.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

					var workflowTracker = new WorkflowTracker<IWorkflow<IBlockchainEventsRehydrationFactory>, IBlockchainEventsRehydrationFactory>(messageTask.Connection, messageSet.Header.WorkflowCorrelationId, messageSet.Header.WorkflowSessionId, messageSet.Header.OriginatorId, this.ChainComponentProvider.ChainNetworkingProviderBase.MyclientUuid, this.workflowCoordinator);

					if(messageSet.Header.IsWorkflowTrigger && messageSet is IBlockchainTriggerMessageSet triggerMessageSet) {
						// route the message
						if(triggerMessageSet.BaseMessage is IWorkflowTriggerMessage workflowTriggerMessage) {

							if(!workflowTracker.WorkflowExists()) {
								// create a new workflow
								var workflow = (ITargettedNetworkingWorkflow<IBlockchainEventsRehydrationFactory>) this.ChainComponentProvider.ChainFactoryProviderBase.ServerWorkflowFactoryBase.CreateResponseWorkflow(triggerMessageSet, messageTask.Connection);

								this.workflowCoordinator.AddWorkflow(workflow);
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

						// forward the message to the right correlated workflow
						// this method wlil ensure we get the right workflow id for our connection

						// now we verify if this message originator was us

						if(workflowTracker.GetActiveWorkflow() is ITargettedNetworkingWorkflow<IBlockchainEventsRehydrationFactory> workflow) {

							workflow.ReceiveNetworkMessage(messageSet);
						} else {
							Log.Verbose($"The message references a workflow correlation ID '{messageSet.Header.WorkflowCorrelationId}' and session Id '{messageSet.Header.WorkflowSessionId}' which does not exist");
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
			if(originatorId == this.ChainComponentProvider.ChainNetworkingProviderBase.MyclientUuid) {
				workflowId = new NetworkWorkflowId(this.ChainComponentProvider.ChainNetworkingProviderBase.MyclientUuid, correlationId, sessionId);
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

		protected virtual void StartWorkers(bool killExistingTasks = true) {
			try {

				if(!killExistingTasks && this.services.Values.Any(s => !s.service.IsCompleted)) {
					throw new ApplicationException("There are still some running tasks in the list. Cancel them first");
				}

				this.StopWorkers();

				foreach(var service in this.services) {
					if(service.Value.executionType == Enums.ServiceExecutionTypes.Threaded) {
						service.Value.service.Start();
					} else if(service.Value.executionType == Enums.ServiceExecutionTypes.Synchronous) {
						// we will be using a synchronous model. let's set it up
						service.Value.service.StartSync();
					} else if(service.Value.executionType == Enums.ServiceExecutionTypes.None) {
						// this service is deactivated. it does nothing

					}
				}
			} catch(Exception ex) {
				Log.Error(ex, "Failed to start services");

				throw ex;
			}
		}

		protected virtual void StopWorkers() {
			try {
				// if we had existing tasks, either break, or stop them all
				foreach(var service in this.services) {
					if(service.Value.executionType == Enums.ServiceExecutionTypes.Threaded) {
						service.Value.service.Stop();
					} else if(service.Value.executionType == Enums.ServiceExecutionTypes.Synchronous) {
						service.Value.service.StopSync();
					}
				}

				Task.WaitAll(this.services.Where(ts => (ts.Value.service.IsCompleted == false) && ts.Value.service.IsStarted && (ts.Value.executionType == Enums.ServiceExecutionTypes.Threaded)).Select(ts => ts.Value.service.Task).ToArray(), 1000 * 20);
			} catch(Exception ex) {
				Log.Error(ex, "Failed to stop controllers");

				throw ex;
			}
		}

		public void RequestShutdown() {
			this.Stop();
		}

		protected override void ProcessLoop() {

			this.CheckShouldCancel();
			
			this.ColoredRoutedTaskReceiver.CheckTasks();
		}

		protected override void DisposeAll() {

			this.Stop();

			// and any providers
			foreach(var provider in this.ChainComponentProvider.Providers) {
				try {
					if(provider is IDisposable disposable) {
						disposable.Dispose();
					}
				} catch {
					
				}
			}

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

		public void PostSystemEvent(SystemMessageTask messageTask, CorrelationContext? correlationContext = null) {
			// for now, only the interface is interrested in system messages
			this.chainInterface.ReceiveChainMessageTask(messageTask);
		}

		/// <summary>
		///     Post a system event
		/// </summary>
		/// <param name="eventType"></param>
		public void PostSystemEvent(BlockchainSystemEventType eventType, CorrelationContext? correlationContext = null) {
			this.PostSystemEvent(eventType, null, correlationContext);
		}

		public void PostSystemEvent(BlockchainSystemEventType eventType, object[] parameters, CorrelationContext? correlationContext = null) {
			//TODO refactor the post system events system
			SystemMessageTask systemMessage = new SystemMessageTask(eventType, parameters, correlationContext);

			this.PostSystemEvent(systemMessage);
		}

		public void PostSystemEvent(SystemEventGenerator generator, CorrelationContext? correlationContext = null) {
			if(generator == null) {
				return;
			}

			//TODO refactor the post system events system
			SystemMessageTask systemMessage = new SystemMessageTask(generator.EventType, generator.Parameters, correlationContext);

			this.PostSystemEvent(systemMessage);
		}

		public void PostSystemEventImmediate(SystemMessageTask messageTask, CorrelationContext? correlationContext = null) {
			// for now, only the interface is interrested in system messages
			this.chainInterface.ReceiveChainMessageTaskImmediate(messageTask);
		}

		/// <summary>
		///     Post a system event
		/// </summary>
		/// <param name="eventType"></param>
		public void PostSystemEventImmediate(BlockchainSystemEventType eventType, CorrelationContext? correlationContext = null) {
			this.PostSystemEventImmediate(eventType, null, correlationContext);
		}

		public void PostSystemEventImmediate(BlockchainSystemEventType eventType, object[] parameters, CorrelationContext? correlationContext = null) {
			//TODO refactor the post system events system
			SystemMessageTask systemMessage = new SystemMessageTask(eventType, parameters, correlationContext);

			this.PostSystemEventImmediate(systemMessage);
		}

		public void PostSystemEventImmediate(SystemEventGenerator generator, CorrelationContext? correlationContext = null) {
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