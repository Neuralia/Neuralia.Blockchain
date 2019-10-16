using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public interface IAccountsSnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshot<STANDARD_ACCOUNT_FEATURE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeature, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshot<JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeature, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccount, new() {

		ACCOUNT_SNAPSHOT GetAccountSnapshotReadonly(AccountId newAccountId);
		ACCOUNT_SNAPSHOT GetAccountSnapshotModify(AccountId newAccountId);

		T GetAccountSnapshotReadonly<T>(AccountId newAccountId)
			where T : class, IAccountSnapshot;

		T GetAccountSnapshotModify<T>(AccountId newAccountId)
			where T : class, IAccountSnapshot;

		STANDARD_ACCOUNT_SNAPSHOT CreateNewStandardAccountSnapshot(AccountId newAccountId, AccountId TemporaryAccountHash);
		bool CheckStandardAccountSnapshotExists(AccountId newAccountId);
		void DeleteAccountSnapshot(AccountId newAccountId);

		STANDARD_ACCOUNT_SNAPSHOT CreateLooseStandardAccountSnapshot(AccountId newAccountId);
		STANDARD_ACCOUNT_SNAPSHOT GetStandardAccountSnapshotReadonly(AccountId newAccountId);
		STANDARD_ACCOUNT_SNAPSHOT GetStandardAccountSnapshotModify(AccountId newAccountId);
		JOINT_ACCOUNT_SNAPSHOT CreateNewJointAccountSnapshot(AccountId newAccountId, AccountId TemporaryAccountHash);

		void DeleteStandardAccountSnapshot(AccountId newAccountId);

		JOINT_ACCOUNT_SNAPSHOT CreateLooseJointAccountSnapshot(AccountId newAccountId);
		JOINT_ACCOUNT_SNAPSHOT GetJointAccountSnapshotReadonly(AccountId newAccountId);
		JOINT_ACCOUNT_SNAPSHOT GetJointAccountSnapshotModify(AccountId newAccountId);
		bool CheckJointAccountSnapshotExists(AccountId newAccountId);
		void DeleteJointAccountSnapshot(AccountId newAccountId);
	}

	public interface IAccountkeysSnapshotCacheSet<STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshot {

		STANDARD_ACCOUNT_KEY_SNAPSHOT CreateNewAccountKeySnapshot((long AccountId, byte OrdinalId) key);
		STANDARD_ACCOUNT_KEY_SNAPSHOT CreateLooseAccountKeySnapshot((long AccountId, byte OrdinalId) key);
		STANDARD_ACCOUNT_KEY_SNAPSHOT GetAccountKeySnapshotReadonly((long AccountId, byte OrdinalId) key);
		STANDARD_ACCOUNT_KEY_SNAPSHOT GetAccountKeySnapshotModify((long AccountId, byte OrdinalId) key);
		bool CheckAccountKeySnapshotExists((long AccountId, byte OrdinalId) key);
		void DeleteJointAccountSnapshot((long AccountId, byte OrdinalId) key);
	}

	public interface IAccreditationCertificateSnapshotCacheSet<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccount {

		ACCREDITATION_CERTIFICATE_SNAPSHOT CreateNewAccreditationCertificateSnapshot(int id);
		ACCREDITATION_CERTIFICATE_SNAPSHOT CreateLooseAccreditationCertificateSnapshot(int id);
		ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificateSnapshotReadonly(int id);
		ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificateSnapshotModify(int id);
		bool CheckAccreditationCertificateSnapshotExists(int id);
		void DeleteAccreditationCertificateSnapshot(int id);
	}

	public interface IChainOptionsSnapshotCacheSet<CHAIN_OPTIONS_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshot {

		CHAIN_OPTIONS_SNAPSHOT CreateNewChainOptionsSnapshot(int id);
		CHAIN_OPTIONS_SNAPSHOT CreateLooseChainOptionsSnapshot(int id);
		CHAIN_OPTIONS_SNAPSHOT GetChainOptionsSnapshotReadonly(int id);
		CHAIN_OPTIONS_SNAPSHOT GetChainOptionsSnapshotModify(int id);
		bool CheckChainOptionsSnapshotExists(int id);
		void DeleteChainOptionsSnapshot(int id);
	}

	public interface ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : IAccountsSnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, IAccountkeysSnapshotCacheSet<STANDARD_ACCOUNT_KEY_SNAPSHOT>, IAccreditationCertificateSnapshotCacheSet<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, IChainOptionsSnapshotCacheSet<CHAIN_OPTIONS_SNAPSHOT>
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

		event Func<STANDARD_ACCOUNT_SNAPSHOT> RequestCreateNewStandardAccountSnapshot;
		event Func<JOINT_ACCOUNT_SNAPSHOT> RequestCreateNewJointAccountSnapshot;
		event Func<STANDARD_ACCOUNT_KEY_SNAPSHOT> RequestCreateNewAccountKeySnapshot;
		event Func<ACCREDITATION_CERTIFICATE_SNAPSHOT> RequestCreateNewAccreditationCertificateSnapshot;
		event Func<CHAIN_OPTIONS_SNAPSHOT> RequestCreateNewChainOptionSnapshot;
		event Func<List<AccountId>, Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT>> RequestStandardAccountSnapshots;
		event Func<List<AccountId>, Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT>> RequestJointAccountSnapshots;
		event Func<List<(long AccountId, byte OrdinalId)>, Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT>> RequestAccountKeySnapshots;
		event Func<List<int>, Dictionary<int, ACCREDITATION_CERTIFICATE_SNAPSHOT>> RequestAccreditationCertificateSnapshots;
		event Func<List<int>, Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>> RequestChainOptionSnapshots;
		void Initialize();
		void Reset();
		void BeginTransaction();
		void CommitTransaction();
		void RollbackTransaction();
		void EnsureSnapshots(SnapshotKeySet snapshotKeySet);
	}

	public interface ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
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
	}

	public static class SnapshotCacheSet {
	}

	public class SnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
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
		protected readonly SnapshotCache<STANDARD_ACCOUNT_KEY_SNAPSHOT, (long AccountId, byte OrdinalId)> accountKeySnapshotCache;
		protected readonly SnapshotCache<ACCREDITATION_CERTIFICATE_SNAPSHOT, int> accreditationCertificateSnapshotCache;
		protected readonly SnapshotCache<CHAIN_OPTIONS_SNAPSHOT, int> chainOptionsSnapshotCache;
		protected readonly SnapshotCache<JOINT_ACCOUNT_SNAPSHOT, AccountId> jointAccountSnapshotCache;

		protected readonly SnapshotCache<STANDARD_ACCOUNT_SNAPSHOT, AccountId> simpleAccountSnapshotCache;

		private bool initialized;

		public SnapshotCacheSet(ICardUtils cardUtils) {
			this.simpleAccountSnapshotCache = new SnapshotCache<STANDARD_ACCOUNT_SNAPSHOT, AccountId>(cardUtils);
			this.jointAccountSnapshotCache = new SnapshotCache<JOINT_ACCOUNT_SNAPSHOT, AccountId>(cardUtils);
			this.accountKeySnapshotCache = new SnapshotCache<STANDARD_ACCOUNT_KEY_SNAPSHOT, (long AccountId, byte OrdinalId)>(cardUtils);
			this.accreditationCertificateSnapshotCache = new SnapshotCache<ACCREDITATION_CERTIFICATE_SNAPSHOT, int>(cardUtils);
			this.chainOptionsSnapshotCache = new SnapshotCache<CHAIN_OPTIONS_SNAPSHOT, int>(cardUtils);
		}

		public event Func<STANDARD_ACCOUNT_SNAPSHOT> RequestCreateNewStandardAccountSnapshot;
		public event Func<JOINT_ACCOUNT_SNAPSHOT> RequestCreateNewJointAccountSnapshot;
		public event Func<STANDARD_ACCOUNT_KEY_SNAPSHOT> RequestCreateNewAccountKeySnapshot;
		public event Func<ACCREDITATION_CERTIFICATE_SNAPSHOT> RequestCreateNewAccreditationCertificateSnapshot;
		public event Func<CHAIN_OPTIONS_SNAPSHOT> RequestCreateNewChainOptionSnapshot;

		public event Func<List<AccountId>, Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT>> RequestStandardAccountSnapshots;
		public event Func<List<AccountId>, Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT>> RequestJointAccountSnapshots;
		public event Func<List<(long AccountId, byte OrdinalId)>, Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT>> RequestAccountKeySnapshots;
		public event Func<List<int>, Dictionary<int, ACCREDITATION_CERTIFICATE_SNAPSHOT>> RequestAccreditationCertificateSnapshots;
		public event Func<List<int>, Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>> RequestChainOptionSnapshots;

		public void Initialize() {

			if(!this.initialized) {
				this.simpleAccountSnapshotCache.RequestSnapshots += this.RequestStandardAccountSnapshots;
				this.jointAccountSnapshotCache.RequestSnapshots += this.RequestJointAccountSnapshots;
				this.accountKeySnapshotCache.RequestSnapshots += this.RequestAccountKeySnapshots;
				this.accreditationCertificateSnapshotCache.RequestSnapshots += this.RequestAccreditationCertificateSnapshots;
				this.chainOptionsSnapshotCache.RequestSnapshots += this.RequestChainOptionSnapshots;

				this.simpleAccountSnapshotCache.CreateSnapshot += this.RequestCreateNewStandardAccountSnapshot;
				this.jointAccountSnapshotCache.CreateSnapshot += this.RequestCreateNewJointAccountSnapshot;
				this.accountKeySnapshotCache.CreateSnapshot += this.RequestCreateNewAccountKeySnapshot;
				this.accreditationCertificateSnapshotCache.CreateSnapshot += this.RequestCreateNewAccreditationCertificateSnapshot;
				this.chainOptionsSnapshotCache.CreateSnapshot += this.RequestCreateNewChainOptionSnapshot;

				this.initialized = true;
			}
		}

		public void Reset() {
			this.simpleAccountSnapshotCache.Reset();
			this.jointAccountSnapshotCache.Reset();
			this.accountKeySnapshotCache.Reset();
			this.accreditationCertificateSnapshotCache.Reset();
			this.chainOptionsSnapshotCache.Reset();

		}

		public void EnsureSnapshots(SnapshotKeySet snapshotKeySet) {
			// since we dont know which is which, we try to load from both. the odds its a simple account are high, so lets try this first
			this.simpleAccountSnapshotCache.EnsureSnapshots(snapshotKeySet.standardAccounts);
			this.jointAccountSnapshotCache.EnsureSnapshots(snapshotKeySet.jointAccounts);
			this.accountKeySnapshotCache.EnsureSnapshots(snapshotKeySet.accountKeys);
			this.accreditationCertificateSnapshotCache.EnsureSnapshots(snapshotKeySet.accreditationCertificates);
			this.chainOptionsSnapshotCache.EnsureSnapshots(snapshotKeySet.chainOptions);
		}

		public ACCOUNT_SNAPSHOT GetAccountSnapshotReadonly(AccountId newAccountId) {
			STANDARD_ACCOUNT_SNAPSHOT result = this.GetStandardAccountSnapshotReadonly(newAccountId);

			if(result != null) {
				return result;
			}

			return this.GetJointAccountSnapshotReadonly(newAccountId);
		}

		public T GetAccountSnapshotReadonly<T>(AccountId newAccountId)
			where T : class, IAccountSnapshot {
			return this.GetAccountSnapshotReadonly(newAccountId) as T;
		}

		public T GetAccountSnapshotModify<T>(AccountId newAccountId)
			where T : class, IAccountSnapshot {
			return this.GetAccountSnapshotModify(newAccountId) as T;
		}

		public ACCOUNT_SNAPSHOT GetAccountSnapshotModify(AccountId newAccountId) {
			STANDARD_ACCOUNT_SNAPSHOT result = this.GetStandardAccountSnapshotModify(newAccountId);

			if(result != null) {
				return result;
			}

			return this.GetJointAccountSnapshotModify(newAccountId);
		}

		public STANDARD_ACCOUNT_SNAPSHOT CreateNewStandardAccountSnapshot(AccountId newAccountId, AccountId TemporaryAccountHash) {
			STANDARD_ACCOUNT_SNAPSHOT entry = this.CreateLooseStandardAccountSnapshot(newAccountId);

			if(entry == null) {
				return null;
			}

			entry.AccountId = newAccountId.ToLongRepresentation();
			this.simpleAccountSnapshotCache.AddEntry(newAccountId, TemporaryAccountHash, entry);

			return entry;
		}

		public bool CheckStandardAccountSnapshotExists(AccountId newAccountId) {
			return this.simpleAccountSnapshotCache.CheckEntryExists(newAccountId);
		}

		public void DeleteAccountSnapshot(AccountId newAccountId) {

			if(this.GetStandardAccountSnapshotReadonly(newAccountId) != null) {
				this.DeleteStandardAccountSnapshot(newAccountId);
			}

			if(this.GetJointAccountSnapshotReadonly(newAccountId) != null) {
				this.DeleteJointAccountSnapshot(newAccountId);
			}
		}

		public STANDARD_ACCOUNT_SNAPSHOT CreateLooseStandardAccountSnapshot(AccountId newAccountId) {
			return this.RequestCreateNewStandardAccountSnapshot?.Invoke();
		}

		public STANDARD_ACCOUNT_SNAPSHOT GetStandardAccountSnapshotReadonly(AccountId newAccountId) {
			return this.simpleAccountSnapshotCache.GetEntryReadonly(newAccountId);
		}

		public STANDARD_ACCOUNT_SNAPSHOT GetStandardAccountSnapshotModify(AccountId newAccountId) {
			return this.simpleAccountSnapshotCache.GetEntryModify(newAccountId);
		}

		public JOINT_ACCOUNT_SNAPSHOT CreateNewJointAccountSnapshot(AccountId newAccountId, AccountId TemporaryAccountHash) {
			JOINT_ACCOUNT_SNAPSHOT entry = this.CreateLooseJointAccountSnapshot(newAccountId);

			if(entry == null) {
				return null;
			}

			entry.AccountId = newAccountId.ToLongRepresentation();
			this.jointAccountSnapshotCache.AddEntry(newAccountId, TemporaryAccountHash, entry);

			return entry;
		}

		public void DeleteStandardAccountSnapshot(AccountId newAccountId) {

			this.simpleAccountSnapshotCache.DeleteEntry(newAccountId);
		}

		public JOINT_ACCOUNT_SNAPSHOT CreateLooseJointAccountSnapshot(AccountId newAccountId) {
			return this.RequestCreateNewJointAccountSnapshot?.Invoke();
		}

		public JOINT_ACCOUNT_SNAPSHOT GetJointAccountSnapshotReadonly(AccountId newAccountId) {
			return this.jointAccountSnapshotCache.GetEntryReadonly(newAccountId);
		}

		public JOINT_ACCOUNT_SNAPSHOT GetJointAccountSnapshotModify(AccountId newAccountId) {
			return this.jointAccountSnapshotCache.GetEntryModify(newAccountId);
		}

		public bool CheckJointAccountSnapshotExists(AccountId newAccountId) {
			return this.jointAccountSnapshotCache.CheckEntryExists(newAccountId);
		}

		public void DeleteJointAccountSnapshot(AccountId newAccountId) {

			this.jointAccountSnapshotCache.DeleteEntry(newAccountId);
		}

		public STANDARD_ACCOUNT_KEY_SNAPSHOT CreateNewAccountKeySnapshot((long AccountId, byte OrdinalId) key) {
			STANDARD_ACCOUNT_KEY_SNAPSHOT entry = this.CreateLooseAccountKeySnapshot(key);

			if(entry == null) {
				return null;
			}

			entry.CompositeKey = key.AccountId.ToString() + key.OrdinalId.ToString();
			entry.AccountId = key.AccountId;
			entry.OrdinalId = key.OrdinalId;

			this.accountKeySnapshotCache.AddEntry(key, entry);

			return entry;
		}

		public STANDARD_ACCOUNT_KEY_SNAPSHOT CreateLooseAccountKeySnapshot((long AccountId, byte OrdinalId) key) {
			return this.RequestCreateNewAccountKeySnapshot?.Invoke();
		}

		public STANDARD_ACCOUNT_KEY_SNAPSHOT GetAccountKeySnapshotReadonly((long AccountId, byte OrdinalId) key) {
			return this.accountKeySnapshotCache.GetEntryReadonly(key);
		}

		public STANDARD_ACCOUNT_KEY_SNAPSHOT GetAccountKeySnapshotModify((long AccountId, byte OrdinalId) key) {
			return this.accountKeySnapshotCache.GetEntryModify(key);
		}

		public bool CheckAccountKeySnapshotExists((long AccountId, byte OrdinalId) key) {
			return this.accountKeySnapshotCache.CheckEntryExists(key);
		}

		public void DeleteJointAccountSnapshot((long AccountId, byte OrdinalId) key) {

			this.accountKeySnapshotCache.DeleteEntry(key);
		}

		public ACCREDITATION_CERTIFICATE_SNAPSHOT CreateNewAccreditationCertificateSnapshot(int id) {
			ACCREDITATION_CERTIFICATE_SNAPSHOT entry = this.CreateLooseAccreditationCertificateSnapshot(id);

			if(entry == null) {
				return null;
			}

			entry.CertificateId = id;
			this.accreditationCertificateSnapshotCache.AddEntry(id, entry);

			return entry;
		}

		public ACCREDITATION_CERTIFICATE_SNAPSHOT CreateLooseAccreditationCertificateSnapshot(int id) {
			return this.RequestCreateNewAccreditationCertificateSnapshot?.Invoke();
		}

		public ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificateSnapshotReadonly(int id) {
			return this.accreditationCertificateSnapshotCache.GetEntryReadonly(id);
		}

		public ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificateSnapshotModify(int id) {
			return this.accreditationCertificateSnapshotCache.GetEntryModify(id);
		}

		public bool CheckAccreditationCertificateSnapshotExists(int id) {
			return this.accreditationCertificateSnapshotCache.CheckEntryExists(id);
		}

		public void DeleteAccreditationCertificateSnapshot(int id) {

			this.accreditationCertificateSnapshotCache.DeleteEntry(id);
		}

		public CHAIN_OPTIONS_SNAPSHOT CreateNewChainOptionsSnapshot(int id) {
			CHAIN_OPTIONS_SNAPSHOT entry = this.CreateLooseChainOptionsSnapshot(id);

			if(entry == null) {
				return null;
			}

			this.chainOptionsSnapshotCache.AddEntry(id, entry);

			return entry;
		}

		public CHAIN_OPTIONS_SNAPSHOT CreateLooseChainOptionsSnapshot(int id) {
			return this.RequestCreateNewChainOptionSnapshot?.Invoke();
		}

		public CHAIN_OPTIONS_SNAPSHOT GetChainOptionsSnapshotReadonly(int id) {
			return this.chainOptionsSnapshotCache.GetEntryReadonly(id);
		}

		public CHAIN_OPTIONS_SNAPSHOT GetChainOptionsSnapshotModify(int id) {
			return this.chainOptionsSnapshotCache.GetEntryModify(id);
		}

		public bool CheckChainOptionsSnapshotExists(int id) {
			return this.chainOptionsSnapshotCache.CheckEntryExists(id);
		}

		public void DeleteChainOptionsSnapshot(int id) {

			this.chainOptionsSnapshotCache.DeleteEntry(id);
		}

		public SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> GetEntriesModificationStack() {

			var history = new SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>();

			history.simpleAccounts = this.simpleAccountSnapshotCache.GetEntriesSubKeyModificationStack();
			history.jointAccounts = this.jointAccountSnapshotCache.GetEntriesSubKeyModificationStack();
			history.standardAccountKeys = this.accountKeySnapshotCache.GetEntriesModificationStack();
			history.accreditationCertificates = this.accreditationCertificateSnapshotCache.GetEntriesModificationStack();
			history.chainOptions = this.chainOptionsSnapshotCache.GetEntriesModificationStack();

			return history;
		}

	#region recording

		public void BeginTransaction() {
			this.simpleAccountSnapshotCache.BeginTransaction();
			this.jointAccountSnapshotCache.BeginTransaction();
			this.accountKeySnapshotCache.BeginTransaction();
			this.accreditationCertificateSnapshotCache.BeginTransaction();
			this.chainOptionsSnapshotCache.BeginTransaction();
		}

		public void CommitTransaction() {
			this.simpleAccountSnapshotCache.CommitTransaction();
			this.jointAccountSnapshotCache.CommitTransaction();
			this.accountKeySnapshotCache.CommitTransaction();
			this.accreditationCertificateSnapshotCache.CommitTransaction();
			this.chainOptionsSnapshotCache.CommitTransaction();
		}

		public void RollbackTransaction() {
			this.simpleAccountSnapshotCache.RollbackTransaction();
			this.jointAccountSnapshotCache.RollbackTransaction();
			this.accountKeySnapshotCache.RollbackTransaction();
			this.accreditationCertificateSnapshotCache.RollbackTransaction();
			this.chainOptionsSnapshotCache.RollbackTransaction();
		}

	#endregion

	}

	public class SnapshotKeySet {
		public List<(long AccountId, byte OrdinalId)> accountKeys = new List<(long AccountId, byte OrdinalId)>();
		public List<int> accreditationCertificates = new List<int>();
		public List<int> chainOptions = new List<int>();
		public List<AccountId> jointAccounts = new List<AccountId>();

		public List<AccountId> standardAccounts = new List<AccountId>();

		public List<AccountId> AllAccounts {
			get {
				var results = this.standardAccounts.ToList();

				results.AddRange(this.jointAccounts);

				return results;
			}
		}

		public void AddAccounts(List<AccountId> accountIds) {

			this.standardAccounts.AddRange(accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard));
			this.jointAccounts.AddRange(accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint));
		}

		public void Add(SnapshotKeySet snapshotKeySet) {
			if(snapshotKeySet != null) {
				this.standardAccounts.AddRange(snapshotKeySet.standardAccounts);
				this.jointAccounts.AddRange(snapshotKeySet.jointAccounts);
				this.accountKeys.AddRange(snapshotKeySet.accountKeys);
				this.accreditationCertificates.AddRange(snapshotKeySet.accreditationCertificates);
				this.chainOptions.AddRange(snapshotKeySet.chainOptions);
			}
		}

		public void Distinct() {
			this.standardAccounts = this.standardAccounts.DistinctBy(a => a).ToList();
			this.jointAccounts = this.jointAccounts.DistinctBy(a => a).ToList();
			this.accountKeys = this.accountKeys.DistinctBy(a => a).ToList();
			this.accreditationCertificates = this.accreditationCertificates.DistinctBy(a => a).ToList();
			this.chainOptions = this.chainOptions.DistinctBy(a => a).ToList();
		}

		public void AddAccountId(AccountId accountId) {
			if(accountId.AccountType == Enums.AccountTypes.Standard) {
				this.standardAccounts.Add(accountId);
			}

			if(accountId.AccountType == Enums.AccountTypes.Joint) {
				this.jointAccounts.Add(accountId);
			}
		}
	}

	public class SnapshotSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : StandardAccountSnapshot<STANDARD_ACCOUNT_FEATURE_SNAPSHOT>, new()
		where STANDARD_ACCOUNT_FEATURE_SNAPSHOT : AccountFeature, new()
		where JOINT_ACCOUNT_SNAPSHOT : JointAccountSnapshot<JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where JOINT_ACCOUNT_FEATURE_SNAPSHOT : AccountFeature, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : JointMemberAccount, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : StandardAccountKeysSnapshot, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : AccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : AccreditationCertificateSnapshotAccount
		where CHAIN_OPTIONS_SNAPSHOT : ChainOptionsSnapshot, new() {
		public Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT> accountKeys = new Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT>();
		public Dictionary<Guid, ACCREDITATION_CERTIFICATE_SNAPSHOT> accreditationCertificates = new Dictionary<Guid, ACCREDITATION_CERTIFICATE_SNAPSHOT>();
		public Dictionary<int, CHAIN_OPTIONS_SNAPSHOT> chainOptions = new Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>();
		public Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT> jointAccounts = new Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT>();

		public Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT> simpleAccounts = new Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT>();

		public void Add(SnapshotSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotSet) {
			if(snapshotSet != null) {

				foreach(var entry in snapshotSet.simpleAccounts) {
					if(!this.simpleAccounts.ContainsKey(entry.Key)) {
						this.simpleAccounts[entry.Key] = entry.Value;
					}
				}

				foreach(var entry in snapshotSet.jointAccounts) {
					if(!this.jointAccounts.ContainsKey(entry.Key)) {
						this.jointAccounts[entry.Key] = entry.Value;
					}
				}

				foreach(var entry in snapshotSet.accountKeys) {
					if(!this.accountKeys.ContainsKey(entry.Key)) {
						this.accountKeys[entry.Key] = entry.Value;
					}
				}

				foreach(var entry in snapshotSet.accreditationCertificates) {
					if(!this.accreditationCertificates.ContainsKey(entry.Key)) {
						this.accreditationCertificates[entry.Key] = entry.Value;
					}
				}

				foreach(var entry in snapshotSet.chainOptions) {
					if(!this.chainOptions.ContainsKey(entry.Key)) {
						this.chainOptions[entry.Key] = entry.Value;
					}
				}
			}
		}

		public bool Any() {
			return this.simpleAccounts.Any() || this.jointAccounts.Any() || this.accountKeys.Any() || this.accreditationCertificates.Any() || this.chainOptions.Any();
		}
	}

}