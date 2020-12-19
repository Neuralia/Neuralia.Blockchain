using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization {

	public interface IElectionContextRehydrationFactory {

		//		TransactionSerializationMap CreateTransactionDehydrationMap(byte type, byte major, byte minor, ByteArray keyLengths);
		IElectionContext CreateElectionContext(SafeArrayHandle compressedContext);
		ITransactionSelectionMethodFactory CreateTransactionSelectionMethodFactory();

		IElectionResultsRehydrator CreateElectionResultsRehydrator();
	}

	public interface IBlockComponentsRehydrationFactory : IElectionContextRehydrationFactory {
	}

	public abstract class BlockComponentsRehydrationFactory : IBlockComponentsRehydrationFactory {
		public abstract IElectionContext CreateElectionContext(SafeArrayHandle compressedContext);
		public abstract ITransactionSelectionMethodFactory CreateTransactionSelectionMethodFactory();
		public abstract IElectionResultsRehydrator CreateElectionResultsRehydrator();
	}
}