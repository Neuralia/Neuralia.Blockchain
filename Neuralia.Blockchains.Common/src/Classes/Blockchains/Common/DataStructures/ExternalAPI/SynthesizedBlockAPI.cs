using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {

	public abstract class SynthesizedBlockAPI {

		public long BlockId { get; set; }
		public string Timestamp { get; set; }

		public string AccountId { get; set; }
		public string AccountHash { get; set; }

		public Dictionary<string, byte[]> ConfirmedGeneralTransactions { get; set; } = new Dictionary<string, byte[]>();
		public Dictionary<string, byte[]> ConfirmedTransactions { get; set; } = new Dictionary<string, byte[]>();
		public Dictionary<string, int> RejectedTransactions { get; set; } = new Dictionary<string, int>();

		[JsonIgnore]
		public abstract List<SynthesizedElectionResultAPI> FinalElectionResultsBase { get; }

		[JsonIgnore]
		public abstract SynthesizedGenesisBlockAPI SynthesizedGenesisBlockBase { get; }

		public abstract class SynthesizedElectionResultAPI {
			public byte Offset { get; set; }
			public string Timestamp { get; set; }
			public byte ElectedTier { get; set; }
			public string DelegateAccountId { get; set; }
			public string SelectedTransactions { get; set; }
		}

		public abstract class SynthesizedGenesisBlockAPI {
			public string Inception { get; set; }
		}
	}

	public abstract class SynthesizedBlockAPI<ELECTION_RESULTS, GENESIS> : SynthesizedBlockAPI
		where ELECTION_RESULTS : SynthesizedBlockAPI.SynthesizedElectionResultAPI
		where GENESIS : SynthesizedBlockAPI.SynthesizedGenesisBlockAPI {

		public List<ELECTION_RESULTS> FinalElectionResults { get; set; } = new List<ELECTION_RESULTS>();

		public GENESIS SynthesizedGenesisBlock { get; set; }

		[JsonIgnore]
		public override List<SynthesizedElectionResultAPI> FinalElectionResultsBase => this.FinalElectionResults.Cast<SynthesizedElectionResultAPI>().ToList();

		[JsonIgnore]
		public override SynthesizedGenesisBlockAPI SynthesizedGenesisBlockBase => this.SynthesizedGenesisBlock;
	}
}