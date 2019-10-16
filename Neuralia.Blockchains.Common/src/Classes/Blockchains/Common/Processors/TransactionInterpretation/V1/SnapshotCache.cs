using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public static class SnapshotCache {
		public enum EntryStatus {
			New,
			Existing,
			Modified,
			Deleted
		}
	}

	/// <summary>
	///     Maintain a modification stack of all the modifications we make to a set of snapshots
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="KEY"></typeparam>
	public class SnapshotCache<T, KEY>
		where T : class, ISnapshot {

		private readonly ICardUtils cardUtils;
		private readonly Dictionary<KEY, LinkedList<EntryDetails>> snapshotCache = new Dictionary<KEY, LinkedList<EntryDetails>>();
		private readonly List<LinkedListNode<EntryDetails>> transactionEntries = new List<LinkedListNode<EntryDetails>>();

		private bool isTransaction;

		private DateTime? recordingTimestamp;

		public SnapshotCache(ICardUtils cardUtils) {
			this.cardUtils = cardUtils;
		}

		public event Func<List<KEY>, Dictionary<KEY, T>> RequestSnapshots;
		public event Func<T> CreateSnapshot;

		public void Reset() {
			this.snapshotCache.Clear();
		}

		/// <summary>
		///     ensure taht the snapshots that do exist are loaded
		/// </summary>
		/// <param name="keys"></param>
		public (List<KEY> recovered, List<KEY> missing) EnsureSnapshots(List<KEY> keys) {
			var missing = new List<KEY>();
			var recovered = new List<KEY>();

			foreach(KEY key in keys) {
				if(!this.snapshotCache.ContainsKey(key)) {
					missing.Add(key);
				}
			}

			if(missing.Any() && (this.RequestSnapshots != null)) {
				var recoveredEntries = this.RequestSnapshots(missing);

				foreach(var newAccount in recoveredEntries) {
					this.snapshotCache[newAccount.Key] = new LinkedList<EntryDetails>();
					this.CreateEntry(newAccount.Key, newAccount.Value, SnapshotCache.EntryStatus.Existing, this.snapshotCache[newAccount.Key]);
				}

				recovered.AddRange(recoveredEntries.Keys);

				var tempMissing = missing.Where(m => !recovered.Contains(m)).ToList();
				missing.Clear();
				missing.AddRange(tempMissing);
			}

			return (recovered, missing);
		}

		public T GetEntryReadonly(KEY key) {

			this.EnsureSnapshots(new[] {key}.ToList());

			if(this.snapshotCache.ContainsKey(key)) {
				EntryDetails entry = this.snapshotCache[key].Last();

				if(entry.status == SnapshotCache.EntryStatus.Deleted) {
					// we must create a new one
					return null;
				}

				return entry.entry;
			}

			return null;
		}

		public bool CheckEntryExists(KEY key) {
			var recoveredEntries = this.RequestSnapshots?.Invoke(new[] {key}.ToList());

			return recoveredEntries?.Any() ?? false;
		}

		public T GetEntryModify(KEY key) {

			var results = this.EnsureSnapshots(new[] {key}.ToList());

			if(this.snapshotCache.ContainsKey(key)) {
				EntryDetails entry = this.snapshotCache[key].Last();

				if(entry.status == SnapshotCache.EntryStatus.Deleted) {
					// we must create a new one
					return null;
				}

				if(entry.status != SnapshotCache.EntryStatus.Modified) {
					// mark it as modified now and create a copy
					T newEntry = (T) this.cardUtils.Clone(entry.entry);

					this.CreateEntry(key, newEntry, SnapshotCache.EntryStatus.Modified, this.snapshotCache[key]);

					return newEntry;
				}

				// if we are in a transaction and the previous was not, then we also create a new entry
				if(this.isTransaction && (entry.TransactionState == TransactionState.Committed)) {
					// ok, gotta make a new entry
					T newEntry = (T) this.cardUtils.Clone(entry.entry);

					this.CreateEntry(key, newEntry, SnapshotCache.EntryStatus.Modified, this.snapshotCache[key]);

					return newEntry;
				}

				return entry.entry;
			}

			return null;
		}

		public void AddEntry(KEY key, T entry) {
			if(!this.snapshotCache.ContainsKey(key)) {
				this.snapshotCache[key] = new LinkedList<EntryDetails>();
			}

			EntryDetails lastEntry = this.snapshotCache[key].LastOrDefault();

			if(lastEntry != null) {
				if(lastEntry.status != SnapshotCache.EntryStatus.Deleted) {
					// already existed
				}
			}

			this.CreateEntry(key, entry, SnapshotCache.EntryStatus.New, this.snapshotCache[key]);
		}

		public void AddEntry(KEY key, KEY subkey, T entry) {
			if(!this.snapshotCache.ContainsKey(key)) {
				this.snapshotCache[key] = new LinkedList<EntryDetails>();
			}

			EntryDetails lastEntry = this.snapshotCache[key].LastOrDefault();

			if(lastEntry != null) {
				if(lastEntry.status != SnapshotCache.EntryStatus.Deleted) {
					// already existed
				}
			}

			this.CreateEntry(key, subkey, entry, SnapshotCache.EntryStatus.New, this.snapshotCache[key]);
		}

		public void DeleteEntry(KEY key) {
			if(this.snapshotCache.ContainsKey(key)) {
				EntryDetails lastEntry = this.snapshotCache[key].LastOrDefault();

				if(lastEntry != null) {
					if(lastEntry.status != SnapshotCache.EntryStatus.Deleted) {
						this.CreateEntry(key, lastEntry.entry, SnapshotCache.EntryStatus.Deleted, this.snapshotCache[key]);
					}
				}
			}
		}

		/// <summary>
		///     Get the latest entries for each snapshot
		/// </summary>
		/// <param name="keys"></param>
		/// <returns></returns>
		public List<T> GetEntries(List<KEY> keys) {
			this.EnsureSnapshots(keys);

			return this.snapshotCache.Where(a => keys.Contains(a.Key)).Select(a => a.Value.LastOrDefault()).Where(a => a != null).Select(a => a.entry).ToList();
		}

		/// <summary>
		///     Get the entire modification stack for each snapshots
		/// </summary>
		/// <returns></returns>
		public Dictionary<KEY, List<(T entry, SnapshotCache.EntryStatus status)>> GetEntriesModificationStack() {

			return this.snapshotCache.ToDictionary(e => e.Key, e => e.Value.OrderBy(v => v.timestamp).Select(v => (v.entry, v.status)).ToList());
		}

		public Dictionary<KEY, List<(T entry, KEY subKey, SnapshotCache.EntryStatus status)>> GetEntriesSubKeyModificationStack() {

			return this.snapshotCache.ToDictionary(e => e.Key, e => e.Value.OrderBy(v => v.timestamp).Select(v => (v.entry, v.subKey, v.status)).ToList());
		}

		public void BeginTransaction() {
			this.RollbackTransaction();

			this.isTransaction = true;
		}

		public void CommitTransaction() {
			if(this.isTransaction) {
				this.isTransaction = false;

				foreach(var entry in this.transactionEntries) {
					if((entry.Previous != null) && (entry.Value.status == SnapshotCache.EntryStatus.Modified) && (entry.Previous.Value.status == SnapshotCache.EntryStatus.Modified) && (entry.Previous.Value.TransactionState == TransactionState.Committed)) {
						// ok, we can merge them together by deleting the previous one
						entry.List.Remove(entry.Previous);
					}

					entry.Value.TransactionState = TransactionState.Committed;
				}

				this.transactionEntries.Clear();
			}
		}

		public void RollbackTransaction() {
			if(this.isTransaction) {
				this.isTransaction = false;

				foreach(var entry in this.transactionEntries) {
					this.snapshotCache[entry.Value.key].Remove(entry);
				}

				this.transactionEntries.Clear();
			}

		}

		private void CreateEntry(KEY key, T entry, SnapshotCache.EntryStatus status, LinkedList<EntryDetails> list) {
			this.CreateEntry(key, key, entry, status, list);
		}

		private void CreateEntry(KEY key, KEY subKey, T entry, SnapshotCache.EntryStatus status, LinkedList<EntryDetails> list) {
			EntryDetails entryDetail = new EntryDetails {
				key = key, subKey = subKey, entry = entry, status = status,
				timestamp = DateTime.UtcNow, TransactionState = this.isTransaction ? TransactionState.InProgress : TransactionState.Committed
			};

			var node = list.AddLast(entryDetail);

			if(this.isTransaction) {
				this.transactionEntries.Add(node);
			}
		}

		private enum TransactionState {
			Committed,
			InProgress
		}

		private class EntryDetails {
			public T entry;
			public KEY key;
			public SnapshotCache.EntryStatus status;
			public KEY subKey;
			public DateTime timestamp;
			public TransactionState TransactionState;
		}
	}
}