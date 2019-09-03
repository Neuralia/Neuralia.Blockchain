namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {
	public interface IAccountKeysSnapshot : ISnapshot {

		byte OrdinalId { get; set; }

		long AccountId { get; set; }

		byte[] PublicKey { get; set; }

		string DeclarationTransactionId { get; set; }

		long DeclarationBlockId { get; set; }
	}

}