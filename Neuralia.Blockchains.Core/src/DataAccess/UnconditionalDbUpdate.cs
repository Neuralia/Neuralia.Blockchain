using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.DataAccess {
	public static class UnconditionalDbUpdate {

		private static void LogError(Exception oex, string message) {
			if(!string.IsNullOrWhiteSpace(message)) {
				NLog.Default.Error(oex, message);
			}
		}

		/// <summary>
		///     run a DB operation where we dont really care about the errors we can git. shoot and forget
		/// </summary>
		/// <param name="action"></param>
		/// <param name="logError"></param>
		public static void RunUnconditionalDbUpdate(Action action, string logError) {

			try {
				Repeater.Repeat(action, 2);
			} catch(DbUpdateConcurrencyException bdcex) {
				LogError(bdcex, logError);
			} catch(SynchronizationLockException syncEx) {
				LogError(syncEx, logError);
			} catch(DbUpdateException syncEx) {
				LogError(syncEx, logError);
			} catch(Exception ex) {
				LogError(ex, logError);
			}
		}

		public static async Task RunUnconditionalDbUpdateAsync(Func<Task> action, string logError) {

			try {
				await Repeater.RepeatAsync(action, 2).ConfigureAwait(false);
			} catch(DbUpdateConcurrencyException bdcex) {
				LogError(bdcex, logError);
			} catch(SynchronizationLockException syncEx) {
				LogError(syncEx, logError);
			} catch(DbUpdateException syncEx) {
				LogError(syncEx, logError);
			} catch(Exception ex) {
				LogError(ex, logError);
			}
		}

		/// <summary>
		///     ignore any concurrent update errors
		/// </summary>
		/// <param name="action"></param>
		/// <param name="logError"></param>
		public static void RunDbUpdateConcurrent(Action action, string logError = null) {

			try {
				Repeater.Repeat(action, 2);
			} catch(DbUpdateConcurrencyException bdcex) {
				LogError(bdcex, logError);
			} catch(SynchronizationLockException syncEx) {
				LogError(syncEx, logError);
			}
		}

		public static async Task RunDbUpdateConcurrentAsync(Func<Task> action, string logError = null) {

			try {
				await Repeater.RepeatAsync(action, 2).ConfigureAwait(false);
			} catch(DbUpdateConcurrencyException bdcex) {
				LogError(bdcex, logError);
			} catch(SynchronizationLockException syncEx) {
				LogError(syncEx, logError);
			}
		}
	}
}