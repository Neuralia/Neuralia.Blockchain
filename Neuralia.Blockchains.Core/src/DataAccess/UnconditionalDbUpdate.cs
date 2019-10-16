using System;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.DataAccess {
	public static class UnconditionalDbUpdate {
		/// <summary>
		/// run a DB operation where we dont really care about the errors we can git. shoot and forget
		/// </summary>
		/// <param name="action"></param>
		/// <param name="logError"></param>
		public static void RunUnconditionalDbUpdate(Action action, string logError) {
			
			try {
				Repeater.Repeat(action);
			} 
			catch(DbUpdateConcurrencyException bdcex) {
				Log.Error(bdcex, logError);
			}
			catch(SynchronizationLockException syncEx) {
				Log.Error(syncEx, logError);
			}
			catch(DbUpdateException syncEx) {
				Log.Error(syncEx, logError);
			}
			catch(Exception ex) {
				Log.Error(ex, logError);
			}
		}
		
		/// <summary>
		/// ignore any concurrent update errors
		/// </summary>
		/// <param name="action"></param>
		/// <param name="logError"></param>
		public static void RunDbUpdateConcurrent(Action action, string logError = null) {
			
			try {
				Repeater.Repeat(action);
			} 
			catch(DbUpdateConcurrencyException bdcex) {
				if(!string.IsNullOrWhiteSpace(logError)) {
					Log.Error(bdcex, logError);
				}
			}
			catch(SynchronizationLockException syncEx) {
				if(!string.IsNullOrWhiteSpace(logError)) {
					Log.Error(syncEx, logError);
				}
			}
		}
	}
}