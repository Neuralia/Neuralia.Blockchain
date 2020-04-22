using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods {
	public interface ITransactionSelectionMethod : IVersionable<TransactionSelectionMethodType>, IBinarySerializable, IJsonSerializable {
		Task<List<TransactionId>> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions);
	}

	public abstract class TransactionSelectionMethod : Versionable<TransactionSelectionMethodType>, ITransactionSelectionMethod {
		protected readonly long blockId;
		protected readonly  IElectionContext electionContext;

		protected readonly IWalletProvider walletProvider;
		protected readonly IChainStateProvider chainStateProvider;
		protected readonly BlockChainConfigurations configuration;
		
		public TransactionSelectionMethod(long blockId, BlockChainConfigurations configuration, IChainStateProvider chainStateProvider, IWalletProvider walletProvider,  IElectionContext electionContext) {
			this.walletProvider = walletProvider;
			this.blockId = blockId;
			this.electionContext = electionContext;
			this.chainStateProvider = chainStateProvider;
			this.configuration = configuration;
		}

		public virtual async Task<List<TransactionId>> PerformTransactionSelection(IEventPoolProvider chainEventPoolProvider, List<TransactionId> existingTransactions) {
			var poolTransactions = await chainEventPoolProvider.GetTransactionIds().ConfigureAwait(false);

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

		protected int MaximumTransactionCount {
			get {
				var miningTier = BlockchainUtilities.GetMiningTier(this.configuration, this.chainStateProvider.DigestHeight);

				if(this.electionContext.MaximumElectedTransactionCount.ContainsKey(miningTier)) {
					return this.electionContext.MaximumElectedTransactionCount[miningTier];
				}

				return 0;
			}
		}
	}
}