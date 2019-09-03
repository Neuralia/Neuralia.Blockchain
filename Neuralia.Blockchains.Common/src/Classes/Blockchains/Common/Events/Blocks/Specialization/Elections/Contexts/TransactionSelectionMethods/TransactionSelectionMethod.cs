using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods {
	public interface ITransactionSelectionMethod : IVersionable<TransactionSelectionMethodType>, IBinarySerializable, IJsonSerializable {
		List<TransactionId> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions);
	}

	public abstract class TransactionSelectionMethod : Versionable<TransactionSelectionMethodType>, ITransactionSelectionMethod {
		protected readonly long blockId;
		protected readonly ushort maximumTransactionCount;

		protected readonly IWalletProvider walletProvider;

		public TransactionSelectionMethod(long blockId, IWalletProvider walletProvider, ushort maximumTransactionCount) {
			this.walletProvider = walletProvider;
			this.blockId = blockId;
			this.maximumTransactionCount = maximumTransactionCount;
		}

		public virtual List<TransactionId> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions) {
			var poolTransactions = chainEventPoolProvider.GetTransactionIds();

			// exclude the transactions that should not be selected
			var availableTransactions = poolTransactions.Where(p => !existingTransactions.Contains(p)).ToList();

			return this.SelectSelection(availableTransactions);
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		protected abstract List<TransactionId> SelectSelection(List<TransactionId> transactionIds);
	}
}