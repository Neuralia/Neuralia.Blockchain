using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	
	public interface IOperationBatcher {
		void AddOperation(Action<DateTime> action);
		Task AddOperationAsync(Func<DateTime, Task> actionAsync);
	}
	
	public class OperationBatchingProvider: BatchingProviderBase<OperationBatchingProvider.OperationEntry>, IOperationBatcher {

		private static IOperationBatcher operationBatcher;
		private static object operationBatcherLocker = new object();
		public static IOperationBatcher OperationsBatcher {
			get {
				if(operationBatcher != null) {
					return operationBatcher;
				}

				lock(operationBatcherLocker) {
					if(operationBatcher == null) {
						operationBatcher = LoadBatcher.Instance.GetProvider<OperationBatchingProvider>(OPERATIONS_BATCHER_KEY);
					}
				}
				
				return operationBatcher;
			}
		}

		public const string OPERATIONS_BATCHER_KEY = "Operations";

		public override string Key => OPERATIONS_BATCHER_KEY;
		protected override SyncTypes SyncType => SyncTypes.Best;
		
		protected override Task ProcessEntryAsync(OperationEntry entry) {

			return entry.ActionAsync(entry.Timestamp);
		}

		protected override void ProcessEntry(OperationEntry entry) {
			entry.Action(entry.Timestamp);
		}

		protected override void ResetEntry(OperationEntry entry) {
			entry.Action = null;
			entry.ActionAsync = null;
		}

		protected override SyncTypes GetSyncType(OperationEntry entry) {
			if(entry.ActionAsync != null) {
				return SyncTypes.Async;
			}
			else if(entry.Action != null) {
				return SyncTypes.Sync;
			} else {
				throw new ApplicationException("Invalid sync type");
			}
		}

		public class OperationEntry : BatchingEntry {

			public Func<DateTime, Task> ActionAsync { get; set; }
			public Action<DateTime> Action { get; set; }
			public override bool IsValid => this.Action != null || this.ActionAsync != null;
		}

		public void AddOperation(Action<DateTime> action) {

			if(action == null) {
				return;
			}
			if(!this.IsRunning) {
				action(DateTimeEx.CurrentTime);

				return;
			}
			
			var entry = this.GetEntry();
			entry.Action = action;
			this.Entries.Add(entry);
		}

		public Task AddOperationAsync(Func<DateTime, Task> actionAsync) {
			if(actionAsync == null) {
				return Task.CompletedTask;
			}
			if(!this.IsRunning) {
				return actionAsync(DateTimeEx.CurrentTime);
			}
			
			var entry = this.GetEntry();
			entry.ActionAsync = actionAsync;
			this.Entries.Add(entry);
			
			return Task.CompletedTask;
		}
	}
}