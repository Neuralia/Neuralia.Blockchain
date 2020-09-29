using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IAccountSnapshotsProvider : IChainProvider {

		//		void InsertModeratorKey(TransactionId transactionId, byte keyId, ArrayWrapper key);
		//		void UpdateModeratorKey(TransactionId transactionId, byte keyId, ArrayWrapper key);
		//		ArrayWrapper GetModeratorKey(byte keyId);
		//		Enums.ChainSyncState GetChainSyncState();
		Task<bool> AnyAccountTracked(List<AccountId> accountId);
		Task<bool> AnyAccountTracked();
		Task<List<AccountId>> AccountsTracked(List<AccountId> accountId);

		void StartTrackingAccounts(List<AccountId> accountIds);
		void StartTrackingConfigAccounts();

		Task<bool> IsAccountTracked(AccountId accountId);
		Task<bool> IsAccountTracked(long accountSequenceId, Enums.AccountTypes accountType);

		Task UpdateSnapshotDigestFromDigest(IAccountSnapshotDigestChannelCard accountSnapshotDigestChannelCard);
		Task UpdateAccountKeysFromDigest(IStandardAccountKeysDigestChannelCard standardAccountKeysDigestChannelCard);
		Task UpdateAccreditationCertificateFromDigest(IAccreditationCertificateDigestChannelCard accreditationCertificateDigestChannelCard);
		Task UpdateChainOptionsFromDigest(IChainOptionsDigestChannelCard chainOptionsDigestChannelCard);

		Task ClearSnapshots();

		Task<List<IAccountSnapshot>> LoadAccountSnapshots(List<AccountId> accountIds);
		Task<List<IAccountKeysSnapshot>> LoadStandardAccountKeysSnapshots(List<(long accountId, byte ordinal)> keys);
		Task<IChainOptionsSnapshot> LoadChainOptionsSnapshot();
		Task<List<IAccreditationCertificateSnapshot>> LoadAccreditationCertificatesSnapshots(List<int> certificateIds);

		IStandardAccountSnapshot CreateNewStandardAccountSnapshots();
		IJointAccountSnapshot CreateNewJointAccountSnapshots();
		IAccountKeysSnapshot CreateNewAccountKeySnapshots();
		IAccreditationCertificateSnapshot CreateNewAccreditationCertificateSnapshots();
		IChainOptionsSnapshot CreateNewChainOptionsSnapshots();

		Task ProcessSnapshotImpacts(ISnapshotHistoryStackSet snapshotsModificationHistoryStack);
		List<Func<LockContext, Task>> PrepareKeysSerializationTasks(Dictionary<(AccountId accountId, byte ordinal), byte[]> keyDictionary);
	}

	public interface IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IAccountSnapshotsProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public interface IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> : IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry {

		Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(ImmutableList<AccountId> accountIds, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType);
		Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(ImmutableList<AccountId> accountIds, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType);
		Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType);
		Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType);
		Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(int certificateId, AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType);
		Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(int certificateId, AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType);

		Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificate(List<int> certificateIds, AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType);
		Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificate(List<int> certificateIds, AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType);
		Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(int certificateId);
	}

	public interface IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshotEntry, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotEntry, new() {
	}

	public interface IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_DAL, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_DAL, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_DAL, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_DAL, TRACKED_ACCOUNTS_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where STANDARD_ACCOUNT_SNAPSHOT_DAL : class, IStandardAccountSnapshotDal<STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IStandardAccountSnapshotContext<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_DAL : class, IJointAccountSnapshotDal<JOINT_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IJointAccountSnapshotContext<JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL : class, IAccreditationCertificatesSnapshotDal<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL : class, IAccountKeysSnapshotDal<STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT : DbContext, IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_DAL : class, IChainOptionsSnapshotDal<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_CONTEXT : DbContext, IChainOptionsSnapshotContext<CHAIN_OPTIONS_SNAPSHOT>
		where TRACKED_ACCOUNTS_DAL : class, ITrackedAccountsDal
		where TRACKED_ACCOUNTS_CONTEXT : DbContext, ITrackedAccountsContext
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshotEntry, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotEntry, new() {
	}

	/// <summary>
	///     A provider that offers the chain state parameters from the DB
	/// </summary>
	/// <typeparam name="ACCOUNT_SNAPSHOT_DAL"></typeparam>
	/// <typeparam name="ACCOUNT_SNAPSHOT_CONTEXT"></typeparam>
	/// <typeparam name="STANDARD_ACCOUNT_SNAPSHOT"></typeparam>
	public abstract class AccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_DAL, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_DAL, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_DAL, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_DAL, TRACKED_ACCOUNTS_CONTEXT, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ChainProvider, IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_DAL, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_DAL, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_DAL, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_DAL, TRACKED_ACCOUNTS_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where STANDARD_ACCOUNT_SNAPSHOT_DAL : class, IStandardAccountSnapshotDal<STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IStandardAccountSnapshotContext<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_DAL : class, IJointAccountSnapshotDal<JOINT_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IJointAccountSnapshotContext<JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL : class, IAccreditationCertificatesSnapshotDal<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL : class, IAccountKeysSnapshotDal<STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT : DbContext, IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_DAL : class, IChainOptionsSnapshotDal<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_CONTEXT : DbContext, IChainOptionsSnapshotContext<CHAIN_OPTIONS_SNAPSHOT>
		where TRACKED_ACCOUNTS_DAL : class, ITrackedAccountsDal<TRACKED_ACCOUNTS_CONTEXT>
		where TRACKED_ACCOUNTS_CONTEXT : DbContext, ITrackedAccountsContext
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshotEntry, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotEntry, new() {

		protected const int GROUP_SIZE = 1_000_000;
		protected const int ACCOUNT_KEYS_GROUP_SIZE = GROUP_SIZE / 4; // each account has 4 keys, so we fit only 1/4th the amount of accounts entries

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		private readonly string folderPath;

		protected readonly object locker = new object();

		protected readonly ITimeService timeService;

		private (STANDARD_ACCOUNT_SNAPSHOT entry, bool full)? accountSnapshotEntry;

		public AccountSnapshotsProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;
		}

		public Func<CHAIN_OPTIONS_SNAPSHOT> CreateNewEntry { get; set; }

		protected abstract ICardUtils CardUtils { get; }

		public Task<bool> IsAccountTracked(AccountId accountId) {
			return this.IsAccountTracked(accountId.SequenceId, accountId.AccountType);
		}

		public Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(int certificateId, AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType) {
			return this.GetAccreditationCertificate(certificateId, accountId, new[] {certificateType}, applicationType);
		}

		public Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(int certificateId, AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType) {

			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificate(async db => {

				List<int> certificateTypeValues = certificateTypes.Select(c => (int) c.Value).ToList();

				// the the account is valid in the certificate
				long longAccountId = accountId.ToLongRepresentation();

				if(!await db.AccreditationCertificateAccounts.AnyAsync(c => (c.CertificateId == certificateId) && (c.AccountId == longAccountId)).ConfigureAwait(false)) {
					return null;
				}

				// ok, the account is in the certificate, lets select it itself
				return await db.AccreditationCertificates.SingleOrDefaultAsync(c => (c.CertificateId == certificateId) && certificateTypeValues.Contains(c.CertificateType) && c.ApplicationType.HasFlag(applicationType)).ConfigureAwait(false);
			}, certificateId);
		}

		public Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(int certificateId) {
			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificate(db => {
				return db.AccreditationCertificates.SingleOrDefaultAsync(c => c.CertificateId == certificateId);
			}, certificateId);
		}

		public Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificate(List<int> certificateIds, AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType) {
			return this.GetAccreditationCertificate(certificateIds, accountId, new[] {certificateType}, applicationType);
		}

		public Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificate(List<int> certificateIds, AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType) {

			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificates(db => {

				List<int> certificateTypeValues = certificateTypes.Select(c => (int) c.Value).ToList();

				// the the account is valid in the certificate
				long longAccountId = accountId.ToLongRepresentation();

				if(!db.AccreditationCertificateAccounts.Any(c => certificateIds.Contains(c.CertificateId) && (c.AccountId == longAccountId))) {
					return Task.FromResult(new List<ACCREDITATION_CERTIFICATE_SNAPSHOT>());
				}

				// ok, the account is in the certificate, lets select it itself
				return db.AccreditationCertificates.Where(c => certificateIds.Contains(c.CertificateId) && certificateTypeValues.Contains(c.CertificateType) && c.ApplicationType.HasFlag(applicationType)).ToListAsync();
			}, certificateIds);
		}

		public Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType) {
			return this.GetAccreditationCertificates(accountId, new[] {certificateType}, applicationType);
		}

		public Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(AccountId accountId, AccreditationCertificateType[] certificateType, Enums.CertificateApplicationTypes applicationType) {

			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificates(db => {
				List<int> certificateTypeValues = certificateType.Select(c => (int) c.Value).ToList();

				// the the account is valid in the certificate
				List<int> containsCertificates = db.AccreditationCertificateAccounts.Where(c => c.AccountId == accountId.ToLongRepresentation()).Select(c => c.CertificateId).ToList();

				// ok, the account is in the certificate, lets select it itself
				return db.AccreditationCertificates.Where(c => (c.AssignedAccount == accountId.ToLongRepresentation()) || (containsCertificates.Contains(c.CertificateId) && certificateTypeValues.Contains(c.CertificateType) && c.ApplicationType.HasFlag(applicationType))).ToListAsync();
			});
		}

		public Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(ImmutableList<AccountId> accountIds, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType) {
			return this.GetAccreditationCertificates(accountIds, new[] {certificateType}, applicationType);
		}

		public Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(ImmutableList<AccountId> accountIds, AccreditationCertificateType[] certificateType, Enums.CertificateApplicationTypes applicationType) {

			List<long> longAccountIds = accountIds.Select(a => a.ToLongRepresentation()).ToList();

			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificates(db => {
				List<int> certificateTypeValues = certificateType.Select(c => (int) c.Value).ToList();

				return db.AccreditationCertificates.Where(c => (c.AssignedAccount != 0) && longAccountIds.Contains(c.AssignedAccount) && certificateTypeValues.Contains(c.CertificateType) && c.ApplicationType.HasFlag(applicationType)).ToListAsync();
			});
		}

		public void StartTrackingConfigAccounts() {
			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.List) {
				// ensure we update the tracking list
				this.StartTrackingAccounts(chainConfiguration.TrackedSnapshotAccountsList.Select(AccountId.FromString).ToList());
			}
		}

		public void StartTrackingAccounts(List<AccountId> accountIds) {
			this.TrackedAccountsDal.AddTrackedAccounts(accountIds);
		}

		public async Task<bool> AnyAccountTracked() {
			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.None) {
				return false;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.All) {
				return true;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.List) {
				return await this.TrackedAccountsDal.AnyAccountsTracked().ConfigureAwait(false);
			}

			return false;
		}

		public async Task<bool> AnyAccountTracked(List<AccountId> accountIds) {
			if(!accountIds.Any()) {
				return false;
			}

			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.None) {
				return false;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.All) {
				return true;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.List) {
				return await this.TrackedAccountsDal.AnyAccountsTracked(accountIds).ConfigureAwait(false);
			}

			return false;
		}

		public async Task<List<AccountId>> AccountsTracked(List<AccountId> accountIds) {
			if(!accountIds.Any()) {
				return new List<AccountId>();
			}

			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.None) {
				return new List<AccountId>();
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.All) {
				return accountIds;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.List) {

				return await this.TrackedAccountsDal.GetTrackedAccounts(accountIds).ConfigureAwait(false);
			}

			return new List<AccountId>();
		}

		/// <summary>
		///     Determine if we are tracking a certain account to maintain the snapshots.
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public virtual async Task<bool> IsAccountTracked(long accountSequenceId, Enums.AccountTypes accountType) {

			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.None) {
				return false;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.All) {
				return true;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.List) {

				return await this.TrackedAccountsDal.IsAccountTracked(new AccountId(accountSequenceId, accountType)).ConfigureAwait(false);
			}

			return false;
		}

		/// <summary>
		///     this is an important method where we commit all stacks of changes to the snapshot databases. transactional of
		///     course
		/// </summary>
		/// <param name="snapshotsModificationHistoryStack"></param>
		/// <returns></returns>
		public virtual Task ProcessSnapshotImpacts(ISnapshotHistoryStackSet snapshotsModificationHistoryStack) {

			ISnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> specializedSnapshotsModificationHistoryStack = this.GetSpecializedSnapshotsModificationHistoryStack(snapshotsModificationHistoryStack);

			Dictionary<AccountId, List<Func<STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledStandardTransactions = specializedSnapshotsModificationHistoryStack.CompileStandardAccountHistorySets<STANDARD_ACCOUNT_SNAPSHOT_CONTEXT>(this.PrepareNewStandardAccountSnapshots, this.PrepareUpdateStandardAccountSnapshots, this.PrepareDeleteStandardAccountSnapshots);
			Dictionary<AccountId, List<Func<JOINT_ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledJointTransactions = specializedSnapshotsModificationHistoryStack.CompileJointAccountHistorySets<JOINT_ACCOUNT_SNAPSHOT_CONTEXT>(this.PrepareNewJointAccountSnapshots, this.PrepareUpdateJointAccountSnapshots, this.PrepareDeleteJointAccountSnapshots);
			Dictionary<AccountId, List<Func<STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledAccountKeysTransactions = specializedSnapshotsModificationHistoryStack.CompileStandardAccountKeysHistorySets<STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT>(this.PrepareNewAccountKeysSnapshots, this.PrepareUpdateAccountKeysSnapshots, this.PrepareDeleteAccountKeysSnapshots);
			Dictionary<long, List<Func<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledAccreditationCertificatesTransactions = specializedSnapshotsModificationHistoryStack.CompileAccreditationCertificatesHistorySets<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT>(this.PrepareNewAccreditationCertificatesSnapshots, this.PrepareUpdateAccreditationCertificatesSnapshots, this.PrepareDeleteAccreditationCertificatesSnapshots);
			Dictionary<long, List<Func<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledChainOptionsTransactions = specializedSnapshotsModificationHistoryStack.CompileChainOptionsHistorySets<CHAIN_OPTIONS_SNAPSHOT_CONTEXT>(this.PrepareNewChainOptionSnapshots, this.PrepareUpdateChainOptionSnapshots, this.PrepareDeleteChainOptionSnapshots);

			return this.RunCompiledTransactionSets(compiledStandardTransactions, compiledJointTransactions, compiledAccountKeysTransactions, compiledAccreditationCertificatesTransactions, compiledChainOptionsTransactions);

		}

		/// <summary>
		///     prepare any serialization tasks that need to be performed for our history sets
		/// </summary>
		/// <param name="snapshotsModificationHistoryStack"></param>
		/// <returns></returns>
		public List<Func<LockContext, Task>> PrepareKeysSerializationTasks(Dictionary<(AccountId accountId, byte ordinal), byte[]> keyDictionary) {
			List<Func<LockContext, Task>> serializationActions = new List<Func<LockContext, Task>>();

			BlockChainConfigurations configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(keyDictionary != null) {
				bool hasTransactions = configuration.EnabledKeyDictionaryTypes.HasFlag(ChainConfigurations.KeyDictionaryTypes.Transactions);
				bool hasMessages = configuration.EnabledKeyDictionaryTypes.HasFlag(ChainConfigurations.KeyDictionaryTypes.Messages);

				foreach(((AccountId accountId, byte ordinal), byte[] value) in keyDictionary) {
					if((accountId.SequenceId >= Constants.FIRST_PUBLIC_ACCOUNT_NUMBER) && (((ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) && hasTransactions) || ((ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) && hasMessages))) {

						serializationActions.Add(async lockContext => {

							using SafeArrayHandle publicKey = SafeArrayHandle.Wrap(value);

							ICryptographicKey cryptoKey = KeyFactory.RehydrateKey(DataSerializationFactory.CreateRehydrator(publicKey));

							if(cryptoKey is XmssCryptographicKey xmssCryptographicKey) {
								await this.centralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.SaveAccountKeyIndex(accountId, xmssCryptographicKey.PublicKey.Clone(), xmssCryptographicKey.TreeHeight, xmssCryptographicKey.HashType, xmssCryptographicKey.BackupHashType, ordinal, lockContext).ConfigureAwait(false);
							}
						});

					}
				}
			}

			return serializationActions;
		}

		public async Task<List<IAccountSnapshot>> LoadAccountSnapshots(List<AccountId> accountIds) {
			List<ACCOUNT_SNAPSHOT> results = new List<ACCOUNT_SNAPSHOT>();

			results.AddRange(await this.StandardAccountSnapshotsDal.LoadAccounts(accountIds.Where(a => a.IsStandard).ToList()).ConfigureAwait(false));
			results.AddRange(await this.JointAccountSnapshotsDal.LoadAccounts(accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).ToList()).ConfigureAwait(false));

			return results.Cast<IAccountSnapshot>().ToList();
		}

		public async Task<List<IAccountKeysSnapshot>> LoadStandardAccountKeysSnapshots(List<(long accountId, byte ordinal)> keys) {

			return (await this.AccountKeysSnapshotDal.LoadAccountKeys(db => {
					       List<string> casted = keys.Select(s => s.accountId + s.ordinal.ToString()).ToList();

					       return db.StandardAccountKeysSnapshots.Where(s => casted.Contains(s.CompositeKey)).ToListAsync();
				       }, keys).ConfigureAwait(false)).Cast<IAccountKeysSnapshot>().ToList();
		}

		public async Task<List<IAccreditationCertificateSnapshot>> LoadAccreditationCertificatesSnapshots(List<int> certificateIds) {

			return (await this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificates(db => {
					       return db.AccreditationCertificates.Where(s => certificateIds.Contains(s.CertificateId)).ToListAsync();
				       }, certificateIds).ConfigureAwait(false)).Cast<IAccreditationCertificateSnapshot>().ToList();
		}

		public async Task<IChainOptionsSnapshot> LoadChainOptionsSnapshot() {
			return await this.ChainOptionsSnapshotDal.LoadChainOptionsSnapshot(db => {
				return db.ChainOptionsSnapshots.Where(s => s.Id == 1).SingleOrDefaultAsync();
			}).ConfigureAwait(false);
		}

		public IStandardAccountSnapshot CreateNewStandardAccountSnapshots() {
			return new STANDARD_ACCOUNT_SNAPSHOT();
		}

		public IJointAccountSnapshot CreateNewJointAccountSnapshots() {
			return new JOINT_ACCOUNT_SNAPSHOT();
		}

		public IAccountKeysSnapshot CreateNewAccountKeySnapshots() {
			return new STANDARD_ACCOUNT_KEY_SNAPSHOT();
		}

		public IAccreditationCertificateSnapshot CreateNewAccreditationCertificateSnapshots() {
			return new ACCREDITATION_CERTIFICATE_SNAPSHOT();
		}

		public IChainOptionsSnapshot CreateNewChainOptionsSnapshots() {
			return new CHAIN_OPTIONS_SNAPSHOT();
		}

		public virtual async Task ClearSnapshots() {
			// clear everything, most probably a digest is upcomming
			await this.StandardAccountSnapshotsDal.Clear().ConfigureAwait(false);

			await this.JointAccountSnapshotsDal.Clear().ConfigureAwait(false);
			await this.AccountKeysSnapshotDal.Clear().ConfigureAwait(false);
			await this.AccreditationCertificateAccountSnapshotsDal.Clear().ConfigureAwait(false);
			await this.ChainOptionsSnapshotDal.Clear().ConfigureAwait(false);
		}

		public async Task UpdateSnapshotDigestFromDigest(IAccountSnapshotDigestChannelCard accountSnapshotDigestChannelCard) {
			if(accountSnapshotDigestChannelCard is IStandardAccountSnapshotDigestChannelCard standardAccountSnapshotDigestChannelCard) {
				STANDARD_ACCOUNT_SNAPSHOT entry = new STANDARD_ACCOUNT_SNAPSHOT();

				accountSnapshotDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

				await this.StandardAccountSnapshotsDal.UpdateSnapshotDigestFromDigest(async db => {

					STANDARD_ACCOUNT_SNAPSHOT result = await db.StandardAccountSnapshots.SingleOrDefaultAsync(c => c.AccountId == entry.AccountId).ConfigureAwait(false);

					if(result != null) {
						db.StandardAccountSnapshots.Remove(result);
						await db.SaveChangesAsync().ConfigureAwait(false);
					}

					db.StandardAccountSnapshots.Add(entry);
					await db.SaveChangesAsync().ConfigureAwait(false);
				}, entry).ConfigureAwait(false);

			} else if(accountSnapshotDigestChannelCard is IJointAccountSnapshotDigestChannelCard jointAccountSnapshotDigestChannelCard) {
				JOINT_ACCOUNT_SNAPSHOT entry = new JOINT_ACCOUNT_SNAPSHOT();

				accountSnapshotDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

				await this.JointAccountSnapshotsDal.UpdateSnapshotDigestFromDigest(async db => {

					JOINT_ACCOUNT_SNAPSHOT result = await db.JointAccountSnapshots.SingleOrDefaultAsync(c => c.AccountId == entry.AccountId).ConfigureAwait(false);

					if(result != null) {
						db.JointAccountSnapshots.Remove(result);
						await db.SaveChangesAsync().ConfigureAwait(false);
					}

					db.JointAccountSnapshots.Add(entry);
					await db.SaveChangesAsync().ConfigureAwait(false);
				}, entry).ConfigureAwait(false);
			}
		}

		public Task UpdateAccountKeysFromDigest(IStandardAccountKeysDigestChannelCard standardAccountKeysDigestChannelCard) {
			STANDARD_ACCOUNT_KEY_SNAPSHOT entry = new STANDARD_ACCOUNT_KEY_SNAPSHOT();

			standardAccountKeysDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

			if(string.IsNullOrWhiteSpace(entry.CompositeKey)) {
				entry.CompositeKey = this.GetCardUtils().GenerateCompositeKey(entry);
			}

			return this.AccountKeysSnapshotDal.UpdateSnapshotDigestFromDigest(async db => {

				STANDARD_ACCOUNT_KEY_SNAPSHOT result = await db.StandardAccountKeysSnapshots.SingleOrDefaultAsync(e => (e.AccountId == entry.AccountId) && (e.OrdinalId == entry.OrdinalId)).ConfigureAwait(false);

				if(result != null) {

					db.StandardAccountKeysSnapshots.Remove(result);
					await db.SaveChangesAsync().ConfigureAwait(false);
				}

				db.StandardAccountKeysSnapshots.Add(entry);
				await db.SaveChangesAsync().ConfigureAwait(false);
			}, entry);
		}

		public Task UpdateAccreditationCertificateFromDigest(IAccreditationCertificateDigestChannelCard accreditationCertificateDigestChannelCard) {
			ACCREDITATION_CERTIFICATE_SNAPSHOT entry = new ACCREDITATION_CERTIFICATE_SNAPSHOT();

			accreditationCertificateDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

			return this.AccreditationCertificateAccountSnapshotsDal.UpdateSnapshotDigestFromDigest(async db => {

				ACCREDITATION_CERTIFICATE_SNAPSHOT result = await db.AccreditationCertificates.SingleOrDefaultAsync(c => c.CertificateId == entry.CertificateId).ConfigureAwait(false);

				if(result != null) {
					db.AccreditationCertificates.Remove(result);
					await db.SaveChangesAsync().ConfigureAwait(false);
				}

				db.AccreditationCertificates.Add(entry);
				await db.SaveChangesAsync().ConfigureAwait(false);
			}, entry);
		}

		public Task UpdateChainOptionsFromDigest(IChainOptionsDigestChannelCard chainOptionsDigestChannelCard) {
			CHAIN_OPTIONS_SNAPSHOT entry = new CHAIN_OPTIONS_SNAPSHOT();

			chainOptionsDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

			return this.ChainOptionsSnapshotDal.UpdateSnapshotDigestFromDigest(async db => {

				CHAIN_OPTIONS_SNAPSHOT result = db.ChainOptionsSnapshots.SingleOrDefault(c => c.Id == entry.Id);

				if(result != null) {
					db.ChainOptionsSnapshots.Remove(result);
					await db.SaveChangesAsync().ConfigureAwait(false);
				}

				db.ChainOptionsSnapshots.Add(entry);
				await db.SaveChangesAsync().ConfigureAwait(false);
			});
		}

		public void EnsureChainOptionsCreated() {
			this.ChainOptionsSnapshotDal.EnsureEntryCreated(async db => {

				CHAIN_OPTIONS_SNAPSHOT state = await db.ChainOptionsSnapshots.SingleOrDefaultAsync().ConfigureAwait(false);

				// make sure there is a single and unique state
				if(state == null) {
					state = new CHAIN_OPTIONS_SNAPSHOT();
					db.ChainOptionsSnapshots.Add(state);
					await db.SaveChangesAsync().ConfigureAwait(false);
				}
			});
		}

		protected TEntity QueryDbSetEntityEntry<TEntity>(DbSet<TEntity> dbSet, Expression<Func<TEntity, bool>> predicate)
			where TEntity : class {

			return dbSet.Local.SingleOrDefault(predicate.Compile()) ?? dbSet.SingleOrDefault(predicate);
		}

		protected List<TEntity> QueryDbSetEntityEntries<TEntity>(DbSet<TEntity> dbSet, Expression<Func<TEntity, bool>> predicate)
			where TEntity : class {

			List<TEntity> results = dbSet.Local.Where(predicate.Compile()).ToList();

			if(!results.Any()) {
				results = dbSet.Where(predicate.Compile()).ToList();
			}

			return results;
		}

		protected ISnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> GetSpecializedSnapshotsModificationHistoryStack(ISnapshotHistoryStackSet snapshotsModificationHistoryStack) {
			return (ISnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>) snapshotsModificationHistoryStack;

		}

		protected virtual async Task RunCompiledTransactionSets(Dictionary<AccountId, List<Func<STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledStandardTransactions, Dictionary<AccountId, List<Func<JOINT_ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledJointTransactions, Dictionary<AccountId, List<Func<STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledAccountKeysTransactions, Dictionary<long, List<Func<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledAccreditationCertificatesTransactions, Dictionary<long, List<Func<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, LockContext, Task>>> compiledChainOptionsTransactions) {

			List<(STANDARD_ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> standardTransactions = null;
			List<(JOINT_ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> jointTransactions = null;
			List<(STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> accountKeysTransactions = null;
			List<(ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> accreditationCertificatesTransactions = null;
			List<(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> chainOptionsTransactions = null;

			try {
				if(compiledStandardTransactions != null) {
					standardTransactions = await this.StandardAccountSnapshotsDal.PerformProcessingSet(compiledStandardTransactions).ConfigureAwait(false);
				}

				if(compiledJointTransactions != null) {
					jointTransactions = await this.JointAccountSnapshotsDal.PerformProcessingSet(compiledJointTransactions).ConfigureAwait(false);
				}

				if(compiledAccountKeysTransactions != null) {
					accountKeysTransactions = await this.AccountKeysSnapshotDal.PerformProcessingSet(compiledAccountKeysTransactions).ConfigureAwait(false);
				}

				if(compiledAccreditationCertificatesTransactions != null) {
					accreditationCertificatesTransactions = await this.AccreditationCertificateAccountSnapshotsDal.PerformProcessingSet(compiledAccreditationCertificatesTransactions).ConfigureAwait(false);
				}

				if(compiledChainOptionsTransactions != null) {
					chainOptionsTransactions = await this.ChainOptionsSnapshotDal.PerformProcessingSet(compiledChainOptionsTransactions).ConfigureAwait(false);
				}

				async Task SaveChanges(List<(DbContext db, IDbContextTransaction transaction)> transactions) {
					if(transactions != null) {
						foreach((DbContext db, IDbContextTransaction transaction) entry in transactions) {
							await entry.db.SaveChangesAsync().ConfigureAwait(false);
						}
					}
				}

				void CommitTransactions(List<(DbContext db, IDbContextTransaction transaction)> transactions) {
					if(transactions != null) {
						foreach((DbContext db, IDbContextTransaction transaction) entry in transactions) {
							entry.transaction.Commit();
							entry.db.Dispose();
						}
					}
				}

				List<(DbContext, IDbContextTransaction transaction)> simple = standardTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();
				List<(DbContext, IDbContextTransaction transaction)> joint = jointTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();
				List<(DbContext, IDbContextTransaction transaction)> keys = accountKeysTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();
				List<(DbContext, IDbContextTransaction transaction)> certificates = accreditationCertificatesTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();
				List<(DbContext, IDbContextTransaction transaction)> options = chainOptionsTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();

				await SaveChanges(simple).ConfigureAwait(false);
				await SaveChanges(joint).ConfigureAwait(false);
				await SaveChanges(keys).ConfigureAwait(false);
				await SaveChanges(certificates).ConfigureAwait(false);
				await SaveChanges(options).ConfigureAwait(false);

				CommitTransactions(simple);
				CommitTransactions(joint);
				CommitTransactions(keys);
				CommitTransactions(certificates);
				CommitTransactions(options);

			} catch {

				void ClearTransactions(List<(DbContext db, IDbContextTransaction transaction)> transactions) {
					if(transactions != null) {
						foreach((DbContext db, IDbContextTransaction transaction) entry in transactions) {
							try {
								entry.transaction?.Rollback();
							} catch {
							}

							try {
								entry.db?.Dispose();
							} catch {
							}
						}
					}
				}

				ClearTransactions(standardTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList());
				ClearTransactions(jointTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList());
				ClearTransactions(accountKeysTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList());
				ClearTransactions(accreditationCertificatesTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList());
				ClearTransactions(chainOptionsTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList());

				throw;
			}

		}

		/// <summary>
		///     Here we determine if an account entry is "barebones", or if it has no special value.
		/// </summary>
		/// <param name="accountSnapshotEntry"></param>
		/// <returns></returns>
		public virtual bool IsAccountEntryNull(ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			if(!accountSnapshotEntry.CollectionCopy.Any()) {
				return true;
			}

			//TODO: define this
			return false;
		}

		public Task UpdateSnapshotEntry(STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			// any account that is barebones, we delete to save space. 
			bool isNullEntry = this.IsAccountEntryNull(accountSnapshotEntry);

			return this.StandardAccountSnapshotsDal.UpdateSnapshotEntry(db => {
				STANDARD_ACCOUNT_SNAPSHOT dbEntry = db.StandardAccountSnapshots.SingleOrDefault(a => a.AccountId == accountSnapshotEntry.AccountId);

				if(isNullEntry && (dbEntry != null)) {
					db.StandardAccountSnapshots.Remove(accountSnapshotEntry);
				} else {
					if(dbEntry == null) {
						db.StandardAccountSnapshots.Add(accountSnapshotEntry);
					} else {
						this.centralCoordinator.ChainComponentProvider.CardUtils.Copy(accountSnapshotEntry, dbEntry);

					}
				}

				return db.SaveChangesAsync();
			}, accountSnapshotEntry);
		}

		public Task UpdateSnapshotEntry(JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			// any account that is barebones, we delete to save space. 
			bool isNullEntry = this.IsAccountEntryNull(accountSnapshotEntry);

			return this.JointAccountSnapshotsDal.UpdateSnapshotEntry(db => {
				JOINT_ACCOUNT_SNAPSHOT dbEntry = db.JointAccountSnapshots.SingleOrDefault(a => a.AccountId == accountSnapshotEntry.AccountId);

				if(isNullEntry && (dbEntry != null)) {
					db.JointAccountSnapshots.Remove(accountSnapshotEntry);
				} else {
					if(dbEntry == null) {
						db.JointAccountSnapshots.Add(accountSnapshotEntry);
					} else {
						this.centralCoordinator.ChainComponentProvider.CardUtils.Copy(accountSnapshotEntry, dbEntry);

					}
				}

				return db.SaveChangesAsync();
			}, accountSnapshotEntry);

		}

		protected abstract ICardUtils GetCardUtils();

	#region DALs

		protected const string STANDARD_ACCOUNTS_SNAPSHOTS_DIRECTORY = "standard-accounts-snapshots";
		protected const string JOINT_ACCOUNTS_SNAPSHOTS_DIRECTORY = "joint-accounts-snapshots";

		protected const string ACCREDITATION_CERTIFICATE_SNAPSHOTS_DIRECTORY = "accreditation-certificates-snapshots";
		protected const string STANDARD_ACCIYBT_KEY_SNAPSHOTS_DIRECTORY = "standard-account-keys-snapshots";
		protected const string CHAIN_OPTIONS_SNAPSHOTS_DIRECTORY = "chain-options-snapshots";
		protected const string TRANACTION_ACCOUNTS_DIRECTORY = "tracked-accounts";

		public string GetStandardAccountSnapshotsPath() {
			return Path.Combine(this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), STANDARD_ACCOUNTS_SNAPSHOTS_DIRECTORY);
		}

		public string GetJointAccountSnapshotsPath() {
			return Path.Combine(this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), JOINT_ACCOUNTS_SNAPSHOTS_DIRECTORY);
		}

		public string GetAccreditationCertificateAccountPath() {
			return Path.Combine(this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), ACCREDITATION_CERTIFICATE_SNAPSHOTS_DIRECTORY);
		}

		public string GetStandardAccountKeysSnapshotsPath() {
			return Path.Combine(this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), STANDARD_ACCIYBT_KEY_SNAPSHOTS_DIRECTORY);
		}

		public string GetChainOptionsSnapshotPath() {
			return Path.Combine(this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), CHAIN_OPTIONS_SNAPSHOTS_DIRECTORY);
		}

		public string GetTrackedAccountsPath() {
			return Path.Combine(this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), TRANACTION_ACCOUNTS_DIRECTORY);
		}

		private STANDARD_ACCOUNT_SNAPSHOT_DAL standardAccountSnapshotDal;

		protected STANDARD_ACCOUNT_SNAPSHOT_DAL StandardAccountSnapshotsDal {
			get {
				lock(this.locker) {
					if(this.standardAccountSnapshotDal == null) {
						this.standardAccountSnapshotDal = this.centralCoordinator.ChainDalCreationFactory.CreateStandardAccountSnapshotDal<STANDARD_ACCOUNT_SNAPSHOT_DAL>(GROUP_SIZE, this.GetStandardAccountSnapshotsPath(), this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);
					}
				}

				return this.standardAccountSnapshotDal;
			}
		}

		private JOINT_ACCOUNT_SNAPSHOT_DAL jointAccountSnapshotDal;

		protected JOINT_ACCOUNT_SNAPSHOT_DAL JointAccountSnapshotsDal {
			get {
				lock(this.locker) {
					if(this.jointAccountSnapshotDal == null) {
						this.jointAccountSnapshotDal = this.centralCoordinator.ChainDalCreationFactory.CreateJointAccountSnapshotDal<JOINT_ACCOUNT_SNAPSHOT_DAL>(GROUP_SIZE, this.GetJointAccountSnapshotsPath(), this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);
					}
				}

				return this.jointAccountSnapshotDal;
			}
		}

		private ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL accreditationCertificateAccountSnapshotsDal;

		protected ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL AccreditationCertificateAccountSnapshotsDal {
			get {
				lock(this.locker) {
					if(this.accreditationCertificateAccountSnapshotsDal == null) {
						this.accreditationCertificateAccountSnapshotsDal = this.centralCoordinator.ChainDalCreationFactory.CreateAccreditationCertificateAccountSnapshotDal<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL>(GROUP_SIZE, this.GetAccreditationCertificateAccountPath(), this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);
					}
				}

				return this.accreditationCertificateAccountSnapshotsDal;
			}
		}

		private STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL accountKeysSnapshotDal;

		protected STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL AccountKeysSnapshotDal {
			get {
				lock(this.locker) {
					if(this.accountKeysSnapshotDal == null) {
						this.accountKeysSnapshotDal = this.centralCoordinator.ChainDalCreationFactory.CreateStandardAccountKeysSnapshotDal<STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL>(ACCOUNT_KEYS_GROUP_SIZE, this.GetStandardAccountKeysSnapshotsPath(), this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);
					}
				}

				return this.accountKeysSnapshotDal;
			}
		}

		private CHAIN_OPTIONS_SNAPSHOT_DAL chainOptionsSnapshotDal;

		protected CHAIN_OPTIONS_SNAPSHOT_DAL ChainOptionsSnapshotDal {
			get {
				lock(this.locker) {
					if(this.chainOptionsSnapshotDal == null) {
						this.chainOptionsSnapshotDal = this.centralCoordinator.ChainDalCreationFactory.CreateChainOptionsSnapshotDal<CHAIN_OPTIONS_SNAPSHOT_DAL>(this.GetChainOptionsSnapshotPath(), this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);

					}
				}

				return this.chainOptionsSnapshotDal;
			}
		}

		private TRACKED_ACCOUNTS_DAL trackedAccountsDal;

		protected TRACKED_ACCOUNTS_DAL TrackedAccountsDal {
			get {
				lock(this.locker) {
					if(this.trackedAccountsDal == null) {
						this.trackedAccountsDal = this.centralCoordinator.ChainDalCreationFactory.CreateTrackedAccountsDal<TRACKED_ACCOUNTS_DAL>(GROUP_SIZE, this.GetTrackedAccountsPath(), this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);
					}
				}

				return this.trackedAccountsDal;
			}
		}

	#endregion

	#region snapshot operations

		protected virtual Task<STANDARD_ACCOUNT_SNAPSHOT> PrepareNewStandardAccountSnapshots(STANDARD_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, AccountId temporaryHashId, IStandardAccountSnapshot source, LockContext lockContext) {
			
			STANDARD_ACCOUNT_SNAPSHOT snapshot = new STANDARD_ACCOUNT_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.StandardAccountSnapshots.Add(snapshot);

			foreach(STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT attribute in snapshot.AppliedAttributes) {
				db.StandardAccountSnapshotAttributes.Add(attribute);
			}

			return Task.FromResult(snapshot);
		}

		protected void UpdateFeatures<ACCOUNT_SNAPSHOT, ACCOUNT_ATTRIBUTE_SNAPSHOT>(ACCOUNT_SNAPSHOT snapshot, AccountId accountId, DbSet<ACCOUNT_ATTRIBUTE_SNAPSHOT> features)
			where ACCOUNT_SNAPSHOT : class, IAccountSnapshotEntry<ACCOUNT_ATTRIBUTE_SNAPSHOT>
			where ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry {

			List<ACCOUNT_ATTRIBUTE_SNAPSHOT> existingAttributes = this.QueryDbSetEntityEntries(features, a => a.AccountId == accountId.ToLongRepresentation());

			List<ACCOUNT_ATTRIBUTE_SNAPSHOT> snapshotAttributes = snapshot.AppliedAttributes.ToList();

			List<(uint CorrelationId, AccountAttributeType FeatureType, long AccountId)> certificateIds = existingAttributes.Select(a => (a.CorrelationId, FeatureType: a.AttributeType, a.AccountId)).ToList();
			List<(uint CorrelationId, AccountAttributeType FeatureType, long AccountId)> snapshotCertificates = snapshotAttributes.Select(a => (a.CorrelationId, FeatureType: a.AttributeType, a.AccountId)).ToList();

			// build the delta
			List<ACCOUNT_ATTRIBUTE_SNAPSHOT> newAttributes = snapshotAttributes.Where(a => !certificateIds.Contains((a.CorrelationId, a.AttributeType, a.AccountId))).ToList();
			List<ACCOUNT_ATTRIBUTE_SNAPSHOT> modifyAttributes = snapshotAttributes.Where(a => certificateIds.Contains((a.CorrelationId, a.AttributeType, a.AccountId))).ToList();
			List<ACCOUNT_ATTRIBUTE_SNAPSHOT> removeAttributes = existingAttributes.Where(a => !snapshotCertificates.Contains((a.CorrelationId, a.AttributeType, a.AccountId))).ToList();

			foreach(ACCOUNT_ATTRIBUTE_SNAPSHOT attribute in newAttributes) {
				features.Add(attribute);
			}

			foreach(ACCOUNT_ATTRIBUTE_SNAPSHOT attribute in modifyAttributes) {

				ACCOUNT_ATTRIBUTE_SNAPSHOT dbEntry = existingAttributes.Single(a => (a.CorrelationId == attribute.CorrelationId) && (a.AttributeType == attribute.AttributeType) && (a.AccountId == attribute.AccountId));
				this.CardUtils.Copy(attribute, dbEntry);
			}

			foreach(ACCOUNT_ATTRIBUTE_SNAPSHOT attribute in removeAttributes) {
				features.Remove(attribute);
			}
		}

		protected virtual Task<STANDARD_ACCOUNT_SNAPSHOT> PrepareUpdateStandardAccountSnapshots(STANDARD_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, IStandardAccountSnapshot source, LockContext lockContext) {
			STANDARD_ACCOUNT_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.StandardAccountSnapshots, a => a.AccountId == accountId.ToLongRepresentation());
			this.CardUtils.Copy(source, snapshot);

			this.UpdateFeatures(snapshot, accountId, db.StandardAccountSnapshotAttributes);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<STANDARD_ACCOUNT_SNAPSHOT> PrepareDeleteStandardAccountSnapshots(STANDARD_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, LockContext lockContext) {
			STANDARD_ACCOUNT_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.StandardAccountSnapshots, a => a.AccountId == accountId.ToLongRepresentation());
			db.StandardAccountSnapshots.Remove(snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<JOINT_ACCOUNT_SNAPSHOT> PrepareNewJointAccountSnapshots(JOINT_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, AccountId temporaryHashId, IJointAccountSnapshot source, LockContext lockContext) {
			JOINT_ACCOUNT_SNAPSHOT snapshot = new JOINT_ACCOUNT_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.JointAccountSnapshots.Add(snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<JOINT_ACCOUNT_SNAPSHOT> PrepareUpdateJointAccountSnapshots(JOINT_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, IJointAccountSnapshot source, LockContext lockContext) {
			JOINT_ACCOUNT_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.JointAccountSnapshots, a => a.AccountId == accountId.ToLongRepresentation());
			this.CardUtils.Copy(source, snapshot);

			this.UpdateFeatures(snapshot, accountId, db.JointAccountSnapshotAttributes);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<JOINT_ACCOUNT_SNAPSHOT> PrepareDeleteJointAccountSnapshots(JOINT_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, LockContext lockContext) {
			JOINT_ACCOUNT_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.JointAccountSnapshots, a => a.AccountId == accountId.ToLongRepresentation());
			db.JointAccountSnapshots.Remove(snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> PrepareNewAccountKeysSnapshots(STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, (long AccountId, byte OrdinalId) key, IAccountKeysSnapshot source, LockContext lockContext) {
			STANDARD_ACCOUNT_KEY_SNAPSHOT snapshot = new STANDARD_ACCOUNT_KEY_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.StandardAccountKeysSnapshots.Add(snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> PrepareUpdateAccountKeysSnapshots(STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, (long AccountId, byte OrdinalId) key, IAccountKeysSnapshot source, LockContext lockContext) {
			STANDARD_ACCOUNT_KEY_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.StandardAccountKeysSnapshots, a => (a.AccountId == key.AccountId) && (a.OrdinalId == key.OrdinalId));
			this.CardUtils.Copy(source, snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<STANDARD_ACCOUNT_KEY_SNAPSHOT> PrepareDeleteAccountKeysSnapshots(STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, (long AccountId, byte OrdinalId) key, LockContext lockContext) {

			STANDARD_ACCOUNT_KEY_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.StandardAccountKeysSnapshots, a => (a.AccountId == key.AccountId) && (a.OrdinalId == key.OrdinalId));
			db.StandardAccountKeysSnapshots.Remove(snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> PrepareNewAccreditationCertificatesSnapshots(ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT db, int certificateId, IAccreditationCertificateSnapshot source, LockContext lockContext) {
			ACCREDITATION_CERTIFICATE_SNAPSHOT snapshot = new ACCREDITATION_CERTIFICATE_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.AccreditationCertificates.Add(snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> PrepareUpdateAccreditationCertificatesSnapshots(ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT db, int certificateId, IAccreditationCertificateSnapshot source, LockContext lockContext) {
			ACCREDITATION_CERTIFICATE_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.AccreditationCertificates, a => a.CertificateId == certificateId);
			this.CardUtils.Copy(source, snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> PrepareDeleteAccreditationCertificatesSnapshots(ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT db, int certificateId, LockContext lockContext) {
			ACCREDITATION_CERTIFICATE_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.AccreditationCertificates, a => a.CertificateId == certificateId);
			db.AccreditationCertificates.Remove(snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<CHAIN_OPTIONS_SNAPSHOT> PrepareNewChainOptionSnapshots(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, int id, IChainOptionsSnapshot source, LockContext lockContext) {
			CHAIN_OPTIONS_SNAPSHOT snapshot = new CHAIN_OPTIONS_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.ChainOptionsSnapshots.Add(snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<CHAIN_OPTIONS_SNAPSHOT> PrepareUpdateChainOptionSnapshots(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, int id, IChainOptionsSnapshot source, LockContext lockContext) {
			CHAIN_OPTIONS_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.ChainOptionsSnapshots, a => a.Id == id);
			this.CardUtils.Copy(source, snapshot);

			return Task.FromResult(snapshot);
		}

		protected virtual Task<CHAIN_OPTIONS_SNAPSHOT> PrepareDeleteChainOptionSnapshots(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, int id, LockContext lockContext) {
			CHAIN_OPTIONS_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.ChainOptionsSnapshots, a => a.Id == id);
			db.ChainOptionsSnapshots.Remove(snapshot);

			return Task.FromResult(snapshot);
		}

	#endregion

	}
}