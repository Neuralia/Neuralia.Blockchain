using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Pools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	public interface ILoggingBatcher {
		void Error(Exception ex, string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Error(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Information(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Warning(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Verbose(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Verbose(Exception ex, string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
	}

	/// <summary>
	/// A tool to prevent potential logging contention by batching them together and releasing them under lower load
	/// </summary>
	public class LoggingBatcher : IDisposableExtended, ILoggingBatcher {
		
		private Timer pollingTimer;

		/// <summary>
		/// maximum number if instances over which we consider to be in overload
		/// </summary>
		public const int OVERLOAD_FACTOR = 100;
		
		/// <summary>
		/// rate of increase factor where we are in danger of overload
		/// </summary>
		public const int OVERLOAD_INCREASE_RATE = 5;
		
		private static readonly TimeSpan PollRate = TimeSpan.FromSeconds(10);
		private bool running;
		
		public LoggingBatcher() {

		}

		public void Start() {
			Interlocked.Increment(ref this.users);
			this.SetTimer();
			this.running = true;
		}

		public void Stop() {
			
			Interlocked.Decrement(ref this.users);

			if(Interlocked.CompareExchange(ref this.users, 0, 0) == 0) {
				try {
					this.StopTimer();
				} catch {

				}

				this.running = false;

				try {
					this.Flush(true, true);
				} catch {

				}
			}
		}
		
		private void SetTimer() {

			this.StopTimer();
			this.pollingTimer = new Timer(state => {

				try {
					this.Flush();
				} catch(Exception ex) {
					NLog.Default.Error(ex, "Failed to flush messages on timer event");
				}

			}, this, PollRate, PollRate);
		}

		private void StopTimer() {
			try {
				this.pollingTimer?.Dispose();
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to dispose timer");
			} finally {
				this.pollingTimer = null;
			}
		}

		private long users = 0;
		
		private long previousInstancesSnapshot = 0;
		private long currentInstancesSnapshot = 0;
		private long currentInstances = 0;
		
		private readonly ObjectPool<LogEntry> LogEntriesPool = new ObjectPool<LogEntry>(() => new LogEntry(), 100, 100);
		private ConcurrentBag<LogEntry> entries = new ConcurrentBag<LogEntry>();

		public void IncrementInstances() {
			Interlocked.Increment(ref this.currentInstances);
		}
		
		public void DecrementInstances() {
			Interlocked.Decrement(ref this.currentInstances);
		}
		
		private void TakeSnapshot() {
			this.previousInstancesSnapshot = this.currentInstancesSnapshot;
			this.currentInstancesSnapshot = Interlocked.CompareExchange(ref this.currentInstances, 0,0);
		}
		
		/// <summary>
		/// if the system is not in overloaded state, we flush the message cache, otherwise we hold on to it a bit more until the load is more acceptable
		/// </summary>
		public void Flush(bool force = false, bool finishing = false) {
			
			// first determine if we have an overload
			this.TakeSnapshot();

			if(!force) {
				if(this.currentInstancesSnapshot > OVERLOAD_FACTOR) {
					// it is too heavy, better wait a bit
					return;
				}

				double rateOfIncrease = this.currentInstancesSnapshot;

				if(this.previousInstancesSnapshot != 0) {
					rateOfIncrease /= this.previousInstancesSnapshot;
				}

				if(rateOfIncrease > OVERLOAD_INCREASE_RATE) {
					// Although we are bellow the overload rate, the rate of increase is too fast, we will wait a bit more.
					return;
				}
			}

			if(this.entries.Any()) {
				try {
					this.StopTimer();
					
					var entriesGroup = this.entries;
					this.entries = new ConcurrentBag<LogEntry>();

					foreach(var entry in entriesGroup) {
						string message = $"[Async: {entry.Timestamp}] - {entry.Message}";

						var logger = NLog.GetLogger(entry.Loggertype);
						if(entry.LogEntryType == LogEntry.LogTypes.Error) {
							if(entry.Exception == null) {
								logger.Error(message);
							} else {
								logger.Error(entry.Exception, message);
							}
						} else if(entry.LogEntryType == LogEntry.LogTypes.Information) {
							logger.Information(message);
						} else if(entry.LogEntryType == LogEntry.LogTypes.Warning) {
							logger.Warning(message);
						} else if(entry.LogEntryType == LogEntry.LogTypes.Verbose) {
							if(entry.Exception == null) {
								logger.Verbose(message);
							} else {
								logger.Verbose(entry.Exception, message);
							}
						}

						this.LogEntriesPool.PutObject(entry);
					}
				} finally {
					if(!finishing) {
						this.SetTimer();
					}
				}
			}
		}

		public void Error(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.running) {
				NLog.GetLogger(loggertype).Error(message);

				return;
			}
			var entry = this.LogEntriesPool.GetObject();
			entry.Timestamp = DateTimeEx.CurrentTime;
			entry.LogEntryType = LogEntry.LogTypes.Error;
			entry.Message = message;
			this.entries.Add(entry);
		}

		public void Error(Exception ex, string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.running) {
				NLog.GetLogger(loggertype).Error(ex, message);

				return;
			}
			var entry = this.LogEntriesPool.GetObject();
			entry.Timestamp = DateTimeEx.CurrentTime;
			entry.LogEntryType = LogEntry.LogTypes.Error;
			entry.Message = message;
			entry.Exception = ex;
			entry.Loggertype = loggertype;
			this.entries.Add(entry);
		}
		
		public void Information(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.running) {
				NLog.GetLogger(loggertype).Information(message);

				return;
			}
			var entry = this.LogEntriesPool.GetObject();
			entry.Timestamp = DateTimeEx.CurrentTime;
			entry.LogEntryType = LogEntry.LogTypes.Information;
			entry.Message = message;
			entry.Loggertype = loggertype;
			this.entries.Add(entry);
		}
		
		public void Warning(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.running) {
				NLog.GetLogger(loggertype).Warning(message);

				return;
			}
			var entry = this.LogEntriesPool.GetObject();
			entry.Timestamp = DateTimeEx.CurrentTime;
			entry.LogEntryType = LogEntry.LogTypes.Warning;
			entry.Message = message;
			entry.Loggertype = loggertype;
			this.entries.Add(entry);
		}
		
		public void Verbose(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.running) {
				NLog.GetLogger(loggertype).Verbose(message);

				return;
			}
			var entry = this.LogEntriesPool.GetObject();
			entry.Timestamp = DateTimeEx.CurrentTime;
			entry.LogEntryType = LogEntry.LogTypes.Verbose;
			entry.Message = message;
			entry.Loggertype = loggertype;
			this.entries.Add(entry);
		}
		
		public void Verbose(Exception ex, string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.running) {
				NLog.GetLogger(loggertype).Verbose(message);

				return;
			}
			var entry = this.LogEntriesPool.GetObject();
			entry.Timestamp = DateTimeEx.CurrentTime;
			entry.LogEntryType = LogEntry.LogTypes.Verbose;
			entry.Message = message;
			entry.Loggertype = loggertype;
			entry.Exception = ex;
			this.entries.Add(entry);
		}
		
		private class LogEntry {

			public enum LogTypes {
				Information,Verbose,Warning,Error
			}

			public LogTypes LogEntryType { get; set; }
			public DateTime Timestamp { get; set; }
			public string Message { get; set; }
			public Exception Exception { get; set; }
			public NLog.LoggerTypes Loggertype { get; set; }
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				this.Stop();

				this.IsDisposed = true;
			}
		}

		~LoggingBatcher() {
			this.Dispose(false);
		}

	#endregion
	}
}