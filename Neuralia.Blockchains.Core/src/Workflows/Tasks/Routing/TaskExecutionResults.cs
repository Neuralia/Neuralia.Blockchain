using System;
using System.Runtime.ExceptionServices;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Routing {
	public class TaskExecutionResults {

		public enum ExceptionHandlingModes {
			Rethrow,
			Handled
		}

		public ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }

		public Exception Exception {
			get => this.ExceptionDispatchInfo?.SourceException;
			set => this.ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(value);
		}

		public bool Success => this.Exception == null;
		public bool Error => !this.Success;

		public ExceptionHandlingModes HandlingMode { get; set; } = ExceptionHandlingModes.Rethrow;

		public void Reset() {
			this.ExceptionDispatchInfo = null;
			this.Rethrow();
		}

		public void Handled() {
			this.HandlingMode = ExceptionHandlingModes.Handled;
		}

		public void Rethrow() {
			this.HandlingMode = ExceptionHandlingModes.Rethrow;
		}

		public void Wrap<E>(string message = null)
			where E : Exception, new() {
			this.Rethrow();

			if(this.Exception == null) {
				if(string.IsNullOrWhiteSpace(message)) {
					this.Exception = new E();
				} else {
					this.Exception = (E) Activator.CreateInstance(typeof(E), message);
				}
			} else {
				if(string.IsNullOrWhiteSpace(message)) {
					this.Exception = (E) Activator.CreateInstance(typeof(E), this.Exception);
				} else {
					this.Exception = (E) Activator.CreateInstance(typeof(E), message, this.Exception);
				}

			}
		}

		public void Copy(TaskExecutionResults other) {
			this.Exception = other.Exception;
			this.HandlingMode = other.HandlingMode;
		}
	}
}