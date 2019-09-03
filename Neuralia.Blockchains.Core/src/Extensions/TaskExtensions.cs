using System;
using System.Threading.Tasks;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class TaskExtensions {
		public static Task<T> WithAllExceptions<T>(this Task<T> task) {
			var tcs = new TaskCompletionSource<T>();

			task.ContinueWith(ignored => {
				switch(task.Status) {
					case TaskStatus.Canceled:
						tcs.SetCanceled();

						break;

					case TaskStatus.RanToCompletion:
						tcs.SetResult(task.Result);

						break;

					case TaskStatus.Faulted:

						// SetException will automatically wrap the original AggregateException
						// in another one. The new wrapper will be removed in TaskAwaiter, leaving
						// the original intact.
						tcs.SetException(task.Exception);

						break;

					default:
						tcs.SetException(new InvalidOperationException("Continuation called illegally."));

						break;
				}
			});

			return tcs.Task;
		}
	}
}