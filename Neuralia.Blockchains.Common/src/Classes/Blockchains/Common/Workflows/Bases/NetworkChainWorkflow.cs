using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;

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

			if(GlobalSettings.ApplicationSettings.P2pEnabled) {
				this.dataDispatcher = new DataDispatcher(centralCoordinator.BlockchainServiceSet.TimeService);
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

		public void StashTask(InternalRoutedTask task) {
			this.RoutedTaskReceiver.StashTask(task);
		}

		public void RestoreStashedTask(InternalRoutedTask task) {
			this.RoutedTaskReceiver.RestoreStashedTask(task);
		}

		public bool CheckSingleTask(Guid taskId) {
			return this.RoutedTaskReceiver.CheckSingleTask(taskId);
		}

		public void Wait() {
			this.RoutedTaskReceiver.Wait();
		}

		public void Wait(TimeSpan timeout) {
			this.RoutedTaskReceiver.Wait(timeout);
		}

		public void DispatchSelfTask(IRoutedTask task) {
			this.RoutedTaskReceiver.DispatchSelfTask(task);
		}

		public void DispatchTaskAsync(IRoutedTask task) {
			this.RoutedTaskReceiver.DispatchTaskAsync(task);
		}

		public void DispatchTaskNoReturnAsync(IRoutedTask task) {
			this.RoutedTaskReceiver.DispatchTaskNoReturnAsync(task);
		}

		public bool DispatchTaskSync(IRoutedTask task) {
			return this.RoutedTaskReceiver.DispatchTaskSync(task);
		}

		public bool DispatchTaskNoReturnSync(IRoutedTask task) {
			return this.RoutedTaskReceiver.DispatchTaskNoReturnSync(task);
		}

		public bool WaitSingleTask(IRoutedTask task) {
			return this.RoutedTaskReceiver.WaitSingleTask(task);
		}

		public bool WaitSingleTask(IRoutedTask task, TimeSpan timeout) {
			return this.RoutedTaskReceiver.WaitSingleTask(task, timeout);
		}

		protected bool SendMessage(PeerConnection peerConnection, INetworkMessageSet message) {
			return this.dataDispatcher?.SendMessage(peerConnection, message) ?? false;
		}

		protected bool SendFinalMessage(PeerConnection peerConnection, INetworkMessageSet message) {
			return this.dataDispatcher?.SendFinalMessage(peerConnection, message) ?? false;
		}

		protected bool SendBytes(PeerConnection peerConnection, SafeArrayHandle data) {
			return this.dataDispatcher?.SendBytes(peerConnection, data) ?? false;
		}

		protected bool SendFinalBytes(PeerConnection peerConnection, SafeArrayHandle data) {
			return this.dataDispatcher?.SendFinalBytes(peerConnection, data) ?? false;
		}
	}
}