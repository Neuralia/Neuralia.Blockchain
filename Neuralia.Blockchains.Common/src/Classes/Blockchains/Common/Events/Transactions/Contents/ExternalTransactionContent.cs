namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Contents {
	public interface IExternalTransactionContent : ITransactionContent {
	}

	/// <summary>
	///     represents content where the main data is stored in its own external file.
	///     for example, could be video, database data, files, etc. saving them separately allows to discard the details data
	///     without the main content.
	/// </summary>
	public abstract class ExternalTransactionContent : TransactionContent, IExternalTransactionContent {
	}
}