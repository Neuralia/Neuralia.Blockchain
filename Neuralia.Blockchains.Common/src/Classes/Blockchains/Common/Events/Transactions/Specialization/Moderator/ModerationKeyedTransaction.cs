namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator {
	public interface IModerationKeyedTransaction : IModerationIndexedTransaction, IKeyedTransaction {
	}

	public abstract class ModerationKeyedTransaction : KeyedTransaction, IModerationKeyedTransaction {
	}
}