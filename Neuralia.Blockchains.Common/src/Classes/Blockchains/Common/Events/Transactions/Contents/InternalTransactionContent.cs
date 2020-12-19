namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Contents {

	public interface IInternalTransactionContent : ITransactionContent {
	}

	/// <summary>
	///     represents content where the main data is stored in the details file
	/// </summary>
	public abstract class InternalTransactionContent : TransactionContent, IInternalTransactionContent {
	}
}