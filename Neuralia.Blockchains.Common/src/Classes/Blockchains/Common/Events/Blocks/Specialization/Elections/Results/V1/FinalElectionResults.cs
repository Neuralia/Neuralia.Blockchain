using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Serialization.OffsetCalculators;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.General.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1 {
	public interface IFinalElectionResults : IElectionResult {

		Dictionary<AccountId, IDelegateResults> DelegateAccounts { get; }
		Dictionary<AccountId, IElectedResults> ElectedCandidates { get; }
		IElectedResults CreateElectedResult();
		IDelegateResults CreateDelegateResult();

		Dictionary<AccountId, IElectedResults> GetTierElectedCandidates(Enums.MiningTiers miningTier);
	}

	public abstract class FinalElectionResults : ElectionResult, IFinalElectionResults {
		public Dictionary<AccountId, IDelegateResults> DelegateAccounts { get; } = new Dictionary<AccountId, IDelegateResults>();
		public Dictionary<AccountId, IElectedResults> ElectedCandidates { get; } = new Dictionary<AccountId, IElectedResults>();

		public Dictionary<AccountId, IElectedResults> GetTierElectedCandidates(Enums.MiningTiers miningTier) {
			return this.ElectedCandidates.Where(e => e.Value.ElectedTier == miningTier).ToDictionary();
		}

		public override void Rehydrate(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree) {

			ClosureWrapper<SpecialIntegerSizeArray> electorTypesArray = new ClosureWrapper<SpecialIntegerSizeArray>();

			try {
				this.ElectedCandidates.Clear();
				this.DelegateAccounts.Clear();

				base.Rehydrate(rehydrator, transactionIndexesTree);

				this.RehydrateHeader(rehydrator);

				// then the elected ones

				AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Rehydrate(rehydrator);
				int count = (int) adaptiveLong.Value;
				
				List<AccountId> sortedDelegateAccounts = this.DelegateAccounts.Keys.OrderBy(k => k).ToList();

				AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId> parameters = new AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId>();

				parameters.DataPrerun = () => {
					SafeArrayHandle typeBytes = (SafeArrayHandle)rehydrator.ReadArray(SpecialIntegerSizeArray.GetbyteSize(SpecialIntegerSizeArray.BitSizes.B0d5, count));
					electorTypesArray.Value = new SpecialIntegerSizeArray(SpecialIntegerSizeArray.BitSizes.B0d5, typeBytes, count);
				};

				parameters.RehydrateExtraData = (accountId, offset, index, totalIndex, dh) => {

					// get the delegate offset
					AdaptiveLong1_9 delegateAccountOffset = rehydrator.ReadRehydratable<AdaptiveLong1_9>();

					AccountId delegateAccount = null;

					if(delegateAccountOffset != null) {
						delegateAccount = sortedDelegateAccounts[(ushort) delegateAccountOffset.Value];
					}

					IElectedResults electedCandidateResult = this.CreateElectedResult(accountId);

					this.RehydrateAccountEntry(accountId, electedCandidateResult, rehydrator);

					// now the transactions
					SequentialLongOffsetCalculator transactionIdCalculator = new SequentialLongOffsetCalculator(0);

					adaptiveLong.Rehydrate(rehydrator);
					uint transactionCount = (uint) adaptiveLong.Value;

					List<TransactionId> assignedTransactions = new List<TransactionId>();

					if(transactionCount != 0) {

						for(int j = 0; j < transactionCount; j++) {

							adaptiveLong.Rehydrate(rehydrator);

							int transactionIndex = (int) transactionIdCalculator.RebuildValue(adaptiveLong.Value);

							// that's our transaction
							if(transactionIndexesTree.ContainsKey(transactionIndex)) {
								assignedTransactions.Add(transactionIndexesTree[transactionIndex]);
							}

							transactionIdCalculator.AddLastOffset();
						}
					}

					electedCandidateResult.Transactions = assignedTransactions.OrderBy(t => t).ToList();
					electedCandidateResult.ElectedTier = (Enums.MiningTiers) electorTypesArray.Value[totalIndex];
					electedCandidateResult.DelegateAccountId = delegateAccount;

					this.ElectedCandidates.Add(accountId, electedCandidateResult);
				};

				AccountIdGroupSerializer.Rehydrate(rehydrator, true, parameters);
			} finally {
				electorTypesArray?.Value?.Dispose();
			}

		}

		public abstract IElectedResults CreateElectedResult();
		public abstract IDelegateResults CreateDelegateResult();

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetArray("DelegateAccounts", this.DelegateAccounts, (js, e) => {
				js.WriteObject(s => {
					s.SetProperty("AccountId", e.Key);
					s.SetProperty("Results", e.Value);
				});
			});

			foreach(Enums.MiningTiers tier in this.ElectedCandidates.Select(e => e.Value.ElectedTier).Distinct()) {
				jsonDeserializer.SetArray($"{tier}ElectedCandidates", this.GetTierElectedCandidates(tier), (js, e) => {
					js.WriteObject(s => {
						s.SetProperty("AccountId", e.Key);
						s.SetProperty("Results", e.Value);
					});
				});
			}
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.DelegateAccounts.Count);

			nodeList.Add(BlockchainHashingUtils.GenerateFinalElectionResultNodeList(this.DelegateAccounts));

			nodeList.Add(this.ElectedCandidates.Count);
			nodeList.Add(BlockchainHashingUtils.GenerateFinalElectionResultNodeList(this.ElectedCandidates));

			return nodeList;
		}

		protected virtual void RehydrateHeader(IDataRehydrator rehydrator) {

			this.DelegateAccounts.Clear();
			AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId> parameters = new AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId>();

			parameters.RehydrateExtraData = (delegateAccountId, offset, index, totalIndex, dh) => {

				IDelegateResults delegateEntry = this.CreateDelegateResult();
				this.DelegateAccounts.Add(delegateAccountId, delegateEntry);

				this.RehydrateDelegateAccountEntry(delegateAccountId, delegateEntry, rehydrator);
			};

			AccountIdGroupSerializer.Rehydrate(rehydrator, true, parameters);
		}

		protected virtual void RehydrateAccountEntry(AccountId accountId, IElectedResults entry, IDataRehydrator rehydrator) {
			entry.Rehydrate(rehydrator);
		}

		protected virtual void RehydrateDelegateAccountEntry(AccountId accountId, IDelegateResults entry, IDataRehydrator rehydrator) {
			entry.Rehydrate(rehydrator);
		}

		protected virtual IElectedResults CreateElectedResult(AccountId accountId) {
			return new ElectedResults();
		}
	}
}