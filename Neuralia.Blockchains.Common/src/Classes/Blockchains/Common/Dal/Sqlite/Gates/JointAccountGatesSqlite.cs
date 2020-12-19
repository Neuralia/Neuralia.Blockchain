using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.Gates;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.Gates {
	public interface IJointAccountGatesSqlite : IJointAccountGates  {
		
	}
	public abstract class JointAccountGatesSqlite : IJointAccountGatesSqlite {

		public long AccountId { get; set; }
		public long? RulesBlockId { get; set; }
	}
}