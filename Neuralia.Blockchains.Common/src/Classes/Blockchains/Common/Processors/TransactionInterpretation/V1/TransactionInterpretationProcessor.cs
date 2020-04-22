using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public abstract partial class TransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshot<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshot<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccount, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshot, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccount, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshot, new() {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		private readonly ChainConfigurations.FastKeyTypes enabledFastKeyTypes;

		private readonly Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys;
		private readonly TransactionImpactSet.OperationModes operationMode;
		
		private ImmutableList<AccountId> dispatchedAccounts;

		private bool isInitialized;

		private bool localMode;

		private ImmutableList<AccountId> publishedAccounts;

		protected SnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCacheSet;
		protected  IAccreditationCertificateProvider AccreditationCertificateProvider => ( IAccreditationCertificateProvider)this.centralCoordinator.ChainComponentProvider.AccreditationCertificateProviderBase;
		protected  IAccountSnapshotsProvider AccountSnapshotsProvider => this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase;

		public TransactionInterpretationProcessor(CENTRAL_COORDINATOR centralCoordinator) : this(centralCoordinator, TransactionImpactSet.OperationModes.Real) {

		}

		public TransactionInterpretationProcessor(CENTRAL_COORDINATOR centralCoordinator, TransactionImpactSet.OperationModes operationMode) {

			this.operationMode = operationMode;
			this.centralCoordinator = centralCoordinator;

			this.TransactionImpactSets = new TransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>(this.centralCoordinator.ChainId, this.centralCoordinator.ChainName);

			this.fastKeys = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex ? new Dictionary<(AccountId accountId, byte ordinal), byte[]>() : null;
			this.enabledFastKeyTypes = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnabledFastKeyTypes;
		}

		public event Func<List<AccountId>, LockContext, Task<Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT>>> RequestStandardAccountSnapshots;
		public event Func<List<AccountId>, LockContext,  Task<Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT>>> RequestJointAccountSnapshots;
		public event Func<List<(long AccountId, byte OrdinalId)>, LockContext,  Task<Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT>>> RequestStandardAccountKeySnapshots;
		public event Func<List<int>, LockContext,  Task<Dictionary<int, ACCREDITATION_CERTIFICATE_SNAPSHOT>>> RequestAccreditationCertificateSnapshots;
		public event Func<List<int>, LockContext,  Task<Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>>> RequestChainOptionSnapshots;

		public event Func<LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> RequestCreateNewStandardAccountSnapshot;
		public event Func<LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> RequestCreateNewJointAccountSnapshot;
		public event Func<LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> RequestCreateNewAccountKeySnapshot;
		public event Func<LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> RequestCreateNewAccreditationCertificateSnapshot;
		public event Func<LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> RequestCreateNewChainOptionSnapshot;

		public event Action<TransactionId, RejectionCode> TransactionRejected;

		public Func<List<AccountId>, Task<bool>> IsAnyAccountTracked { get; set; } = async ids => false;
		public Func<List<AccountId>, Task<List<AccountId>>> GetTrackedAccounts { get; set; } = async ids => new List<AccountId>();

		public Func<List<(long AccountId, byte OrdinalId)>, List<AccountId>, Task<bool>> IsAnyAccountKeysTracked { get; set; } = async (ids, accounts) => false;
		public Func<List<int>, Task<bool>> IsAnyAccreditationCertificateTracked { get; set; } = async ids => false;
		public Func<List<int>, Task<bool>> IsAnyChainOptionTracked { get; set; } = async ids => false;
		public Func<bool, List<AccountId>, List<AccountId>, ITransaction, LockContext, Task> AccountInfluencingTransactionFound { get; set; } = null;

		public async Task Initialize() {

			if(!this.isInitialized) {

				await this.RegisterTransactionImpactSets().ConfigureAwait(false);

				// lets connect all our events

				// since the this functions can change over time, we need to wrap them into calling functions. we can not '=' them directly unless we did a rebind.
				this.TransactionImpactSets.IsAnyAccountTracked = async ids => {

					if(this.IsAnyAccountTracked == null) {
						return false;
					}
					return await this.IsAnyAccountTracked(ids).ConfigureAwait(false);
				};
				this.TransactionImpactSets.GetTrackedAccounts = async ids => {

					if(this.GetTrackedAccounts == null) {
						return new List<AccountId>();
					}
					return await this.GetTrackedAccounts(ids).ConfigureAwait(false);
				};

				this.TransactionImpactSets.IsAnyAccountKeysTracked = async (ids, accounts) => {
					if(this.IsAnyAccountKeysTracked == null) {
						return false;
					}
					return await this.IsAnyAccountKeysTracked(ids, accounts).ConfigureAwait(false);
				};
				this.TransactionImpactSets.IsAnyAccreditationCertificateTracked = async ids => {
					if(this.IsAnyAccreditationCertificateTracked == null) {
						return false;
					}
					return await this.IsAnyAccreditationCertificateTracked(ids).ConfigureAwait(false);
				};
				this.TransactionImpactSets.IsAnyChainOptionTracked = async ids => {
					if(this.IsAnyChainOptionTracked == null) {
						return false;
					}
					return await this.IsAnyChainOptionTracked(ids).ConfigureAwait(false);
				};
				

				this.snapshotCacheSet.RequestStandardAccountSnapshots += this.RequestStandardAccountSnapshots;
				this.snapshotCacheSet.RequestJointAccountSnapshots += this.RequestJointAccountSnapshots;
				this.snapshotCacheSet.RequestAccountKeySnapshots += this.RequestStandardAccountKeySnapshots;
				this.snapshotCacheSet.RequestAccreditationCertificateSnapshots += this.RequestAccreditationCertificateSnapshots;
				this.snapshotCacheSet.RequestChainOptionSnapshots += this.RequestChainOptionSnapshots;

				this.snapshotCacheSet.RequestCreateNewStandardAccountSnapshot += this.RequestCreateNewStandardAccountSnapshot;
				this.snapshotCacheSet.RequestCreateNewJointAccountSnapshot += this.RequestCreateNewJointAccountSnapshot;
				this.snapshotCacheSet.RequestCreateNewAccountKeySnapshot += this.RequestCreateNewAccountKeySnapshot;
				this.snapshotCacheSet.RequestCreateNewAccreditationCertificateSnapshot += this.RequestCreateNewAccreditationCertificateSnapshot;
				this.snapshotCacheSet.RequestCreateNewChainOptionSnapshot += this.RequestCreateNewChainOptionSnapshot;

				this.snapshotCacheSet.Initialize();

				this.isInitialized = true;
			}
		}

		public SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> GetEntriesModificationStack() {
			return this.snapshotCacheSet.GetEntriesModificationStack();
		}

		public Dictionary<(AccountId accountId, byte ordinal), byte[]> GetImpactedFastKeys() {
			return this.fastKeys;
		}

		public virtual async Task ApplyBlockElectionsInfluence(List<IFinalElectionResults> publicationResult, Dictionary<TransactionId, ITransaction> transactions, LockContext lockContext) {
			await this.Initialize().ConfigureAwait(false);

			// first get all the impacted accounts
			SnapshotKeySet impactedSnapshotKeys = new SnapshotKeySet();

			foreach(IFinalElectionResults finalElectionResult in publicationResult) {

				impactedSnapshotKeys.AddAccounts(finalElectionResult.DelegateAccounts.Select(a => a.Key).ToList());
				impactedSnapshotKeys.AddAccounts(finalElectionResult.ElectedCandidates.Select(a => a.Key).ToList());
			}

			impactedSnapshotKeys.Distinct();

			// now, we can query the snapshots we will need
			await this.snapshotCacheSet.EnsureSnapshots(impactedSnapshotKeys, lockContext).ConfigureAwait(false);

			foreach(IFinalElectionResults finalElectionResult in publicationResult) {

				var trackedDelegateAccounts = await this.GetTrackedAccounts(finalElectionResult.DelegateAccounts.Keys.ToList()).ConfigureAwait(false);

				foreach(var entry in finalElectionResult.DelegateAccounts.Where(a => trackedDelegateAccounts.Contains(a.Key))) {

					ACCOUNT_SNAPSHOT snapshot = await this.snapshotCacheSet.GetAccountSnapshotModify(entry.Key, lockContext).ConfigureAwait(false);

					if(snapshot != null) {
						this.ApplyDelegateResultsToSnapshot(snapshot, entry.Value, transactions);
					}
				}

				var trackedElectedAccounts = await this.GetTrackedAccounts(finalElectionResult.ElectedCandidates.Keys.ToList()).ConfigureAwait(false);

				foreach((AccountId key, IElectedResults value) in finalElectionResult.ElectedCandidates.Where(a => trackedElectedAccounts.Contains(a.Key))) {

					ACCOUNT_SNAPSHOT snapshot = await this.snapshotCacheSet.GetAccountSnapshotModify(key, lockContext).ConfigureAwait(false);

					if(snapshot != null) {
						this.ApplyElectedResultsToSnapshot(snapshot, value, transactions);
					}
				}
			}
		}

		public virtual async Task ApplyBlockElectionsInfluence(List<SynthesizedBlock.SynthesizedElectionResult> finalElectionResults, Dictionary<TransactionId, ITransaction> transactions, LockContext lockContext) {
			await this.Initialize().ConfigureAwait(false);

			// first get all the impacted accounts
			SnapshotKeySet impactedSnapshotKeys = new SnapshotKeySet();

			foreach(SynthesizedBlock.SynthesizedElectionResult finalElectionResult in finalElectionResults) {

				impactedSnapshotKeys.AddAccounts(finalElectionResult.DelegateAccounts);
				impactedSnapshotKeys.AddAccounts(finalElectionResult.ElectedAccounts.Keys.ToList());
			}

			impactedSnapshotKeys.Distinct();

			// now, we can query the snapshots we will need
			await this.snapshotCacheSet.EnsureSnapshots(impactedSnapshotKeys, lockContext).ConfigureAwait(false);

			foreach(SynthesizedBlock.SynthesizedElectionResult finalElectionResult in finalElectionResults) {

				var trackedDelegateAccounts = await this.GetTrackedAccounts(finalElectionResult.DelegateAccounts).ConfigureAwait(false);

				foreach(AccountId entry in finalElectionResult.DelegateAccounts.Where(a => trackedDelegateAccounts.Contains(a))) {

					ACCOUNT_SNAPSHOT snapshot = await this.snapshotCacheSet.GetAccountSnapshotModify(entry, lockContext).ConfigureAwait(false);

					if(snapshot != null) {

						this.ApplyDelegateResultsToSnapshot(snapshot, entry, finalElectionResult, transactions);
					}
				}

				var trackedElectedAccounts = await this.GetTrackedAccounts(finalElectionResult.ElectedAccounts.Keys.ToList()).ConfigureAwait(false);

				foreach(AccountId entry in finalElectionResult.ElectedAccounts.Keys.Where(a => trackedElectedAccounts.Contains(a))) {

					ACCOUNT_SNAPSHOT snapshot = await this.snapshotCacheSet.GetAccountSnapshotModify(entry, lockContext).ConfigureAwait(false);

					if(snapshot != null) {
						this.ApplyElectedResultsToSnapshot(snapshot, entry, finalElectionResult, transactions);
					}
				}
			}
		}

		public void EnableLocalMode(bool value) {
			this.localMode = value;
		}

		public void Reset() {
			this.snapshotCacheSet.Reset();
		}

		public void SetLocalAccounts(ImmutableList<AccountId> publishedAccounts, ImmutableList<AccountId> dispatchedAccounts) {
			this.publishedAccounts = publishedAccounts;
			this.dispatchedAccounts = dispatchedAccounts;
		}

		public void SetLocalAccounts(ImmutableList<AccountId> publishedAccounts) {
			this.publishedAccounts = publishedAccounts;
			this.dispatchedAccounts = null;
		}

		public void ClearLocalAccounts() {
			this.publishedAccounts = null;
			this.dispatchedAccounts = null;
		}

		private async Task InterpretTransaction(ITransaction transaction, long blockId, LockContext lockContext) {
			SnapshotKeySet impactedSnapshots = await this.GetTransactionImpactedSnapshots(transaction, lockContext).ConfigureAwait(false);
				bool isLocal = false;
				bool isDispatched = false;
				AccountId accounthash = null;

				if(this.localMode) {

					// if we are in local mode, then we ensure we treat presentation transactions specially, they are special. we add the accountId hash.
					if(transaction is IPresentationTransaction presentationTransaction) {
						accounthash = presentationTransaction.TransactionId.Account;
						isDispatched = this.dispatchedAccounts?.Contains(accounthash) ?? false;
					}

					// determine if it is a local account matching and if yes, if it is a dispatched one
					isLocal = isDispatched || impactedSnapshots.standardAccounts.Any(a => this.publishedAccounts?.Contains(a) ?? false) || impactedSnapshots.jointAccounts.Any(a => this.publishedAccounts?.Contains(a) ?? false);

					if(isLocal) {

						var impactedLocalDispatchedAccounts = new List<AccountId>();

						if(accounthash != null) {
							impactedLocalDispatchedAccounts.Add(accounthash);
						}

						var impactedLocalPublishedAccounts = impactedSnapshots.standardAccounts.Where(a => this.publishedAccounts?.Contains(a) ?? false).ToList();
						impactedLocalPublishedAccounts.AddRange(impactedSnapshots.jointAccounts.Where(a => this.publishedAccounts?.Contains(a) ?? false));

						// determine if it is our own transaction, or if it is foreign
						bool isOwn = impactedLocalPublishedAccounts.Contains(transaction.TransactionId.Account) || impactedLocalDispatchedAccounts.Contains(transaction.TransactionId.Account);

						// this transaction concerns us, lets alert.
						if(this.AccountInfluencingTransactionFound != null) {
							await AccountInfluencingTransactionFound(isOwn, impactedLocalPublishedAccounts, impactedLocalDispatchedAccounts, transaction, lockContext).ConfigureAwait(false);
						}
					}
				}

				await this.TransactionImpactSets.InterpretTransaction(transaction, blockId, impactedSnapshots, this.fastKeys, this.enabledFastKeyTypes, this.operationMode, this.snapshotCacheSet, isLocal, isDispatched, this.TransactionRejected, lockContext).ConfigureAwait(false);

		}
		public virtual async Task InterpretTransactionStream(List<ITransaction> transactions, long blockId, LockContext lockContext, Action<int> step = null) {
			
			await this.PrepareImpactedSnapshotsList(transactions, lockContext).ConfigureAwait(false);

			int index = 1;

			foreach(ITransaction transaction in transactions) {

				await this.InterpretTransaction(transaction,blockId, lockContext).ConfigureAwait(false);
if(				step != null){	step(index);}
				index++;
			}

		}
		
		public virtual async Task InterpretTransactions(List<ITransaction> transactions, long blockId, LockContext lockContext, Action<int> step = null) {

			await this.Initialize().ConfigureAwait(false);

			await this.InterpretTransactionStream(transactions, blockId, lockContext, step).ConfigureAwait(false);
		}

		/// <summary>
		///     this method will extract all transactions that affect our accounts and split them in the ones we created, and the
		///     ones that target us
		/// </summary>
		/// <param name="transactions"></param>
		/// <returns></returns>
		public async Task<(List<ITransaction> impactingLocals, List<(ITransaction transaction, AccountId targetAccount)> impactingExternals, Dictionary<AccountId, List<TransactionId>> accountsTransactions)> GetImpactingTransactionsList(List<ITransaction> transactions, LockContext lockContext) {

			await this.Initialize().ConfigureAwait(false);

			// the ones we did not create but target us one way or another
			var impactingExternals = new Dictionary<TransactionId, (ITransaction transaction, AccountId targetAccount)>();

			// the ones we created
			var impactingLocals = new Dictionary<TransactionId, ITransaction>();

			// here we group them by impacted account
			var accountsTransactions = new Dictionary<AccountId, List<TransactionId>>();

			void AddTranaction(AccountId account, TransactionId transactionId) {
				if(!accountsTransactions.ContainsKey(account)) {
					accountsTransactions.Add(account, new List<TransactionId>());
				}

				accountsTransactions[account].Add(transactionId);
			}

			foreach(ITransaction transaction in transactions) {

				var search = new[] {transaction.TransactionId.Account}.ToList();

				// if we track the transaction source account, then we add it
				if(await this.IsAnyAccountTracked(search).ConfigureAwait(false)) {
					// this is one of ours
					if(!impactingLocals.ContainsKey(transaction.TransactionId)) {
						impactingLocals.Add(transaction.TransactionId, transaction);
					}

					AddTranaction(transaction.TransactionId.Account, transaction.TransactionId);
				}

				// we still need to check the target, since we may send transactions between accounts that we own

				SnapshotKeySet snapshots = await this.RunTransactionImpactSet(transaction, lockContext).ConfigureAwait(false);

				var trackedAccounts = await this.GetTrackedAccounts(snapshots.AllAccounts).ConfigureAwait(false);

				foreach(AccountId account in trackedAccounts) {
					// ok, this transaction impacts us. lets see if its send by us, or not

					if(account == transaction.TransactionId.Account) {
						if(!impactingLocals.ContainsKey(transaction.TransactionId)) {
							impactingLocals.Add(transaction.TransactionId, transaction);
						}
					} else {
						if(!impactingExternals.ContainsKey(transaction.TransactionId)) {
							impactingExternals.Add(transaction.TransactionId, (transaction, account));
						}
					}

					// now all the accounts targetted by this transaction
					foreach(AccountId subaccount in trackedAccounts) {
						AddTranaction(subaccount, transaction.TransactionId);
					}
				}
			}

			return (impactingLocals.Values.ToList(), impactingExternals.Values.ToList(), accountsTransactions);
		}
		
		protected virtual void ApplyDelegateResultsToSnapshot(ACCOUNT_SNAPSHOT snapshot, IDelegateResults delegateResults, Dictionary<TransactionId, ITransaction> transactions) {

		}

		protected virtual void ApplyElectedResultsToSnapshot(ACCOUNT_SNAPSHOT snapshot, IElectedResults electedResults, Dictionary<TransactionId, ITransaction> transactions) {

		}

		protected virtual void ApplyDelegateResultsToSnapshot(ACCOUNT_SNAPSHOT snapshot, AccountId accountId, SynthesizedBlock.SynthesizedElectionResult synthesizedElectionResult, Dictionary<TransactionId, ITransaction> transactions) {

		}

		protected virtual void ApplyElectedResultsToSnapshot(ACCOUNT_SNAPSHOT snapshot, AccountId accountId, SynthesizedBlock.SynthesizedElectionResult synthesizedElectionResult, Dictionary<TransactionId, ITransaction> transactions) {

		}

		/// <summary>
		///     This method tells us all accounts impacted by this transaction
		/// </summary>
		/// <param name="transaction"></param>
		/// <returns></returns>
		public async Task<SnapshotKeySet> GetTransactionImpactedSnapshots(ITransaction transaction, LockContext lockContext) {
			SnapshotKeySet impactedSnapshots = new SnapshotKeySet();
			impactedSnapshots.Add(await this.RunTransactionImpactSet(transaction, lockContext).ConfigureAwait(false));

			impactedSnapshots.Distinct();

			return impactedSnapshots;
		}

		public async Task<SnapshotKeySet> GetImpactedSnapshotsList(List<ITransaction> transactions, LockContext lockContext) {

			await this.Initialize().ConfigureAwait(false);

			SnapshotKeySet impactedSnapshots = new SnapshotKeySet();

			foreach(ITransaction transaction in transactions) {
				impactedSnapshots.Add(await this.RunTransactionImpactSet(transaction, lockContext).ConfigureAwait(false));
			}

			impactedSnapshots.Distinct();

			return impactedSnapshots;
		}

		protected async Task PrepareImpactedSnapshotsList(List<ITransaction> transactions, LockContext lockContext) {

			await this.Initialize().ConfigureAwait(false);

			SnapshotKeySet impactedSnapshots = await this.GetImpactedSnapshotsList(transactions, lockContext).ConfigureAwait(false);

			// now, we can query the snapshots we will need
			await this.snapshotCacheSet.EnsureSnapshots(impactedSnapshots, lockContext).ConfigureAwait(false);
		}

		protected Task<SnapshotKeySet> RunTransactionImpactSet(ITransaction transaction, LockContext lockContext) {

			return this.TransactionImpactSets.GetImpactedSnapshots(transaction, lockContext);
		}

		public TransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> TransactionImpactSets { get; }
		
		
		protected byte[] Dehydratekey(ICryptographicKey key) {

			using SafeArrayHandle bytes = key.Dehydrate();
			return bytes.ToExactByteArrayCopy();
		}
	}
}