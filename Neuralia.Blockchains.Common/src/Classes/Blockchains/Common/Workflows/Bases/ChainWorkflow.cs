using System;
using System.Reflection;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;

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

		protected override sealed void PerformWork() {
			// here we delegate all the work to a task, so we can benefit from all it's strengths including stashing
			if(!this.performWorkOverriden.HasValue) {
				this.performWorkOverriden = this.IsOverride(nameof(PerformWork), new[] {typeof(IChainWorkflow), typeof(TaskRoutingContext)});
			}

			if(this.performWorkOverriden.Value) {
				var task = new RoutedTask<IChainWorkflow, bool>();

				task.SetAction(this.PerformWork);

				this.DispatchSelfTask(task);
			}
		}

		protected override sealed void Initialize() {
			base.Initialize();

			if(this.IsOverride(nameof(Initialize), new[] {typeof(IChainWorkflow), typeof(TaskRoutingContext)})) {
				var task = new RoutedTask<IChainWorkflow, bool>();

				task.SetAction(this.Initialize);

				this.DispatchSelfTask(task);
			}
		}

		protected override sealed void Terminate(bool clean) {
			base.Terminate(clean);

			if(this.IsOverride(nameof(Terminate), new[] {typeof(bool), typeof(IChainWorkflow), typeof(TaskRoutingContext)})) {
				var task = new RoutedTask<IChainWorkflow, bool>();

				task.SetAction((workflow, taskRoutingContext) => this.Terminate(clean, workflow, taskRoutingContext));

				this.DispatchSelfTask(task);
			}
		}

		protected virtual void Initialize(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {

		}

		protected virtual void Terminate(bool clean, IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {

		}

		protected virtual void PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {

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