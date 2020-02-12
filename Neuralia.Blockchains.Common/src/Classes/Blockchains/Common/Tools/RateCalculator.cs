using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Core.Collections;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	public class RateCalculator {
		
		private readonly WrapperConcurrentQueue<SyncHistory> insertionHistory = new WrapperConcurrentQueue<SyncHistory>();

		public void AddHistoryEntry(long blockId) {
			this.insertionHistory.Enqueue(new SyncHistory {blockId = blockId, timestamp = DateTime.UtcNow});

			while(this.insertionHistory.Count > 100) {
				this.insertionHistory.TryDequeue(out var item);
			}
		}

		public string CalculateSyncingRate(long remaining) {

			if(this.insertionHistory.Any()) {
				var entries = this.insertionHistory.ToArray();

				SyncHistory min = entries.MinBy(e => e.timestamp).First();
				SyncHistory max = entries.MaxBy(e => e.timestamp).First();

				TimeSpan timeRange = max.timestamp - min.timestamp;
				long blockRange = max.blockId - min.blockId;
				
				decimal multiplier = 0;

				if(blockRange > 0) {
					multiplier = (decimal) remaining / blockRange;
				}

				timeRange = TimeSpan.FromSeconds((int) ((decimal) timeRange.TotalSeconds * multiplier));

				string tal = "";

				if((timeRange.Days == 0) && (timeRange.Hours == 0) && (timeRange.Minutes == 0)) {
					return $"{timeRange.Seconds} second{(timeRange.Seconds == 1 ? "" : "s")}";
				}

				return $"{timeRange.Days} day{(timeRange.Days == 1 ? "" : "s")}, {timeRange.Hours} hour{(timeRange.Hours == 1 ? "" : "s")}, {timeRange.Minutes} minute{(timeRange.Minutes == 1 ? "" : "s")}";

			}

			return "";
		}
		
		private struct SyncHistory {
			public long blockId;
			public DateTime timestamp;
		}

	}
}