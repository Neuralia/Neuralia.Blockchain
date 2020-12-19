using System.ComponentModel.DataAnnotations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState {

	public interface IChainStateModeratorKeysEntry {
	}

	public interface IChainStateModeratorKeysEntry<MODEL_SNAPSHOT, in MODERATOR_KEYS_SNAPSHOT> : IChainStateModeratorKeysEntry
		where MODEL_SNAPSHOT : class, IChainStateEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {

		[Key]
		byte OrdinalId { get; set; }

		int ChainStateId { get; set; }
		MODEL_SNAPSHOT ChainStateEntry { get; set; }

		long ExpectedNextKeyIndex { get; set; }
		long ExpectedNextKeySequenceId { get; set; }
		
		byte[] PublicKey { get; set; }

		string DeclarationTransactionId { get; set; }
		bool IsCurrent { get; set; }

		void Copy(MODERATOR_KEYS_SNAPSHOT other);
	}
}