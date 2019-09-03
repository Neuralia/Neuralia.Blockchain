using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions.Operations {
	public abstract class SerializationTransactionOperation : IBinarySerializable {
		protected readonly IChainDataWriteProvider chainDataWriteProvider;

		protected readonly ISerializationManager serializationManager;

		public SerializationTransactionOperation(ISerializationManager serializationManager, IChainDataWriteProvider chainDataWriteProvider) {
			this.SetType();

			this.serializationManager = serializationManager;
			this.chainDataWriteProvider = chainDataWriteProvider;
		}

		public SerializationTransactionOperationTypes SerializationTransactionOperationType { get; protected set; }

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write((int) this.SerializationTransactionOperationType);
		}

		protected abstract void SetType();

		public abstract void Undo();
	}
}