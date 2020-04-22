using System;
using System.Reflection;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Tools {
	public interface IRoutedTaskRoutingThread : IRoutedTaskRoutingHandler, ILoopThread {
	}

	public interface IRoutedTaskRoutingThread<out T, out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ILoopThread<T>, IRoutedTaskRoutingThread
		where T : IRoutedTaskRoutingThread<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class RoutedTaskRoutingThread<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : LoopThread<T>, IRoutedTaskRoutingThread<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where T : class, IRoutedTaskRoutingThread<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public RoutedTaskRoutingThread(CENTRAL_COORDINATOR centralCoordinator, int maxParallelTasks, int sleepTime = 100) : base(sleepTime) {
			this.RoutedTaskRoutingReceiver = new SpecializedRoutedTaskRoutingReceiver<T>(centralCoordinator, this as T, true, maxParallelTasks);
			this.CentralCoordinator = centralCoordinator;
			
			//TODO: for production, give it 30 seconds
			this.hibernateTimeoutSpan = TimeSpan.FromSeconds(30 * 60);
		}

		protected ISpecializedRoutedTaskRoutingReceiver RoutedTaskRoutingReceiver { get; set; }

		protected CENTRAL_COORDINATOR CentralCoordinator { get; }

		public bool Synchronous {
			get => this.RoutedTaskRoutingReceiver.Synchronous;
			set => this.RoutedTaskRoutingReceiver.Synchronous = value;
		}

		public bool StashingEnabled => this.RoutedTaskRoutingReceiver.StashingEnabled;

		public virtual void ReceiveTask(IRoutedTask task) {
			try {
				this.RoutedTaskRoutingReceiver.ReceiveTask(task);
			} catch(Exception ex) {
				Log.Error(ex, "Failed to post task");
			}

			// now lets wakeup our thread and continue
			this.Awaken();
		}

		public void ReceiveTaskSynchronous(IRoutedTask task) {
			this.RoutedTaskRoutingReceiver.ReceiveTaskSynchronous(task);

			this.Awaken();
		}

		public ITaskRouter TaskRouter => this.RoutedTaskRoutingReceiver.TaskRouter;

		public Task StashTask(InternalRoutedTask task) {
			return this.RoutedTaskRoutingReceiver.StashTask(task);
		}

		public Task RestoreStashedTask(InternalRoutedTask task) {
			return this.RoutedTaskRoutingReceiver.RestoreStashedTask(task);
		}

		public Task<bool> CheckSingleTask(Guid taskId) {
			return this.RoutedTaskRoutingReceiver.CheckSingleTask(taskId);
		}

		public Task Wait() {
			return this.RoutedTaskRoutingReceiver.Wait();
		}

		public Task Wait(TimeSpan timeout) {
			return this.RoutedTaskRoutingReceiver.Wait(timeout);
		}

		public Task DispatchSelfTask(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskRoutingReceiver.DispatchSelfTask(task, lockContext);
		}

		public Task DispatchTaskAsync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskRoutingReceiver.DispatchTaskAsync(task, lockContext);
		}

		public Task DispatchTaskNoReturnAsync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskRoutingReceiver.DispatchTaskNoReturnAsync(task, lockContext);
		}

		public Task<bool> DispatchTaskSync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskRoutingReceiver.DispatchTaskSync(task, lockContext);
		}

		public Task<bool> DispatchTaskNoReturnSync(IRoutedTask task, LockContext lockContext) {
			return this.RoutedTaskRoutingReceiver.DispatchTaskNoReturnSync(task, lockContext);
		}

		public Task<bool> WaitSingleTask(IRoutedTask task) {
			return this.RoutedTaskRoutingReceiver.WaitSingleTask(task);
		}

		public Task<bool> WaitSingleTask(IRoutedTask task, TimeSpan timeout) {
			return this.RoutedTaskRoutingReceiver.WaitSingleTask(task, timeout);
		}

		public override async Task Stop() {
			await base.Stop().ConfigureAwait(false);
			this.Awaken(); // just in case we were sleeping
		}

		protected override sealed async Task Initialize(LockContext lockContext) {
			await base.Initialize(lockContext).ConfigureAwait(false);

			if(this.IsOverride(nameof(Initialize), new[] {typeof(T), typeof(TaskRoutingContext), typeof(LockContext)})) {
				var task = new RoutedTask<T, bool>();

				task.SetAction((workflow, taskRoutingContext, lc) => this.Initialize(workflow, taskRoutingContext, lc));

				await this.DispatchSelfTask(task, lockContext).ConfigureAwait(false);
			}
		}

		protected override sealed async Task Terminate(bool clean, LockContext lockContext) {
			await base.Terminate(clean, lockContext).ConfigureAwait(false);

			if(this.IsOverride(nameof(Terminate), new[] {typeof(bool), typeof(T), typeof(TaskRoutingContext), typeof(LockContext)})) {
				var task = new RoutedTask<T, bool>();

				task.SetAction((workflow, taskRoutingContext, lc) => this.Terminate(clean, workflow, taskRoutingContext));

				await this.DispatchSelfTask(task, lockContext).ConfigureAwait(false);
			}
		}

		protected override sealed async Task ProcessLoop(LockContext lockContext) {

			try {
				if(this.IsOverride(nameof(ProcessLoop), new[] {typeof(T), typeof(TaskRoutingContext), typeof(LockContext)})) {
					var task = new RoutedTask<T, bool>();

					task.SetAction(this.ProcessLoop);

					await this.DispatchSelfTask(task, lockContext).ConfigureAwait(false);
				} else {
					await this.RoutedTaskRoutingReceiver.CheckTasks(async () => this.CheckShouldCancel()).ConfigureAwait(false);
				}
			} catch(Exception ex) {
				Log.Error(ex, "Failed to process task loop");
			}
		}

		protected virtual Task Initialize(T workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
			return Task.CompletedTask;
		}

		protected virtual Task Terminate(bool clean, T workflow, TaskRoutingContext taskRoutingContext) {

			return Task.CompletedTask;
		}

		protected virtual Task ProcessLoop(T workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
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

		public static bool IsOverride(MethodInfo m) {
			return m.GetBaseDefinition().DeclaringType != m.DeclaringType;
		}

		public void PostChainEvent(SystemMessageTask messageTask, CorrelationContext correlationContext = default) {
			this.CentralCoordinator.PostSystemEvent(messageTask, correlationContext);
		}

		public void PostChainEvent(BlockchainSystemEventType message, CorrelationContext correlationContext = default) {
			this.CentralCoordinator.PostSystemEvent(message, correlationContext);
		}
	}
}