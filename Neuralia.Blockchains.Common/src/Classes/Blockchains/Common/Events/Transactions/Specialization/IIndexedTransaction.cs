using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization {
	public interface IIndexedTransaction : ITransaction, IRateLimitedTransaction {
	}
}