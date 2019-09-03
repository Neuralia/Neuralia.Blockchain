using System;
using System.Threading;

namespace Neuralia.Blockchains.Core.Tools {
	/// <summary>
	///     A utility method to repeat an action until success or count expire
	/// </summary>
	public static class Repeater {

		public static bool Repeat(Action action, int tries = 3, Action afterFailed = null) {
			return Repeat(index => action(), tries, afterFailed);
		}

		public static bool Repeat(Action<int> action, int tries = 3, Action afterFailed = null) {
			int count = 1;

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
				Thread.Sleep(5);
				count++;
			}

			return false;
		}
	}
}