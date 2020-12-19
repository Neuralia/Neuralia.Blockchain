using System;

namespace Neuralia.Blockchains.Core.Workflows.Tasks {
	public class TaskErrorInfo {
		/// <summary>
		///     None: default behavior, nothing
		///     Handled : the exception as handled; as if nothing happened. done.
		///     Rethrow:  the exception will be rethrown in the calling dispatcher as a .NET exception to catch (does not
		///     propagate)
		///     Propagate: the exception will be propagated back to the parent to handle there.
		/// </summary>
		public enum ErrorHandlingMode {
			None,
			Handled,
			Rethrow,
			Propagate
		}

		public Exception Exception { get; set; }

		public ErrorHandlingMode Handling { get; set; } = ErrorHandlingMode.None;
	}
}