using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General {

	public interface IDebugTransaction : ITransaction {
	}

	public abstract class DebugTransaction : Transaction, IDebugTransaction {

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.DEBUG, 1, 0);
		}
	}
}