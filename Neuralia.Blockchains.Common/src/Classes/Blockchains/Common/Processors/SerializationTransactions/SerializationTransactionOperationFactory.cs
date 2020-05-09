using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions.Operations;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions {
	public static class SerializationTransactionOperationFactory {

		public static SerializationTransactionOperation Rehydrate(IDataRehydrator rehydrator, IChainDataWriteProvider chainDataWriteProvider) {

			SerializationTransactionOperationTypes type = (SerializationTransactionOperationTypes) rehydrator.ReadInt();

			SerializationTransactionOperation entry = null;

			switch(type) {
				case SerializationTransactionOperationTypes.FastKeys:
					entry = new SerializationFastKeysOperations(chainDataWriteProvider);

					break;
				default:
					throw new ApplicationException();
			}

			entry.Rehydrate(rehydrator);

			return entry;
		}
	}
}