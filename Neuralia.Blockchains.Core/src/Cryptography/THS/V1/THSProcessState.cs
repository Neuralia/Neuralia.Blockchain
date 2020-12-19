using System;
using System.Collections.Generic;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1 {
	public class THSProcessState {
		public class SolutionEntry {

			public SolutionEntry(int solution, long nonce) {
				this.Solution = solution;
				this.Nonce = nonce;
			}

			public int Solution { get; set; } 
			public long Nonce { get; set; }
		}
		public long Nonce { get; set; } = 1;
		public int Round { get; set; } = 1;
		public long TotalNonce { get; set; } = 1;

		public List<SolutionEntry> Solutions { get; set; } = new List<SolutionEntry>();
	}
}