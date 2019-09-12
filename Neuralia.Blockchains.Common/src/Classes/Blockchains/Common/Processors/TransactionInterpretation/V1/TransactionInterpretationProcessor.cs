using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public abstract partial class TransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshot<STANDARD_ACCOUNT_FEATURE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeature, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshot<JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeature, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccount, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshot, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccount, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshot, new() {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		private readonly ChainConfigurations.FastKeyTypes enabledFastKeyTypes;

		private readonly Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys;
		private readonly TransactionImpactSet.OperationModes operationMode;

		protected readonly Dictionary<Type, ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>> overriddenTransactionImpactSets = new Dictionary<Type, ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>>();

		protected readonly Dictionary<Type, ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>> transactionImpactSets = new Dictionary<Type, ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>>();
		private ImmutableList<AccountId> dispatchedAccounts;

		private bool isInitialized;

		private bool localMode;

		private ImmutableList<AccountId> publishedAccounts;

		protected SnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCacheSet;

		public TransactionInterpretationProcessor(CENTRAL_COORDINATOR centralCoordinator) : this(centralCoordinator, TransactionImpactSet.OperationModes.Real) {

		}

		public TransactionInterpretationProcessor(CENTRAL_COORDINATOR centralCoordinator, TransactionImpactSet.OperationModes operationMode) {

			this.operationMode = operationMode;
			this.centralCoordinator = centralCoordinator;

			this.fastKeys = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex ? new Dictionary<(AccountId accountId, byte ordinal), byte[]>() : null;
			this.enabledFastKeyTypes = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnabledFastKeyTypes;
		}

		public event Func<List<AccountId>, Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT>> RequestStandardAccountSnapshots;
		public event Func<List<AccountId>, Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT>> RequestJointAccountSnapshots;
		public event Func<List<(long AccountId, byte OrdinalId)>, Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT>> RequestStandardAccountKeySnapshots;
		public event Func<List<int>, Dictionary<int, ACCREDITATION_CERTIFICATE_SNAPSHOT>> RequestAccreditationCertificateSnapshots;
		public event Func<List<int>, Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>> RequestChainOptionSnapshots;

		public event Func<STANDARD_ACCOUNT_SNAPSHOT> RequestCreateNewStandardAccountSnapshot;
		public event Func<JOINT_ACCOUNT_SNAPSHOT> RequestCreateNewJointAccountSnapshot;
		public event Func<STANDARD_ACCOUNT_KEY_SNAPSHOT> RequestCreateNewAccountKeySnapshot;
		public event Func<ACCREDITATION_CERTIFICATE_SNAPSHOT> RequestCreateNewAccreditationCertificateSnapshot;
		public event Func<CHAIN_OPTIONS_SNAPSHOT> RequestCreateNewChainOptionSnapshot;

		public event Action<TransactionId, RejectionCode> TransactionRejected;

		public Func<List<AccountId>, bool> IsAnyAccountTracked { get; set; } = ids => false;
		public Func<List<AccountId>, List<AccountId>> GetTrackedAccounts { get; set; } = ids => new List<AccountId>();

		public Func<List<(long AccountId, byte OrdinalId)>, List<AccountId>, bool> IsAnyAccountKeysTracked { get; set; } = (ids, accounts) => false;
		public Func<List<int>, bool> IsAnyAccreditationCertificateTracked { get; set; } = ids => false;
		public Func<List<int>, bool> IsAnyChainOptionTracked { get; set; } = ids => false;
		public Action<bool, List<AccountId>, List<AccountId>, ITransaction> AccountInfluencingTransactionFound { get; set; } = null;

		public void Initialize() {

			if(!this.isInitialized) {

				this.RegisterTransactionImpactSets();

				// lets connect all our events
				foreach(var transactionImpactSet in this.transactionImpactSets.Values) {
					// since the this functions can change over time, we need to wrap them into calling functions. we can not '=' them directly unless we did a rebind.
					transactionImpactSet.IsAnyAccountTracked = ids => this.IsAnyAccountTracked?.Invoke(ids) ?? false;
					transactionImpactSet.GetTrackedAccounts = ids => this.GetTrackedAccounts?.Invoke(ids) ?? new List<AccountId>();

					transactionImpactSet.IsAnyAccountKeysTracked = (ids, accounts) => this.IsAnyAccountKeysTracked?.Invoke(ids, accounts) ?? false;
					transactionImpactSet.IsAnyAccreditationCertificateTracked = ids => this.IsAnyAccreditationCertificateTracked?.Invoke(ids) ?? false;
					transactionImpactSet.IsAnyChainOptionTracked = ids => this.IsAnyChainOptionTracked?.Invoke(ids) ?? false;
				}

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

		public SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> GetEntriesModificationStack() {
			return this.snapshotCacheSet.GetEntriesModificationStack();
		}

		public Dictionary<(AccountId accountId, byte ordinal), byte[]> GetImpactedFastKeys() {
			return this.fastKeys;
		}

		public virtual void ApplyBlockElectionsInfluence(List<IFinalElectionResults> publicationResult, Dictionary<TransactionId, ITransaction> transactions) {
			this.Initialize();

			// first get all the impacted accounts
			SnapshotKeySet impactedSnapshotKeys = new SnapshotKeySet();

			foreach(IFinalElectionResults finalElectionResult in publicationResult) {

				impactedSnapshotKeys.AddAccounts(finalElectionResult.DelegateAccounts.Select(a => a.Key).ToList());
				impactedSnapshotKeys.AddAccounts(finalElectionResult.ElectedCandidates.Select(a => a.Key).ToList());
			}

			impactedSnapshotKeys.Distinct();

			// now, we can query the snapshots we will need
			this.snapshotCacheSet.EnsureSnapshots(impactedSnapshotKeys);

			foreach(IFinalElectionResults finalElectionResult in publicationResult) {

				var trackedDelegateAccounts = this.GetTrackedAccounts(finalElectionResult.DelegateAccounts.Keys.ToList());

				foreach(var entry in finalElectionResult.DelegateAccounts.Where(a => trackedDelegateAccounts.Contains(a.Key))) {

					ACCOUNT_SNAPSHOT snapshot = this.snapshotCacheSet.GetAccountSnapshotModify(entry.Key);

					if(snapshot != null) {
						this.ApplyDelegateResultsToSnapshot(snapshot, entry.Value, transactions);
					}
				}

				var trackedElectedAccounts = this.GetTrackedAccounts(finalElectionResult.ElectedCandidates.Keys.ToList());

				foreach(var entry in finalElectionResult.ElectedCandidates.Where(a => trackedElectedAccounts.Contains(a.Key))) {

					ACCOUNT_SNAPSHOT snapshot = this.snapshotCacheSet.GetAccountSnapshotModify(entry.Key);

					if(snapshot != null) {
						this.ApplyElectedResultsToSnapshot(snapshot, entry.Value, transactions);
					}
				}
			}
		}

		public virtual void ApplyBlockElectionsInfluence(List<SynthesizedBlock.SynthesizedElectionResult> finalElectionResults, Dictionary<TransactionId, ITransaction> transactions) {
			this.Initialize();

			// first get all the impacted accounts
			SnapshotKeySet impactedSnapshotKeys = new SnapshotKeySet();

			foreach(SynthesizedBlock.SynthesizedElectionResult finalElectionResult in finalElectionResults) {

				impactedSnapshotKeys.AddAccounts(finalElectionResult.DelegateAccounts);
				impactedSnapshotKeys.AddAccounts(finalElectionResult.ElectedAccounts.Keys.ToList());
			}

			impactedSnapshotKeys.Distinct();

			// now, we can query the snapshots we will need
			this.snapshotCacheSet.EnsureSnapshots(impactedSnapshotKeys);

			foreach(SynthesizedBlock.SynthesizedElectionResult finalElectionResult in finalElectionResults) {

				var trackedDelegateAccounts = this.GetTrackedAccounts(finalElectionResult.DelegateAccounts);

				foreach(AccountId entry in finalElectionResult.DelegateAccounts.Where(a => trackedDelegateAccounts.Contains(a))) {

					ACCOUNT_SNAPSHOT snapshot = this.snapshotCacheSet.GetAccountSnapshotModify(entry);

					if(snapshot != null) {

						this.ApplyDelegateResultsToSnapshot(snapshot, entry, finalElectionResult, transactions);
					}
				}

				var trackedElectedAccounts = this.GetTrackedAccounts(finalElectionResult.ElectedAccounts.Keys.ToList());

				foreach(AccountId entry in finalElectionResult.ElectedAccounts.Keys.Where(a => trackedElectedAccounts.Contains(a))) {

					ACCOUNT_SNAPSHOT snapshot = this.snapshotCacheSet.GetAccountSnapshotModify(entry);

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

		public virtual void InterpretTransactions(List<ITransaction> transactions, long blockId, Action<int> step = null) {

			this.Initialize();

			this.PrepareImpactedSnapshotsList(transactions);

			int index = 1;

			foreach(ITransaction transaction in transactions) {

				SnapshotKeySet impactedSnapshots = this.GetTransactionImpactedSnapshots(transaction);
				bool isLocal = false;
				bool isDispatched = false;

				if(this.localMode) {
					// determine if it is a local account matching and if yes, if it is a dispatched one
					isDispatched = impactedSnapshots.standardAccounts.Any(a => this.dispatchedAccounts?.Contains(a) ?? false) || impactedSnapshots.jointAccounts.Any(a => this.dispatchedAccounts?.Contains(a) ?? false);
					isLocal = isDispatched || impactedSnapshots.standardAccounts.Any(a => this.publishedAccounts?.Contains(a) ?? false) || impactedSnapshots.jointAccounts.Any(a => this.publishedAccounts?.Contains(a) ?? false);

					if(isLocal || isDispatched) {

						var impactedLocalDispatchedAccounts = impactedSnapshots.standardAccounts.Where(a => this.dispatchedAccounts?.Contains(a) ?? false).ToList();
						impactedLocalDispatchedAccounts.AddRange(impactedSnapshots.jointAccounts.Where(a => this.dispatchedAccounts?.Contains(a) ?? false));

						var impactedLocalPublishedAccounts = impactedSnapshots.standardAccounts.Where(a => isDispatched || (this.publishedAccounts?.Contains(a) ?? false)).ToList();
						impactedLocalPublishedAccounts.AddRange(impactedSnapshots.jointAccounts.Where(a => isDispatched || (this.publishedAccounts?.Contains(a) ?? false)));

						// determine if it is our own transaction, or if it is foreign
						bool isOwn = impactedLocalPublishedAccounts.Contains(transaction.TransactionId.Account);

						// this transaction concerns us, lets alert.
						this.AccountInfluencingTransactionFound?.Invoke(isOwn, impactedLocalPublishedAccounts, impactedLocalDispatchedAccounts, transaction);
					}
				}

				foreach(var entry in this.transactionImpactSets) {
					entry.Value.InterpretTransaction(transaction, blockId, impactedSnapshots, this.fastKeys, this.enabledFastKeyTypes, this.operationMode, this.snapshotCacheSet, isLocal, isDispatched, this.TransactionRejected);
				}

				step?.Invoke(index);
				index++;
			}
		}

		/// <summary>
		///     this method will extract all transactions that affect our accounts and split them in the ones we created, and the
		///     ones that target us
		/// </summary>
		/// <param name="transactions"></param>
		/// <returns></returns>
		public (List<ITransaction> impactingLocals, List<(ITransaction transaction, AccountId targetAccount)> impactingExternals, Dictionary<AccountId, List<TransactionId>> accountsTransactions) GetImpactingTransactionsList(List<ITransaction> transactions) {

			this.Initialize();

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
				if(this.IsAnyAccountTracked(search)) {
					// this is one of ours
					if(!impactingLocals.ContainsKey(transaction.TransactionId.SimpleTransactionId)) {
						impactingLocals.Add(transaction.TransactionId.SimpleTransactionId, transaction);
					}

					AddTranaction(transaction.TransactionId.Account, transaction.TransactionId.SimpleTransactionId);
				}

				// we still need to check the target, since we may send transactions between accounts that we own

				SnapshotKeySet snapshots = this.RunTransactionImpactSet(transaction, impactSet => impactSet.GetImpactedSnapshots(transaction));

				var trackedAccounts = this.GetTrackedAccounts(snapshots.AllAccounts);

				foreach(AccountId account in trackedAccounts) {
					// ok, this transaction impacts us. lets see if its send by us, or not

					if(account == transaction.TransactionId.Account) {
						if(!impactingLocals.ContainsKey(transaction.TransactionId.SimpleTransactionId)) {
							impactingLocals.Add(transaction.TransactionId.SimpleTransactionId, transaction);
						}
					} else {
						if(!impactingExternals.ContainsKey(transaction.TransactionId.SimpleTransactionId)) {
							impactingExternals.Add(transaction.TransactionId.SimpleTransactionId, (transaction, account));
						}
					}

					// now all the accounts targetted by this transaction
					foreach(AccountId subaccount in trackedAccounts) {
						AddTranaction(subaccount, transaction.TransactionId.SimpleTransactionId);
					}
				}
			}

			return (impactingLocals.Values.ToList(), impactingExternals.Values.ToList(), accountsTransactions);
		}

		public void ModifyTransactionImpact<T>(Action<ITransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>> action)
			where T : ITransaction {

			Type type = typeof(T);

			ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> transactionImpactSet = null;

			if(this.transactionImpactSets.ContainsKey(type)) {
				transactionImpactSet = this.transactionImpactSets[type];
			}

			// search for the base class
			if((transactionImpactSet == null) && this.overriddenTransactionImpactSets.ContainsKey(type)) {
				transactionImpactSet = this.overriddenTransactionImpactSets[type];
			}

			if(transactionImpactSet != null) {
				action((ITransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>) transactionImpactSet);
			}
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
		public SnapshotKeySet GetTransactionImpactedSnapshots(ITransaction transaction) {
			SnapshotKeySet impactedSnapshots = new SnapshotKeySet();
			impactedSnapshots.Add(this.RunTransactionImpactSet(transaction, impactSet => impactSet.GetImpactedSnapshots(transaction)));

			impactedSnapshots.Distinct();

			return impactedSnapshots;
		}

		public SnapshotKeySet GetImpactedSnapshotsList(List<ITransaction> transactions) {

			this.Initialize();

			SnapshotKeySet impactedSnapshots = new SnapshotKeySet();

			foreach(ITransaction transaction in transactions) {
				impactedSnapshots.Add(this.RunTransactionImpactSet(transaction, impactSet => impactSet.GetImpactedSnapshots(transaction)));
			}

			impactedSnapshots.Distinct();

			return impactedSnapshots;
		}

		protected void PrepareImpactedSnapshotsList(List<ITransaction> transactions) {

			this.Initialize();

			SnapshotKeySet impactedSnapshots = this.GetImpactedSnapshotsList(transactions);

			// now, we can query the snapshots we will need
			this.snapshotCacheSet.EnsureSnapshots(impactedSnapshots);
		}

		protected SnapshotKeySet RunTransactionImpactSet(ITransaction transaction, Func<ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>, SnapshotKeySet> action) {

			SnapshotKeySet results = new SnapshotKeySet();

			foreach(var entry in this.transactionImpactSets) {
				results.Add(action(entry.Value));
			}

			return results;
		}

		protected void RegisterTransactionImpactSet(ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> transactionImpactSet) {

			this.transactionImpactSets.Add(transactionImpactSet.ActingType, transactionImpactSet);
		}

		/// <summary>
		///     override the base class behavior and wrap it ina subclass
		/// </summary>
		/// <param name="transactionImpactSet"></param>
		/// <typeparam name="T_PARENT"></typeparam>
		protected void RegisterTransactionImpactSetOverride(ISupersetTransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> transactionImpactSet) {

			Type parentType = transactionImpactSet.ParentActingType;

			// wrap the parent
			if(this.transactionImpactSets.ContainsKey(parentType)) {
				transactionImpactSet.Parent = this.transactionImpactSets[parentType];
				this.transactionImpactSets.Remove(parentType);
				this.overriddenTransactionImpactSets.Add(parentType, transactionImpactSet.Parent);

			}

			transactionImpactSet.RegisterOverrides();
			this.transactionImpactSets.Add(transactionImpactSet.ActingType, transactionImpactSet);
		}

		protected byte[] Dehydratekey(ICryptographicKey key) {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			key.Dehydrate(dehydrator);
			IByteArray bytes = dehydrator.ToArray();
			var result = bytes.ToExactByteArrayCopy();
			bytes.Return();

			return result;
		}

		protected class TransactionImpactSet<T> : TransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
			where T : ITransaction {
		}

		//TODO: improve error handling in this class
		protected class SupersetTransactionImpactSet<T, T_PARENT> : SupersetTransactionImpactSet<T, T_PARENT, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
			where T : ITransaction, T_PARENT
			where T_PARENT : ITransaction {
		}
	}
}