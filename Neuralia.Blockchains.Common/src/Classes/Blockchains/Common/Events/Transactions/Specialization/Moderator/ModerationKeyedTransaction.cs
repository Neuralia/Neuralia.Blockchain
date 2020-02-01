using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator {
	public interface IModerationKeyedTransaction : IModerationMasterTransaction, IKeyedTransaction {
	}

	public abstract class ModerationKeyedTransaction : KeyedTransaction, IModerationKeyedTransaction {
	}
}