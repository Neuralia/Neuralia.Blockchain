using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases {
	public interface INetworkChainWorkflow : ITargettedNetworkingWorkflow<IBlockchainEventsRehydrationFactory>, IChainWorkflow {
	}

	public interface INetworkChainWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : INetworkChainWorkflow, IChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class NetworkChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : TargettedNetworkingWorkflow<IBlockchainEventsRehydrationFactory>, INetworkChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		private readonly DataDispatcher dataDispatcher;

		protected readonly DelegatingRoutedTaskRoutingReceiver RoutedTaskReceiver;

		public NetworkChainWorkflow(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator.BlockchainServiceSet) {
			this.centralCoordinator = centralCoordinator;

			this.RoutedTaskReceiver = new DelegatingRoutedTaskRoutingReceiver(this, this.centralCoordinator, true, 1, RoutedTaskRoutingReceiver.RouteMode.ReceiverOnly);

			this.RoutedTaskReceiver.TaskReceived += () => {
				// wake up, a task has been received
				this.Awaken();
			};

			if(GlobalSettings.ApplicationSettings.P2PEnabled) {
				this.dataDispatcher = new DataDispatcher(centralCoordinator.BlockchainServiceSet.TimeService, faultyConnection => {
					// just in case, attempt to remove the connection if it was not already
					NLog.Connections.Verbose($"[{nameof(NetworkingWorkflow)} removing faulty connection {faultyConnection.NodeAddressInfo}.");
					this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RemoveConnection(faultyConnection);
				});
			} else {
				// no network
				this.dataDispatcher = null;
			}
		}

		public CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;

		public void ReceiveTask(IRoutedTask task) {
			this.RoutedTaskReceiver.ReceiveTask(task);
		}

		public void ReceiveTaskSynchronous(IRoutedTask task) {
			this.RoutedTaskReceiver.ReceiveTaskSynchronous(task);
		}

		public bool Synchronous {
			get => this.RoutedTaskReceiver.Synchronous;
			set => this.RoutedTaskReceiver.Synchronous = value;
		}

		public bool StashingEnabled => this.RoutedTaskReceiver.StashingEnabled;

		public ITaskRouter TaskRouter => this.RoutedTaskReceiver.TaskRouter;

		public Task StashTask(InternalRoutedTask task) {
			return this.RoutedTaskReceiver.StashTask(task);
		}

		public Task RestoreStashedTask(InternalRoutedTask task) {
			return this.RoutedTaskReceiver.RestoreStashedTask(task);
		}

		public Task<bool> CheckSingleTask(Guid taskId) {
			return this.RoutedTaskReceiver.CheckSingleTask(taskId);
		}

		public Task Wait() {
			return this.RoutedTaskReceiver.Wait();
		}

		public Task Wait(TimeSpan timeout) {
			return this.RoutedTaskReceiver.Wait(timeout);
		}

		public Task DispatchSelfTask(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchSelfTask(task, lockContext);
		}

		public Task DispatchTaskAsync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchTaskAsync(task, lockContext);
		}

		public Task DispatchTaskNoReturnAsync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchTaskNoReturnAsync(task, lockContext);
		}

		public Task<bool> DispatchTaskSync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchTaskSync(task, lockContext);
		}

		public Task<bool> DispatchTaskNoReturnSync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskReceiver.DispatchTaskNoReturnSync(task, lockContext);
		}

		public Task<bool> WaitSingleTask(IRoutedTask task) {
			return this.RoutedTaskReceiver.WaitSingleTask(task);
		}

		public Task<bool> WaitSingleTask(IRoutedTask task, TimeSpan timeout) {
			return this.RoutedTaskReceiver.WaitSingleTask(task, timeout);
		}

		protected Task<bool> SendMessage(PeerConnection peerConnection, INetworkMessageSet message) {
			if(this.dataDispatcher == null) {
				return Task.FromResult(false);
			}
			LockContext lockContext = null;
			return this.dataDispatcher.SendMessage(peerConnection, message, lockContext) ;
		}

		protected Task<bool> SendFinalMessage(PeerConnection peerConnection, INetworkMessageSet message) {
			if(this.dataDispatcher == null) {
				return Task.FromResult(false);
			}
			LockContext lockContext = null;
			return this.dataDispatcher.SendFinalMessage(peerConnection, message, lockContext) ;
		}

		protected Task<bool> SendBytes(PeerConnection peerConnection, SafeArrayHandle data) {
			if(this.dataDispatcher == null) {
				return Task.FromResult(false);
			}
			LockContext lockContext = null;
			return this.dataDispatcher.SendBytes(peerConnection, data, lockContext) ;
		}

		protected Task<bool> SendFinalBytes(PeerConnection peerConnection, SafeArrayHandle data) {
			if(this.dataDispatcher == null) {
				return Task.FromResult(false);
			}
			LockContext lockContext = null;
			return this.dataDispatcher.SendFinalBytes(peerConnection, data, lockContext) ;
		}
	}
}