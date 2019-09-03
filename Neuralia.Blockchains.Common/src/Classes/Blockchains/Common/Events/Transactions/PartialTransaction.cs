namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions {

	public interface IPartialTransaction : ITransaction {
	}

	public abstract class PartialTransaction : Transaction, IPartialTransaction {
	}
}