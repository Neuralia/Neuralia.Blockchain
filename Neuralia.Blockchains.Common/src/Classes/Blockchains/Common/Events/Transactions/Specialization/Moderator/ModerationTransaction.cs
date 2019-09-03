using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator {
	public interface IModerationTransaction : ITransaction, IModeration {
	}

	public abstract class ModerationTransaction : Transaction, IModerationTransaction {
	}
}