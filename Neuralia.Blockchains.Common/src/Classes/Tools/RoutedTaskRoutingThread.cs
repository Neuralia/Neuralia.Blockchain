using System;
using System.Reflection;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Receivers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
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

		public RoutedTaskRoutingThread(CENTRAL_COORDINATOR centralCoordinator, int maxParallelTasks) {
			this.RoutedTaskRoutingReceiver = new SpecializedRoutedTaskRoutingReceiver<T>(centralCoordinator, this as T, true, maxParallelTasks);
			this.CentralCoordinator = centralCoordinator;

			//TODO: for production, give it 30 seconds
			this.hibernateTimeoutSpan = TimeSpan.FromSeconds(30 * 60);
		}

		public RoutedTaskRoutingThread(CENTRAL_COORDINATOR router, int maxParallelTasks, int sleepTime) : base(sleepTime) {
			this.RoutedTaskRoutingReceiver = new SpecializedRoutedTaskRoutingReceiver<T>(router, this as T, true, maxParallelTasks);

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

		public void StashTask(InternalRoutedTask task) {
			this.RoutedTaskRoutingReceiver.StashTask(task);
		}

		public void RestoreStashedTask(InternalRoutedTask task) {
			this.RoutedTaskRoutingReceiver.RestoreStashedTask(task);
		}

		public bool CheckSingleTask(Guid taskId) {
			return this.RoutedTaskRoutingReceiver.CheckSingleTask(taskId);
		}

		public void Wait() {
			this.RoutedTaskRoutingReceiver.Wait();
		}

		public void Wait(TimeSpan timeout) {
			this.RoutedTaskRoutingReceiver.Wait(timeout);
		}

		public void DispatchSelfTask(IRoutedTask task) {
			this.RoutedTaskRoutingReceiver.DispatchSelfTask(task);
		}

		public void DispatchTaskAsync(IRoutedTask task) {
			this.RoutedTaskRoutingReceiver.DispatchTaskAsync(task);
		}

		public void DispatchTaskNoReturnAsync(IRoutedTask task) {
			this.RoutedTaskRoutingReceiver.DispatchTaskNoReturnAsync(task);
		}

		public bool DispatchTaskSync(IRoutedTask task) {
			return this.RoutedTaskRoutingReceiver.DispatchTaskSync(task);
		}

		public bool DispatchTaskNoReturnSync(IRoutedTask task) {
			return this.RoutedTaskRoutingReceiver.DispatchTaskNoReturnSync(task);
		}

		public bool WaitSingleTask(IRoutedTask task) {
			return this.RoutedTaskRoutingReceiver.WaitSingleTask(task);
		}

		public bool WaitSingleTask(IRoutedTask task, TimeSpan timeout) {
			return this.RoutedTaskRoutingReceiver.WaitSingleTask(task, timeout);
		}

		public override void Stop() {
			base.Stop();
			this.Awaken(); // just in case we were sleeping
		}

		protected override sealed void Initialize() {
			base.Initialize();

			if(this.IsOverride(nameof(Initialize), new[] {typeof(T), typeof(TaskRoutingContext)})) {
				var task = new RoutedTask<T, bool>();

				task.SetAction(this.Initialize);

				this.DispatchSelfTask(task);
			}
		}

		protected override sealed void Terminate(bool clean) {
			base.Terminate(clean);

			if(this.IsOverride(nameof(Terminate), new[] {typeof(bool), typeof(T), typeof(TaskRoutingContext)})) {
				var task = new RoutedTask<T, bool>();

				task.SetAction((workflow, taskRoutingContext) => this.Terminate(clean, workflow, taskRoutingContext));

				this.DispatchSelfTask(task);
			}
		}

		protected override sealed void ProcessLoop() {

			try {
				if(this.IsOverride(nameof(ProcessLoop), new[] {typeof(T), typeof(TaskRoutingContext)})) {
					var task = new RoutedTask<T, bool>();

					task.SetAction(this.ProcessLoop);

					this.DispatchSelfTask(task);
				} else {
					this.RoutedTaskRoutingReceiver.CheckTasks(this.CheckShouldCancel);
				}
			} catch(Exception ex) {
				Log.Error(ex, "Failed to process task loop");
			}
		}

		protected virtual void Initialize(T workflow, TaskRoutingContext taskRoutingContext) {

		}

		protected virtual void Terminate(bool clean, T workflow, TaskRoutingContext taskRoutingContext) {

		}

		protected virtual void ProcessLoop(T workflow, TaskRoutingContext taskRoutingContext) {

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