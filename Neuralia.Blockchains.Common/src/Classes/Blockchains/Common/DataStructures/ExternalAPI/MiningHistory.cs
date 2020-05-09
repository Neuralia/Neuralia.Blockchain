using System;
using System.Collections.Generic;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {

	public class MiningHistory {
		public List<string> selectedTransactions { get; set; } = new List<string>();
		public long blockId { get; set; }
		public DateTime Timestamp { get; set; }
		public byte Level { get; set; }
		public ushort Message { get; set; }
		public object[] Parameters { get; set; }

		public override string ToString() {
			return $"BlockId: {this.blockId}, SelectedTransactions: {string.Join(",", this.selectedTransactions)}";
		}
	}
}