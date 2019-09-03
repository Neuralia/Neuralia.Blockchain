namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations {
	public class StandardAccountKeysSnapshot : IStandardAccountKeysSnapshot {

		public byte OrdinalId { get; set; }
		public long AccountId { get; set; }
		public byte[] PublicKey { get; set; }
		public string DeclarationTransactionId { get; set; }
		public long DeclarationBlockId { get; set; }
	}
}