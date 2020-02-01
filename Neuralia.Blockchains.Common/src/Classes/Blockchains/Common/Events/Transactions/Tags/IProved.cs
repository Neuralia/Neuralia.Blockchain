using System.Collections.Generic;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags {
	public interface IProved : ITransaction {
		int PowNonce { get; set; }
		List<int> PowSolutions { get; set; }
		ushort PowDifficulty { get; set; }
	}
}