using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Pools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {

	public interface IBatchingProviderBase {
		string Key { get; }
		bool HasEntries { get; }
		Task Flush(bool finishing);
		void SetRunning(bool running);
	}
	public abstract class BatchingProviderBase<T> : IBatchingProviderBase
	where T : BatchingProviderBase<T>.BatchingEntry, new(){

		protected enum SyncTypes {
			Sync, Async, Best
		}

		protected ConcurrentBag<T> Entries { get; private set; } = new ConcurrentBag<T>();
		private readonly ObjectPool<T> LogEntriesPool = new ObjectPool<T>(() => new T(), 100, 100);
		public bool IsRunning { get; private set; }

		public abstract string Key { get; }
		protected abstract SyncTypes SyncType { get; }
		
		public bool HasEntries => this.Entries.Any();

		protected T GetEntry() {
			var entry = this.LogEntriesPool.GetObject();
			entry.Timestamp = DateTimeEx.CurrentTime;

			return entry;
		}
		
		public void SetRunning(bool running) {
			this.IsRunning = running;
		}
		
		public abstract class BatchingEntry {
			public DateTime Timestamp { get; protected internal set; }

		}

		public async Task Flush(bool finishing) {
			// swap entries
			var entriesGroup = this.Entries;
			this.Entries = new ConcurrentBag<T>();

			foreach(var entry in entriesGroup.OrderBy(e => e.Timestamp)) {

				var syncType = this.SyncType;

				if(syncType == SyncTypes.Best) {
					syncType = this.GetSyncType(entry);
				}
				
				try {
					if(syncType == SyncTypes.Sync) {
						ProcessEntry(entry);
					} else if(syncType == SyncTypes.Async){
						await ProcessEntryAsync(entry).ConfigureAwait(false);
					}
				} catch(Exception ex) {

					if(!finishing) {
						NLog.Default.Error(ex, $"Failed to flush entry, it will be retried a bit later.");
						// we take it back into a further retry
						this.Entries.Add(entry);
					} else {
						int retries = 10;
						
						// this is bad, we are finishing. we will retry it a few times
						NLog.Default.Error(ex, $"Failed to flush entry on finishing. We will retry {retries} times now...");
						
						try {
							if(syncType == SyncTypes.Sync) {
								Repeater.Repeat(() => this.ProcessEntry(entry), retries);
							} else if(syncType == SyncTypes.Async) {
								await Repeater.RepeatAsync(() => this.ProcessEntryAsync(entry), retries).ConfigureAwait(false);
							}
						} catch(Exception ex2) {
							NLog.Default.Error(ex2, $"Failed to flush entry after {retries} retries. Now we have to give up.");
						}
					}
				}

				this.ResetEntry(entry);
				this.LogEntriesPool.PutObject(entry);
			}
		}

		protected abstract Task ProcessEntryAsync(T entry);
		
		protected abstract void ProcessEntry(T entry);

		protected abstract void ResetEntry(T entry);
		
		protected virtual SyncTypes GetSyncType(T entry) {
			return this.SyncType;
		}
		
	}
}