using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public interface IAccountsSnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshot<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshot<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccount, new() {

		Task<ACCOUNT_SNAPSHOT> GetAccountSnapshotReadonly(AccountId newAccountId, LockContext lockContext);
		Task<ACCOUNT_SNAPSHOT> GetAccountSnapshotModify(AccountId newAccountId, LockContext lockContext);

		Task<T> GetAccountSnapshotReadonly<T>(AccountId newAccountId, LockContext lockContext)
			where T : class, IAccountSnapshot;

		Task<T> GetAccountSnapshotModify<T>(AccountId newAccountId, LockContext lockContext)
			where T : class, IAccountSnapshot;

		Task<STANDARD_ACCOUNT_SNAPSHOT> CreateNewStandardAccountSnapshot(AccountId newAccountId, AccountId TemporaryAccountHash, LockContext lockContext);
		Task<bool> CheckStandardAccountSnapshotExists(AccountId newAccountId, LockContext lockContext);
		void DeleteAccountSnapshot(AccountId newAccountId, LockContext lockContext);

		Task<STANDARD_ACCOUNT_SNAPSHOT> CreateLooseStandardAccountSnapshot(AccountId newAccountId, LockContext lockContext);
		Task<STANDARD_ACCOUNT_SNAPSHOT> GetStandardAccountSnapshotReadonly(AccountId newAccountId, LockContext lockContext);
		Task<STANDARD_ACCOUNT_SNAPSHOT> GetStandardAccountSnapshotModify(AccountId newAccountId, LockContext lockContext);
		Task<JOINT_ACCOUNT_SNAPSHOT> CreateNewJointAccountSnapshot(AccountId newAccountId, AccountId TemporaryAccountHash, LockContext lockContext);

		void DeleteStandardAccountSnapshot(AccountId newAccountId, LockContext lockContext);

		Task<JOINT_ACCOUNT_SNAPSHOT> CreateLooseJointAccountSnapshot(AccountId newAccountId, LockContext lockContext);
		Task<JOINT_ACCOUNT_SNAPSHOT> GetJointAccountSnapshotReadonly(AccountId newAccountId, LockContext lockContext);
		Task<JOINT_ACCOUNT_SNAPSHOT> GetJointAccountSnapshotModify(AccountId newAccountId, LockContext lockContext);
		Task<bool> CheckJointAccountSnapshotExists(AccountId newAccountId, LockContext lockContext);
		void DeleteJointAccountSnapshot(AccountId newAccountId, LockContext lockContext);
	}

	public interface IAccountkeysSnapshotCacheSet<STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshot {

		Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> CreateNewAccountKeySnapshot((long AccountId, byte OrdinalId) key, LockContext lockContext);
		Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> CreateLooseAccountKeySnapshot((long AccountId, byte OrdinalId) key, LockContext lockContext);
		Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> GetAccountKeySnapshotReadonly((long AccountId, byte OrdinalId) key, LockContext lockContext);
		Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> GetAccountKeySnapshotModify((long AccountId, byte OrdinalId) key, LockContext lockContext);
		Task<bool> CheckAccountKeySnapshotExists((long AccountId, byte OrdinalId) key, LockContext lockContext);
		void DeleteJointAccountSnapshot((long AccountId, byte OrdinalId) key, LockContext lockContext);
	}

	public interface IAccreditationCertificateSnapshotCacheSet<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccount {

		Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> CreateNewAccreditationCertificateSnapshot(int id, LockContext lockContext);
		Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> CreateLooseAccreditationCertificateSnapshot(int id, LockContext lockContext);
		Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificateSnapshotReadonly(int id, LockContext lockContext);
		Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificateSnapshotModify(int id, LockContext lockContext);
		Task<bool> CheckAccreditationCertificateSnapshotExists(int id, LockContext lockContext);
		void DeleteAccreditationCertificateSnapshot(int id, LockContext lockContext);
	}

	public interface IChainOptionsSnapshotCacheSet<CHAIN_OPTIONS_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshot {

		Task<CHAIN_OPTIONS_SNAPSHOT> CreateNewChainOptionsSnapshot(int id, LockContext lockContext);
		Task<CHAIN_OPTIONS_SNAPSHOT> CreateLooseChainOptionsSnapshot(int id, LockContext lockContext);
		Task<CHAIN_OPTIONS_SNAPSHOT> GetChainOptionsSnapshotReadonly(int id, LockContext lockContext);
		Task<CHAIN_OPTIONS_SNAPSHOT> GetChainOptionsSnapshotModify(int id, LockContext lockContext);
		Task<bool> CheckChainOptionsSnapshotExists(int id, LockContext lockContext);
		void DeleteChainOptionsSnapshot(int id, LockContext lockContext);
	}

	public interface ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : IAccountsSnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, IAccountkeysSnapshotCacheSet<STANDARD_ACCOUNT_KEY_SNAPSHOT>, IAccreditationCertificateSnapshotCacheSet<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, IChainOptionsSnapshotCacheSet<CHAIN_OPTIONS_SNAPSHOT>
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

		event Func<LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> RequestCreateNewStandardAccountSnapshot;
		event Func<LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> RequestCreateNewJointAccountSnapshot;
		event Func<LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> RequestCreateNewAccountKeySnapshot;
		event Func<LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> RequestCreateNewAccreditationCertificateSnapshot;
		event Func<LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> RequestCreateNewChainOptionSnapshot;
		
		event Func<List<AccountId>, LockContext, Task<Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT>>> RequestStandardAccountSnapshots;
		event Func<List<AccountId>, LockContext, Task<Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT>>> RequestJointAccountSnapshots;
		event Func<List<(long AccountId, byte OrdinalId)>, LockContext, Task<Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT>>> RequestAccountKeySnapshots;
		event Func<List<int>, LockContext, Task<Dictionary<int, ACCREDITATION_CERTIFICATE_SNAPSHOT>>> RequestAccreditationCertificateSnapshots;
		event Func<List<int>, LockContext, Task<Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>>> RequestChainOptionSnapshots;
		void Initialize();
		void Reset();
		void BeginTransaction();
		void CommitTransaction();
		void RollbackTransaction();
		Task EnsureSnapshots(SnapshotKeySet snapshotKeySet, LockContext lockContext);
	}

	public interface ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
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
	}

	public static class SnapshotCacheSet {
	}

	public abstract class SnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
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

		public event Func<LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> RequestCreateNewStandardAccountSnapshot;
		public event Func<LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> RequestCreateNewJointAccountSnapshot;
		public event Func<LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> RequestCreateNewAccountKeySnapshot;
		public event Func<LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> RequestCreateNewAccreditationCertificateSnapshot;
		public event Func<LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> RequestCreateNewChainOptionSnapshot;

		public event Func<List<AccountId>, LockContext, Task<Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT>>> RequestStandardAccountSnapshots;
		public event Func<List<AccountId>, LockContext, Task<Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT>>> RequestJointAccountSnapshots;
		public event Func<List<(long AccountId, byte OrdinalId)>, LockContext, Task<Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT>>> RequestAccountKeySnapshots;
		public event Func<List<int>, LockContext, Task<Dictionary<int, ACCREDITATION_CERTIFICATE_SNAPSHOT>>> RequestAccreditationCertificateSnapshots;
		public event Func<List<int>, LockContext, Task<Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>>> RequestChainOptionSnapshots;

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

		public async Task EnsureSnapshots(SnapshotKeySet snapshotKeySet, LockContext lockContext) {
			// since we dont know which is which, we try to load from both. the odds its a simple account are high, so lets try this first
			await this.simpleAccountSnapshotCache.EnsureSnapshots(snapshotKeySet.standardAccounts, lockContext).ConfigureAwait(false);
			await this.jointAccountSnapshotCache.EnsureSnapshots(snapshotKeySet.jointAccounts, lockContext).ConfigureAwait(false);
			await this.accountKeySnapshotCache.EnsureSnapshots(snapshotKeySet.accountKeys, lockContext).ConfigureAwait(false);
			await this.accreditationCertificateSnapshotCache.EnsureSnapshots(snapshotKeySet.accreditationCertificates, lockContext).ConfigureAwait(false);
			await this.chainOptionsSnapshotCache.EnsureSnapshots(snapshotKeySet.chainOptions, lockContext).ConfigureAwait(false);
		}

		public async Task<ACCOUNT_SNAPSHOT> GetAccountSnapshotReadonly(AccountId newAccountId, LockContext lockContext) {
			STANDARD_ACCOUNT_SNAPSHOT result = await this.GetStandardAccountSnapshotReadonly(newAccountId, lockContext).ConfigureAwait(false);

			if(result != null) {
				return result;
			}

			return await this.GetJointAccountSnapshotReadonly(newAccountId, lockContext).ConfigureAwait(false);
		}

		public async Task<T> GetAccountSnapshotReadonly<T>(AccountId newAccountId, LockContext lockContext)
			where T : class, IAccountSnapshot {
			return await this.GetAccountSnapshotReadonly(newAccountId, lockContext).ConfigureAwait(false) as T;
		}

		public async Task<T> GetAccountSnapshotModify<T>(AccountId newAccountId, LockContext lockContext)
			where T : class, IAccountSnapshot {
			return await this.GetAccountSnapshotModify(newAccountId, lockContext).ConfigureAwait(false) as T;
		}

		public async Task<ACCOUNT_SNAPSHOT> GetAccountSnapshotModify(AccountId newAccountId, LockContext lockContext) {
			STANDARD_ACCOUNT_SNAPSHOT result = await this.GetStandardAccountSnapshotModify(newAccountId, lockContext).ConfigureAwait(false);

			if(result != null) {
				return result;
			}

			return await this.GetJointAccountSnapshotModify(newAccountId, lockContext).ConfigureAwait(false);
		}

		public async Task<STANDARD_ACCOUNT_SNAPSHOT> CreateNewStandardAccountSnapshot(AccountId newAccountId, AccountId TemporaryAccountHash, LockContext lockContext) {
			
			STANDARD_ACCOUNT_SNAPSHOT entry = this.simpleAccountSnapshotCache.LastNew(newAccountId);
			
			if(entry != null) {
				return entry;
			}
			
			entry = await CreateLooseStandardAccountSnapshot(newAccountId, lockContext).ConfigureAwait(false);

			if(entry == null) {
				return null;
			}

			entry.AccountId = newAccountId.ToLongRepresentation();
			this.simpleAccountSnapshotCache.AddEntry(newAccountId, TemporaryAccountHash, entry);

			return entry;
		}

		public Task<bool> CheckStandardAccountSnapshotExists(AccountId newAccountId, LockContext lockContext) {
			return this.simpleAccountSnapshotCache.CheckEntryExists(newAccountId, lockContext);
		}

		public void DeleteAccountSnapshot(AccountId newAccountId, LockContext lockContext) {

			if(this.GetStandardAccountSnapshotReadonly(newAccountId, lockContext) != null) {
				this.DeleteStandardAccountSnapshot(newAccountId, lockContext);
			}

			if(this.GetJointAccountSnapshotReadonly(newAccountId, lockContext) != null) {
				this.DeleteJointAccountSnapshot(newAccountId, lockContext);
			}
		}

		public Task<STANDARD_ACCOUNT_SNAPSHOT> CreateLooseStandardAccountSnapshot(AccountId newAccountId, LockContext lockContext) {
			if(this.RequestCreateNewStandardAccountSnapshot != null) {
				return this.RequestCreateNewStandardAccountSnapshot(lockContext);
			}

			return Task.FromResult((STANDARD_ACCOUNT_SNAPSHOT)null);
		}

		public Task<STANDARD_ACCOUNT_SNAPSHOT> GetStandardAccountSnapshotReadonly(AccountId newAccountId, LockContext lockContext) {
			return this.simpleAccountSnapshotCache.GetEntryReadonly(newAccountId, lockContext);
		}

		public Task<STANDARD_ACCOUNT_SNAPSHOT> GetStandardAccountSnapshotModify(AccountId newAccountId, LockContext lockContext) {
			return this.simpleAccountSnapshotCache.GetEntryModify(newAccountId, lockContext);
		}

		public async Task<JOINT_ACCOUNT_SNAPSHOT> CreateNewJointAccountSnapshot(AccountId newAccountId, AccountId TemporaryAccountHash, LockContext lockContext) {
			
			JOINT_ACCOUNT_SNAPSHOT entry = this.jointAccountSnapshotCache.LastNew(newAccountId);
			
			if(entry != null) {
				return entry;
			}

			entry = await CreateLooseJointAccountSnapshot(newAccountId, lockContext).ConfigureAwait(false);

			if(entry == null) {
				return null;
			}

			entry.AccountId = newAccountId.ToLongRepresentation();
			this.jointAccountSnapshotCache.AddEntry(newAccountId, TemporaryAccountHash, entry);

			return entry;
		}

		public void DeleteStandardAccountSnapshot(AccountId newAccountId, LockContext lockContext) {

			this.simpleAccountSnapshotCache.DeleteEntry(newAccountId, lockContext);
		}

		public Task<JOINT_ACCOUNT_SNAPSHOT> CreateLooseJointAccountSnapshot(AccountId newAccountId, LockContext lockContext) {
			if(this.RequestCreateNewJointAccountSnapshot != null) {
				return this.RequestCreateNewJointAccountSnapshot(lockContext);
			}

			return Task.FromResult((JOINT_ACCOUNT_SNAPSHOT)null);
		}

		public Task<JOINT_ACCOUNT_SNAPSHOT> GetJointAccountSnapshotReadonly(AccountId newAccountId, LockContext lockContext) {
			return this.jointAccountSnapshotCache.GetEntryReadonly(newAccountId, lockContext);
		}

		public Task<JOINT_ACCOUNT_SNAPSHOT> GetJointAccountSnapshotModify(AccountId newAccountId, LockContext lockContext) {
			return this.jointAccountSnapshotCache.GetEntryModify(newAccountId, lockContext);
		}

		public Task<bool> CheckJointAccountSnapshotExists(AccountId newAccountId, LockContext lockContext) {
			return this.jointAccountSnapshotCache.CheckEntryExists(newAccountId, lockContext);
		}

		public void DeleteJointAccountSnapshot(AccountId newAccountId, LockContext lockContext) {

			this.jointAccountSnapshotCache.DeleteEntry(newAccountId, lockContext);
		}

		protected abstract ICardUtils GetCardUtils();
		
		public async Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> CreateNewAccountKeySnapshot((long AccountId, byte OrdinalId) key, LockContext lockContext) {
			
			STANDARD_ACCOUNT_KEY_SNAPSHOT entry = this.accountKeySnapshotCache.LastNew(key);
			
			if(entry != null) {
				return entry;
			}

			entry = await CreateLooseAccountKeySnapshot(key, lockContext).ConfigureAwait(false);

			if(entry == null) {
				return null;
			}

			entry.CompositeKey = this.GetCardUtils().GenerateCompositeKey(key.AccountId, key.OrdinalId);
			entry.AccountId = key.AccountId;
			entry.OrdinalId = key.OrdinalId;

			this.accountKeySnapshotCache.AddEntry(key, entry);

			return entry;
		}

		public Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> CreateLooseAccountKeySnapshot((long AccountId, byte OrdinalId) key, LockContext lockContext) {
			if(this.RequestCreateNewAccountKeySnapshot != null) {
				return this.RequestCreateNewAccountKeySnapshot(lockContext);
			}

			return Task.FromResult((STANDARD_ACCOUNT_KEY_SNAPSHOT)null);
		}

		public Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> GetAccountKeySnapshotReadonly((long AccountId, byte OrdinalId) key, LockContext lockContext) {
			return this.accountKeySnapshotCache.GetEntryReadonly(key, lockContext);
		}

		public Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> GetAccountKeySnapshotModify((long AccountId, byte OrdinalId) key, LockContext lockContext) {
			return this.accountKeySnapshotCache.GetEntryModify(key, lockContext);
		}

		public Task<bool> CheckAccountKeySnapshotExists((long AccountId, byte OrdinalId) key, LockContext lockContext) {
			return this.accountKeySnapshotCache.CheckEntryExists(key, lockContext);
		}

		public void DeleteJointAccountSnapshot((long AccountId, byte OrdinalId) key, LockContext lockContext) {

			this.accountKeySnapshotCache.DeleteEntry(key, lockContext);
		}

		public async Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> CreateNewAccreditationCertificateSnapshot(int id, LockContext lockContext) {
			
			ACCREDITATION_CERTIFICATE_SNAPSHOT entry = this.accreditationCertificateSnapshotCache.LastNew(id);
			
			if(entry != null) {
				return entry;
			}

			entry = await CreateLooseAccreditationCertificateSnapshot(id, lockContext).ConfigureAwait(false);

			if(entry == null) {
				return null;
			}

			entry.CertificateId = id;
			this.accreditationCertificateSnapshotCache.AddEntry(id, entry);

			return entry;
		}

		public Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> CreateLooseAccreditationCertificateSnapshot(int id, LockContext lockContext) {
			if(this.RequestCreateNewAccreditationCertificateSnapshot != null) {
				return this.RequestCreateNewAccreditationCertificateSnapshot(lockContext);
			}

			return Task.FromResult((ACCREDITATION_CERTIFICATE_SNAPSHOT)null);
		}

		public Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificateSnapshotReadonly(int id, LockContext lockContext) {
			return this.accreditationCertificateSnapshotCache.GetEntryReadonly(id, lockContext);
		}

		public Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificateSnapshotModify(int id, LockContext lockContext) {
			return this.accreditationCertificateSnapshotCache.GetEntryModify(id, lockContext);
		}

		public Task<bool> CheckAccreditationCertificateSnapshotExists(int id, LockContext lockContext) {
			return this.accreditationCertificateSnapshotCache.CheckEntryExists(id, lockContext);
		}

		public void DeleteAccreditationCertificateSnapshot(int id, LockContext lockContext) {

			this.accreditationCertificateSnapshotCache.DeleteEntry(id, lockContext);
		}

		public async Task<CHAIN_OPTIONS_SNAPSHOT> CreateNewChainOptionsSnapshot(int id, LockContext lockContext) {

			CHAIN_OPTIONS_SNAPSHOT entry = this.chainOptionsSnapshotCache.LastNew(id);
			
			if(entry != null) {
				return entry;
			}
			
			entry = await CreateLooseChainOptionsSnapshot(id, lockContext).ConfigureAwait(false);

			if(entry == null) {
				return null;
			}

			this.chainOptionsSnapshotCache.AddEntry(id, entry);

			return entry;
		}

		public Task<CHAIN_OPTIONS_SNAPSHOT> CreateLooseChainOptionsSnapshot(int id, LockContext lockContext) {
			if(this.RequestCreateNewChainOptionSnapshot != null) {
				return this.RequestCreateNewChainOptionSnapshot(lockContext);
			}

			return Task.FromResult((CHAIN_OPTIONS_SNAPSHOT)null);
		}

		public Task<CHAIN_OPTIONS_SNAPSHOT> GetChainOptionsSnapshotReadonly(int id, LockContext lockContext) {
			return this.chainOptionsSnapshotCache.GetEntryReadonly(id, lockContext);
		}

		public Task<CHAIN_OPTIONS_SNAPSHOT> GetChainOptionsSnapshotModify(int id, LockContext lockContext) {
			return this.chainOptionsSnapshotCache.GetEntryModify(id, lockContext);
		}

		public Task<bool> CheckChainOptionsSnapshotExists(int id, LockContext lockContext) {
			return this.chainOptionsSnapshotCache.CheckEntryExists(id, lockContext);
		}

		public void DeleteChainOptionsSnapshot(int id, LockContext lockContext) {

			this.chainOptionsSnapshotCache.DeleteEntry(id, lockContext);
		}

		public SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> GetEntriesModificationStack() {

			var history = new SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>();

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

		public void AddAccounts(ImmutableList<AccountId> accountIds) {

			this.standardAccounts.AddRange(accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard));
			this.jointAccounts.AddRange(accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint));
		}
		
		public void AddAccounts(List<AccountId> accountIds) {

			this.AddAccounts(accountIds.ToImmutableList());
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

	public class SnapshotSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : StandardAccountSnapshot<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : AccountAttribute, new()
		where JOINT_ACCOUNT_SNAPSHOT : JointAccountSnapshot<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : AccountAttribute, new()
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

		public void Add(SnapshotSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotSet) {
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