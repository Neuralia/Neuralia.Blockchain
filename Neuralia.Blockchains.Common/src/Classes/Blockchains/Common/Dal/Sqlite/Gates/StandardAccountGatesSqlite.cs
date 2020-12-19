using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.Gates;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.Gates {

	public interface IStandardAccountGatesSqlite : IStandardAccountGates  {
		
	}
	public abstract class StandardAccountGatesSqlite : IStandardAccountGatesSqlite {

		public long AccountId { get; set; }
		public byte[] TransactionKeyGate { get; set; }
		public byte[] MessageKeyGate { get; set; }
		public byte[] ChangeKeyGate { get; set; }
		public byte[] SuperKeyGate { get; set; }
		public byte[] ValidatorSignatureKeyGate { get; set; }
		public byte[] ValidatorSecretKeyGate { get; set; }
	}
}