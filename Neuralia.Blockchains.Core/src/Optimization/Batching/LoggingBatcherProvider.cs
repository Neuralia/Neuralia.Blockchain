using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	
	public interface ILoggingBatcher {
		void Error(Exception ex, string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Error(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Information(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Warning(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Verbose(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
		void Verbose(Exception ex, string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default);
	}

	public class LoggingBatcherProvider : BatchingProviderBase<LoggingBatcherProvider.LogEntry>, ILoggingBatcher {


		private static ILoggingBatcher loggingBatcher;
		private static object loggingBatcherLocker = new object();
		public static ILoggingBatcher LoggingBatcher {
			get {
				if(loggingBatcher != null) {
					return loggingBatcher;
				}

				lock(loggingBatcherLocker) {
					if(loggingBatcher == null) {
						loggingBatcher = LoadBatcher.Instance.GetProvider<LoggingBatcherProvider>(LOGGING_BATCHER_KEY);
					}
				}
				
				return loggingBatcher;
			}
		}
		
		public const string LOGGING_BATCHER_KEY = "Logging";
		
		public void Error(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.IsRunning) {
				NLog.GetLogger(loggertype).Error(message);

				return;
			}
			var entry = this.GetEntry();
			entry.LogEntryType = LogEntry.LogTypes.Error;
			entry.Message = message;
			this.Entries.Add(entry);
		}

		public void Error(Exception ex, string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.IsRunning) {
				NLog.GetLogger(loggertype).Error(ex, message);

				return;
			}
			var entry = this.GetEntry();
			entry.LogEntryType = LogEntry.LogTypes.Error;
			entry.Message = message;
			entry.Exception = ex;
			entry.Loggertype = loggertype;
			this.Entries.Add(entry);
		}
		
		public void Information(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.IsRunning) {
				NLog.GetLogger(loggertype).Information(message);

				return;
			}

			var entry = this.GetEntry();
			entry.LogEntryType = LogEntry.LogTypes.Information;
			entry.Message = message;
			entry.Loggertype = loggertype;
			this.Entries.Add(entry);
		}
		
		public void Warning(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.IsRunning) {
				NLog.GetLogger(loggertype).Warning(message);

				return;
			}
			var entry = this.GetEntry();
			entry.LogEntryType = LogEntry.LogTypes.Warning;
			entry.Message = message;
			entry.Loggertype = loggertype;
			this.Entries.Add(entry);
		}
		
		public void Verbose(string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.IsRunning) {
				NLog.GetLogger(loggertype).Verbose(message);

				return;
			}
			var entry = this.GetEntry();
			entry.LogEntryType = LogEntry.LogTypes.Verbose;
			entry.Message = message;
			entry.Loggertype = loggertype;
			this.Entries.Add(entry);
		}
		
		public void Verbose(Exception ex, string message, NLog.LoggerTypes loggertype = NLog.LoggerTypes.Default) {
			if(!this.IsRunning) {
				NLog.GetLogger(loggertype).Verbose(message);

				return;
			}
			var entry = this.GetEntry();
			entry.LogEntryType = LogEntry.LogTypes.Verbose;
			entry.Message = message;
			entry.Loggertype = loggertype;
			entry.Exception = ex;
			this.Entries.Add(entry);
		}
		
		public class LogEntry : BatchingEntry {

			public enum LogTypes {
				Information,Verbose,Warning,Error
			}

			public LogTypes LogEntryType { get; set; }
			public string Message { get; set; }
			public Exception Exception { get; set; }
			public NLog.LoggerTypes Loggertype { get; set; }
		}

		public override string Key => LOGGING_BATCHER_KEY;
		protected override SyncTypes SyncType => SyncTypes.Sync;

		protected override Task ProcessEntryAsync(LogEntry entry) {
			throw new NotImplementedException();
		}

		protected override void ProcessEntry(LoggingBatcherProvider.LogEntry entry) {
			
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
		}

		protected override void ResetEntry(LogEntry entry) {
			entry.Exception = null;
		}
	}
}