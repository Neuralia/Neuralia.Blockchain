using System;
using System.Reflection;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases {

	public interface IChainWorkflow : IWorkflow<IBlockchainEventsRehydrationFactory>, IRoutedTaskRoutingHandler {
	}

	public interface IChainWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainWorkflow
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class ChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : Workflow<IBlockchainEventsRehydrationFactory>, IChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		private readonly DataDispatcher dataDispatcher;

		protected readonly DelegatingRoutedTaskRoutingReceiver RoutedTaskReceiver;

		private bool? performWorkOverriden;

		public ChainWorkflow(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator.BlockchainServiceSet) {
			this.centralCoordinator = centralCoordinator;

			this.RoutedTaskReceiver = new DelegatingRoutedTaskRoutingReceiver(this, this.centralCoordinator, true, 1, RoutedTaskRoutingReceiver.RouteMode.ReceiverOnly);

			this.RoutedTaskReceiver.TaskReceived += this.Awaken;
		}

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

		protected override sealed async Task PerformWork(LockContext lockContext) {
			// here we delegate all the work to a task, so we can benefit from all it's strengths including stashing
			if(!this.performWorkOverriden.HasValue) {
				this.performWorkOverriden = this.IsOverride(nameof(PerformWork), new[] {typeof(IChainWorkflow), typeof(TaskRoutingContext), typeof(LockContext)});
			}

			if(this.performWorkOverriden.Value) {
				var task = new RoutedTask<IChainWorkflow, bool>();

				task.SetAction((workflow, taskRoutingContext, lc) => this.PerformWork(workflow, taskRoutingContext, lc));

				await this.DispatchSelfTask(task, lockContext).ConfigureAwait(false);
			}

		}

		protected override sealed async Task Initialize(LockContext lockContext) {
			await base.Initialize(lockContext).ConfigureAwait(false);

			if(this.IsOverride(nameof(Initialize), new[] {typeof(IChainWorkflow), typeof(TaskRoutingContext), typeof(LockContext)})) {
				var task = new RoutedTask<IChainWorkflow, bool>();

				task.SetAction((workflow, taskRoutingContext, lc) => this.Initialize(workflow, taskRoutingContext, lc));

				await this.DispatchSelfTask(task, lockContext).ConfigureAwait(false);
			}
		}

		protected override sealed async Task Terminate(bool clean, LockContext lockContext) {
			await base.Terminate(clean, lockContext).ConfigureAwait(false);

			if(this.IsOverride(nameof(Terminate), new[] {typeof(bool), typeof(IChainWorkflow), typeof(TaskRoutingContext), typeof(LockContext)})) {
				var task = new RoutedTask<IChainWorkflow, bool>();

				task.SetAction((workflow, taskRoutingContext, lc) => this.Terminate(clean, workflow, taskRoutingContext));

				await this.DispatchSelfTask(task, lockContext).ConfigureAwait(false);
			}
		}

		protected virtual Task Initialize(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
			return Task.CompletedTask;
		}

		protected virtual Task Terminate(bool clean, IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {
			return Task.CompletedTask;
		}

		protected virtual Task PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
			return Task.CompletedTask;
		}

		/// <summary>
		///     determine if we have an override
		/// </summary>
		/// <param name="name"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public bool IsOverride(string name, Type[] parameters) {
			MethodInfo methodInfo = this.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, Type.DefaultBinder, parameters, null);

			if(methodInfo != null) {
				return methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;
			}

			return false;
		}
	}
}