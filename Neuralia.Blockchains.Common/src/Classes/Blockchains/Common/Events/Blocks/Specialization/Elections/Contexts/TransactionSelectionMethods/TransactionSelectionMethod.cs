using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods {
	public interface ITransactionSelectionMethod : IVersionable<TransactionSelectionMethodType>, IBinarySerializable, IJsonSerializable {
		Task<List<TransactionId>> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions, List<WebTransactionPoolResult> webTransactions);
	}

	public abstract class TransactionSelectionMethod : Versionable<TransactionSelectionMethodType>, ITransactionSelectionMethod {
		protected readonly long blockId;

		protected readonly IChainMiningStatusProvider chainMiningStatusProvider;
		protected readonly IChainStateProvider chainStateProvider;
		protected readonly BlockChainConfigurations configuration;
		protected readonly IElectionContext electionContext;

		protected readonly IWalletProvider walletProvider;

		public TransactionSelectionMethod(long blockId, IChainMiningStatusProvider chainMiningStatusProvider, BlockChainConfigurations configuration, IChainStateProvider chainStateProvider, IWalletProvider walletProvider, IElectionContext electionContext) {
			this.chainMiningStatusProvider = chainMiningStatusProvider;
			this.walletProvider = walletProvider;
			this.blockId = blockId;
			this.electionContext = electionContext;
			this.chainStateProvider = chainStateProvider;
			this.configuration = configuration;
		}

		protected int MaximumTransactionCount {
			get {
				var miningTier = chainMiningStatusProvider.MiningTier;

				if(this.electionContext.MaximumElectedTransactionCount.ContainsKey(miningTier)) {
					return this.electionContext.MaximumElectedTransactionCount[miningTier];
				}

				return 0;
			}
		}

		public virtual async Task<List<TransactionId>> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions, List<WebTransactionPoolResult> webTransactions) {
			List<TransactionId> poolTransactions = await chainEventPoolProvider.GetTransactionIds().ConfigureAwait(false);

			// exclude the transactions that should not be selected
			List<TransactionId> availableTransactions = poolTransactions.Where(p => !existingTransactions.Contains(p)).ToList();
			List<WebTransactionPoolResult> availableWebTransactions = webTransactions.Where(p => !existingTransactions.Contains(new TransactionId(p.TransactionId))).ToList();

			return this.SelectSelection(availableTransactions, availableWebTransactions);
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		protected abstract List<TransactionId> SelectSelection(List<TransactionId> transactionIds, List<WebTransactionPoolResult> availableWebTransactions);
	}
}