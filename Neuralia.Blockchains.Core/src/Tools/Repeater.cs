using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neuralia.Blockchains.Core.Tools {
	/// <summary>
	///     A utility method to repeat an action until success or count expire
	/// </summary>
	public static class Repeater {

		public static Task<R> RepeatAsync<R>(Func<Task<R>> action, int tries = 3, Action afterFailed = null) {
			return RepeatAsync<R>(index => action(), tries, afterFailed);
		}
		
		public static Task RepeatAsync(Func<Task> action, int tries = 3, Action afterFailed = null) {
			return RepeatAsync(index => action(), tries, afterFailed);
		}
		
		public static R Repeat<R>(Func<R> action, int tries = 3, Action afterFailed = null) {
			return Repeat<R>(index => action(), tries, afterFailed);
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

					afterFailed?.Invoke();
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

					afterFailed?.Invoke();
				}

				// this inside a lock is not great, but we want stability so we will just wait...
				Thread.Sleep(time);
				time += 100;
				count++;
			}
			throw new ApplicationException($"Falied to retry {tries} times.");
		}
		
		public static async Task RepeatAsync(Func<int, Task> action, int tries = 3, Action afterFailed = null) {
			int count = 1;

			int time = 10;
			while(count <= tries) {

				try {

					await action(count);

					return;

				} catch(Exception ex) {

					if(count == tries) {
						throw;
					}

					afterFailed?.Invoke();
				}

				// this inside a lock is not great, but we want stability so we will just wait...
				Thread.Sleep(time);
				time += 100;
				count++;
			}
			throw new ApplicationException($"Falied to retry {tries} times.");
		}
		
		public static async Task<R> RepeatAsync<R>(Func<int, Task<R>> action, int tries = 3, Action afterFailed = null) {
			int count = 1;

			int time = 10;
			while(count <= tries) {

				try {

					return await action(count);
					
				} catch(Exception ex) {

					if(count == tries) {
						throw;
					}

					afterFailed?.Invoke();
				}

				// this inside a lock is not great, but we want stability so we will just wait...
				Thread.Sleep(time);
				time += 100;
				count++;
			}
			throw new ApplicationException($"Falied to retry {tries} times.");
		}
	}
}