using System;

namespace Neuralia.Blockchains.Core.Workflows.Tasks {
	public interface IBasicTask : IDelegatedTask {
	}

	public interface IBasicTaskIn<in T> : IDelegatedTaskIn<T>, IBasicTask {
		Action<T> Action { get; }

		void TriggerAction(T sender);
	}

	public interface IBasicTaskOut<out T> : IDelegatedTaskOut<T>, IBasicTask {
		void SetAction(Action<T> action);
	}

	public interface IBasicTask<T> : IBasicTaskIn<T>, IBasicTaskOut<T>, IDelegatedTask<T> {
	}

	/// <summary>
	///     a simple task meant to be executed on another thread
	/// </summary>
	public class BasicTask<T> : DelegatedTask<T>, IBasicTask<T> {

		public void SetAction(Action<T> action) {
			this.Action = action;
		}

		/// <summary>
		///     This method is called in the context of the destination thread.null
		///     Returning a new task will chain it to this one, and once the new task(s) completes, then this task will return to
		///     the caller and complete
		/// </summary>
		public virtual Action<T> Action { get; set; }

		public virtual void TriggerAction(T sender) {
if(			this.Action != null){			this.Action(sender);}
		}
	}
}