using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neuralia.Blockchains.Core.Tools {
	/// <summary>
	///     A utility method to repeat an action until success or count expire
	/// </summary>
	public static class Repeater {
		
		public static R Repeat<R>(Func<R> action, int tries = 3, Action afterFailed = null) {
			return Repeat(index => action(), tries, afterFailed);
		}

		public static bool Repeat(Action action, int tries = 3, Action afterFailed = null) {
			return Repeat(index => action(), tries, afterFailed);
		}

		public static bool Repeat(Action<int> action, int tries = 3, Action afterFailed = null) {
			int count = 1;

			int time = 10;

			while(count <= tries) {

				try {

					action(count);

					//all good, we are done
					return true;
				} catch(Exception ex) {

					if(count == tries) {
						throw;
					}

					if(afterFailed != null) {
						afterFailed();
					}
				}

				// this inside a lock is not great, but we want stability so we will just wait...
				Thread.Sleep(time);
				time += 100;
				count++;
			}

			return false;
		}
		

		public static R Repeat<R>(Func<int, R> action, int tries = 3, Action afterFailed = null) {
			int count = 1;

			int time = 10;

			while(count <= tries) {

				try {

					return action(count);

				} catch(Exception ex) {

					if(count == tries) {
						throw;
					}

					if(afterFailed != null) {
						afterFailed();
					}
				}

				// this inside a lock is not great, but we want stability so we will just wait...
				Thread.Sleep(time);
				time += 100;
				count++;
			}

			throw new ApplicationException($"Falied to retry {tries} times.");
		}

		public static Task<R> RepeatAsync<R>(Func<Task<R>> action, int tries = 3, Func<Task> afterFailed = null) {
			return RepeatAsync(index => action(), tries, afterFailed);
		}

		public static Task<bool> RepeatAsync(Func<Task> action, int tries = 3, Func<Task> afterFailed = null) {
			return RepeatAsync(index => action(), tries, afterFailed);
		}
		
		public static async Task<bool> RepeatAsync<T>( T parameter, Func<T, int, Task> action, int tries = 3, Func<Task> afterFailed = null) {
			int count = 1;

			int time = 10;

			while(count <= tries) {

				try {

					await action(parameter, count).ConfigureAwait(false);

					return true;

				} catch(Exception ex) {

					if(count == tries) {
						throw;
					}

					if(afterFailed != null) {
						await afterFailed().ConfigureAwait(false);
					}
				}

				// this inside a lock is not great, but we want stability so we will just wait...
				Thread.Sleep(time);
				time += 100;
				count++;
			}

			return false;
		}

		public static async Task<bool> RepeatAsync(Func<int, Task> action, int tries = 3, Func<Task> afterFailed = null) {
			int count = 1;

			int time = 10;

			while(count <= tries) {

				try {

					await action(count).ConfigureAwait(false);

					return true;

				} catch(Exception ex) {

					if(count == tries) {
						throw;
					}

					if(afterFailed != null) {
						await afterFailed().ConfigureAwait(false);
					}
				}

				// this inside a lock is not great, but we want stability so we will just wait...
				Thread.Sleep(time);
				time += 100;
				count++;
			}

			return false;
		}

		public static async Task<R> RepeatAsync<R>(Func<int, Task<R>> action, int tries = 3, Func<Task> afterFailed = null) {
			int count = 1;

			int time = 10;

			while(count <= tries) {

				try {

					return await action(count).ConfigureAwait(false);

				} catch(Exception ex) {

					if(count == tries) {
						throw;
					}

					if(afterFailed != null) {
						await afterFailed().ConfigureAwait(false);
					}
				}

				// this inside a lock is not great, but we want stability so we will just wait...
				Thread.Sleep(time);
				time += 100;
				count++;
			}

			throw new ApplicationException($"Failed to retry {tries} times.");
		}
		
		/// <summary>
		/// a version without exceptions
		/// </summary>
		/// <param name="action"></param>
		/// <param name="tries"></param>
		/// <param name="afterFailed"></param>
		/// <typeparam name="R"></typeparam>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public static async Task<(R result, bool success)> RepeatAsync2<R>(Func<int, Task<(R result, bool success)>> action, int tries = 3, Func<Task> afterFailed = null) {
			int count = 1;

			int time = 10;

			while(count <= tries) {
				
				var result = await action(count).ConfigureAwait(false);

				if(result.success) {
					return result;
				}

				// this inside a lock is not great, but we want stability so we will just wait...
				Thread.Sleep(time);
				time += 100;
				count++;
				
				if(count == tries) {
					return default;
				}
				
				if(afterFailed != null) {
					await afterFailed().ConfigureAwait(false);
				}
			}

			throw new ApplicationException($"Failed to retry {tries} times.");
		}
	}
}