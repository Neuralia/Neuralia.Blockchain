using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IAccountSnapshotsProvider {

		//		void InsertModeratorKey(TransactionId transactionId, byte keyId, ArrayWrapper key);
		//		void UpdateModeratorKey(TransactionId transactionId, byte keyId, ArrayWrapper key);
		//		ArrayWrapper GetModeratorKey(byte keyId);
		//		Enums.ChainSyncState GetChainSyncState();
		bool AnyAccountTracked(List<AccountId> accountId);
		bool AnyAccountTracked();
		List<AccountId> AccountsTracked(List<AccountId> accountId);

		void StartTrackingAccounts(List<AccountId> accountIds);
		void StartTrackingConfigAccounts();

		bool IsAccountTracked(AccountId accountId);
		bool IsAccountTracked(long accountSequenceId, Enums.AccountTypes accountType);

		void UpdateSnapshotDigestFromDigest(IAccountSnapshotDigestChannelCard accountSnapshotDigestChannelCard);
		void UpdateAccountKeysFromDigest(IStandardAccountKeysDigestChannelCard standardAccountKeysDigestChannelCard);
		void UpdateAccreditationCertificateFromDigest(IAccreditationCertificateDigestChannelCard accreditationCertificateDigestChannelCard);
		void UpdateChainOptionsFromDigest(IChainOptionsDigestChannelCard chainOptionsDigestChannelCard);

		void ClearSnapshots();

		List<IAccountSnapshot> LoadAccountSnapshots(List<AccountId> accountIds);
		List<IAccountKeysSnapshot> LoadStandardAccountKeysSnapshots(List<(long accountId, byte ordinal)> keys);
		List<IChainOptionsSnapshot> LoadChainOptionsSnapshots(List<int> ids);
		List<IAccreditationCertificateSnapshot> LoadAccreditationCertificatesSnapshots(List<int> certificateIds);

		IStandardAccountSnapshot CreateNewStandardAccountSnapshots();
		IJointAccountSnapshot CreateNewJointAccountSnapshots();
		IAccountKeysSnapshot CreateNewAccountKeySnapshots();
		IAccreditationCertificateSnapshot CreateNewAccreditationCertificateSnapshots();
		IChainOptionsSnapshot CreateNewChainOptionsSnapshots();

		void ProcessSnapshotImpacts(ISnapshotHistoryStackSet snapshotsModificationHistoryStack);
		List<Action<ISerializationManager, TaskRoutingContext>> PrepareKeysSerializationTasks(Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys);
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

		List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(ImmutableList<AccountId> accountIds, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType);
		List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(ImmutableList<AccountId> accountIds, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType);
		List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType);
		List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType);
		ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificate(int certificateId, AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType);
		ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificate(int certificateId, AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType);

		List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(List<int> certificateIds, AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType);
		List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(List<int> certificateIds, AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType);
	}

	public interface IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<STANDARD_ACCOUNT_FEATURE_SNAPSHOT>, new()
		where STANDARD_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureEntry, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where JOINT_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshotEntry, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotEntry, new() {
	}

	public interface IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_DAL, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_DAL, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_DAL, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_DAL, TRACKED_ACCOUNTS_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where STANDARD_ACCOUNT_SNAPSHOT_DAL : class, IStandardAccountSnapshotDal<STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IStandardAccountSnapshotContext<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_DAL : class, IJointAccountSnapshotDal<JOINT_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IJointAccountSnapshotContext<JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL : class, IAccreditationCertificatesSnapshotDal<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL : class, IAccountKeysSnapshotDal<STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT : DbContext, IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_DAL : class, IChainOptionsSnapshotDal<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_CONTEXT : DbContext, IChainOptionsSnapshotContext<CHAIN_OPTIONS_SNAPSHOT>
		where TRACKED_ACCOUNTS_DAL : class, ITrackedAccountsDal
		where TRACKED_ACCOUNTS_CONTEXT : DbContext, ITrackedAccountsContext
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<STANDARD_ACCOUNT_FEATURE_SNAPSHOT>, new()
		where STANDARD_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureEntry, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where JOINT_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureEntry, new()
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
	public abstract class AccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_DAL, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_DAL, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_DAL, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_DAL, TRACKED_ACCOUNTS_CONTEXT, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_DAL, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_DAL, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_DAL, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_DAL, TRACKED_ACCOUNTS_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where STANDARD_ACCOUNT_SNAPSHOT_DAL : class, IStandardAccountSnapshotDal<STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IStandardAccountSnapshotContext<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_DAL : class, IJointAccountSnapshotDal<JOINT_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IJointAccountSnapshotContext<JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_DAL : class, IAccreditationCertificatesSnapshotDal<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT : DbContext, IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_DAL : class, IAccountKeysSnapshotDal<STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT : DbContext, IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_DAL : class, IChainOptionsSnapshotDal<CHAIN_OPTIONS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_CONTEXT : DbContext, IChainOptionsSnapshotContext<CHAIN_OPTIONS_SNAPSHOT>
		where TRACKED_ACCOUNTS_DAL : class, ITrackedAccountsDal<TRACKED_ACCOUNTS_CONTEXT>
		where TRACKED_ACCOUNTS_CONTEXT : DbContext, ITrackedAccountsContext
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<STANDARD_ACCOUNT_FEATURE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureEntry, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureEntry, new()
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

		public bool IsAccountTracked(AccountId accountId) {
			return this.IsAccountTracked(accountId.SequenceId, accountId.AccountType);
		}

		public ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificate(int certificateId, AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType) {
			return this.GetAccreditationCertificate(certificateId, accountId, new[] {certificateType}, applicationType);
		}

		public ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificate(int certificateId, AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType) {

			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificate(db => {

				var certificateTypeValues = certificateTypes.Select(c => c.Value).ToList();

				// the the account is valid in the certificate
				if(!db.AccreditationCertificateAccounts.Any(c => (c.CertificateId == certificateId) && (c.AccountId == accountId.ToLongRepresentation()))) {
					return null;
				}

				// ok, the account is in the certificate, lets select it itself
				return db.AccreditationCertificates.SingleOrDefault(c => (c.CertificateId == certificateId) && certificateTypeValues.Contains(c.CertificateType.Value) && c.ApplicationType.HasFlag(applicationType));
			}, certificateId);
		}

		public List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(List<int> certificateIds, AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType) {
			return this.GetAccreditationCertificate(certificateIds, accountId, new[] {certificateType}, applicationType);
		}

		public List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(List<int> certificateIds, AccountId accountId, AccreditationCertificateType[] certificateTypes, Enums.CertificateApplicationTypes applicationType) {

			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificates(db => {

				var certificateTypeValues = certificateTypes.Select(c => c.Value).ToList();

				// the the account is valid in the certificate
				if(!db.AccreditationCertificateAccounts.Any(c => certificateIds.Contains(c.CertificateId) && (c.AccountId == accountId.ToLongRepresentation()))) {
					return null;
				}

				// ok, the account is in the certificate, lets select it itself
				return db.AccreditationCertificates.Where(c => certificateIds.Contains(c.CertificateId) && certificateTypeValues.Contains(c.CertificateType.Value) && c.ApplicationType.HasFlag(applicationType)).ToList();
			}, certificateIds);
		}

		public List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(AccountId accountId, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType) {
			return this.GetAccreditationCertificates(accountId, new[] {certificateType}, applicationType);
		}

		public List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(AccountId accountId, AccreditationCertificateType[] certificateType, Enums.CertificateApplicationTypes applicationType) {

			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificates(db => {
				var certificateTypeValues = certificateType.Select(c => c.Value).ToList();

				// the the account is valid in the certificate
				var containsCertificates = db.AccreditationCertificateAccounts.Where(c => c.AccountId == accountId.ToLongRepresentation()).Select(c => c.CertificateId).ToList();

				// ok, the account is in the certificate, lets select it itself
				return db.AccreditationCertificates.Where(c => containsCertificates.Contains(c.CertificateId) && certificateTypeValues.Contains(c.CertificateType.Value) && c.ApplicationType.HasFlag(applicationType)).ToList();
			});
		}

		public List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(ImmutableList<AccountId> accountIds, AccreditationCertificateType certificateType, Enums.CertificateApplicationTypes applicationType) {
			return this.GetAccreditationCertificates(accountIds, new[] {certificateType}, applicationType);
		}

		public List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(ImmutableList<AccountId> accountIds, AccreditationCertificateType[] certificateType, Enums.CertificateApplicationTypes applicationType) {

			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificates(db => {
				var certificateTypeValues = certificateType.Select(c => c.Value).ToList();

				return db.AccreditationCertificates.Where(c => (c.AssignedAccount != 0) && accountIds.Contains(c.AssignedAccount.ToAccountId()) && certificateTypeValues.Contains(c.CertificateType.Value) && c.ApplicationType.HasFlag(applicationType)).ToList();
			});
		}

		public void StartTrackingConfigAccounts() {
			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			this.StartTrackingAccounts(chainConfiguration.TrackedSnapshotAccountsList.Select(a => new AccountId(a.accountId, a.accountType)).ToList());
		}

		public void StartTrackingAccounts(List<AccountId> accountIds) {
			this.TrackedAccountsDal.AddTrackedAccounts(accountIds);
		}

		public bool AnyAccountTracked() {
			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.None) {
				return false;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.All) {
				return true;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.List) {
				return this.TrackedAccountsDal.AnyAccountsTracked();
			}

			return false;
		}

		public bool AnyAccountTracked(List<AccountId> accountIds) {
			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.None) {
				return false;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.All) {
				return true;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.List) {
				return this.TrackedAccountsDal.AnyAccountsTracked(accountIds);
			}

			return false;
		}

		public List<AccountId> AccountsTracked(List<AccountId> accountIds) {
			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.None) {
				return new List<AccountId>();
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.All) {
				return accountIds;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.List) {

				return this.TrackedAccountsDal.GetTrackedAccounts(accountIds);
			}

			return new List<AccountId>();
		}

		/// <summary>
		///     Determine if we are tracking a certain account to maintain the snapshots.
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public virtual bool IsAccountTracked(long accountSequenceId, Enums.AccountTypes accountType) {

			ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.None) {
				return false;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.All) {
				return true;
			}

			if(chainConfiguration.AccountSnapshotTrackingMethod == AppSettingsBase.SnapshotIndexTypes.List) {

				return this.TrackedAccountsDal.IsAccountTracked(new AccountId(accountSequenceId, accountType));
			}

			return false;
		}

		/// <summary>
		///     this is an important method where we commit all stacks of changes to the snapshot databases. transactional of
		///     course
		/// </summary>
		/// <param name="snapshotsModificationHistoryStack"></param>
		/// <returns></returns>
		public virtual void ProcessSnapshotImpacts(ISnapshotHistoryStackSet snapshotsModificationHistoryStack) {

			var specializedSnapshotsModificationHistoryStack = this.GetSpecializedSnapshotsModificationHistoryStack(snapshotsModificationHistoryStack);

			var compiledSimpleTransactions = specializedSnapshotsModificationHistoryStack.CompileStandardAccountHistorySets<STANDARD_ACCOUNT_SNAPSHOT_CONTEXT>(this.PrepareNewStandardAccountSnapshots, this.PrepareUpdateStandardAccountSnapshots, this.PrepareDeleteStandardAccountSnapshots);
			var compiledJointTransactions = specializedSnapshotsModificationHistoryStack.CompileJointAccountHistorySets<JOINT_ACCOUNT_SNAPSHOT_CONTEXT>(this.PrepareNewJointAccountSnapshots, this.PrepareUpdateJointAccountSnapshots, this.PrepareDeleteJointAccountSnapshots);
			var compiledAccountKeysTransactions = specializedSnapshotsModificationHistoryStack.CompileStandardAccountKeysHistorySets<STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT>(this.PrepareNewAccountKeysSnapshots, this.PrepareUpdateAccountKeysSnapshots, this.PrepareDeleteAccountKeysSnapshots);
			var compiledAccreditationCertificatesTransactions = specializedSnapshotsModificationHistoryStack.CompileAccreditationCertificatesHistorySets<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT>(this.PrepareNewAccreditationCertificatesSnapshots, this.PrepareUpdateAccreditationCertificatesSnapshots, this.PrepareDeleteAccreditationCertificatesSnapshots);
			var compiledChainOptionsTransactions = specializedSnapshotsModificationHistoryStack.CompileChainOptionsHistorySets<CHAIN_OPTIONS_SNAPSHOT_CONTEXT>(this.PrepareNewChainOptionSnapshots, this.PrepareUpdateChainOptionSnapshots, this.PrepareDeleteChainOptionSnapshots);

			this.RunCompiledTransactionSets(compiledSimpleTransactions, compiledJointTransactions, compiledAccountKeysTransactions, compiledAccreditationCertificatesTransactions, compiledChainOptionsTransactions);

		}

		/// <summary>
		///     prepare any serialization tasks that need to be performed for our history sets
		/// </summary>
		/// <param name="snapshotsModificationHistoryStack"></param>
		/// <returns></returns>
		public List<Action<ISerializationManager, TaskRoutingContext>> PrepareKeysSerializationTasks(Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys) {
			var serializationActions = new List<Action<ISerializationManager, TaskRoutingContext>>();

			BlockChainConfigurations configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(fastKeys != null) {
				bool hasTransactions = configuration.EnabledFastKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Transactions);
				bool hasMessages = configuration.EnabledFastKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Messages);

				foreach(var keyEntry in fastKeys) {
					if((keyEntry.Key.accountId.SequenceId >= Constants.FIRST_PUBLIC_ACCOUNT_NUMBER) && (((keyEntry.Key.ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) && hasTransactions) || ((keyEntry.Key.ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) && hasMessages))) {

						serializationActions.Add((manager, taskContext) => {

							ByteArray publicKey = keyEntry.Value;

							ICryptographicKey cryptoKey = KeyFactory.RehydrateKey(DataSerializationFactory.CreateRehydrator(publicKey));

							if(cryptoKey is XmssCryptographicKey xmssCryptographicKey) {
								manager.SaveAccountKeyIndex(keyEntry.Key.accountId, xmssCryptographicKey.Key, xmssCryptographicKey.TreeHeight, xmssCryptographicKey.BitSize, keyEntry.Key.ordinal);
							}
						});

					}
				}
			}

			return serializationActions;
		}

		public List<IAccountSnapshot> LoadAccountSnapshots(List<AccountId> accountIds) {
			var results = new List<ACCOUNT_SNAPSHOT>();

			results.AddRange(this.StandardAccountSnapshotsDal.LoadAccounts(accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard).ToList()));
			results.AddRange(this.JointAccountSnapshotsDal.LoadAccounts(accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).ToList()));

			return results.Cast<IAccountSnapshot>().ToList();
		}

		public List<IAccountKeysSnapshot> LoadStandardAccountKeysSnapshots(List<(long accountId, byte ordinal)> keys) {

			return this.AccountKeysSnapshotDal.LoadAccountKeys(db => {
				var casted = keys.Select(s => new Tuple<long, byte>(s.accountId, s.ordinal)).ToList();

				return db.StandardAccountkeysSnapshots.Where(s => casted.Contains(new Tuple<long, byte>(s.AccountId, s.OrdinalId))).ToList();
			}, keys).Cast<IAccountKeysSnapshot>().ToList();
		}

		public List<IAccreditationCertificateSnapshot> LoadAccreditationCertificatesSnapshots(List<int> certificateIds) {

			return this.AccreditationCertificateAccountSnapshotsDal.GetAccreditationCertificates(db => {
				return db.AccreditationCertificates.Where(s => certificateIds.Contains(s.CertificateId)).ToList();
			}, certificateIds).Cast<IAccreditationCertificateSnapshot>().ToList();
		}

		public List<IChainOptionsSnapshot> LoadChainOptionsSnapshots(List<int> ids) {
			return this.ChainOptionsSnapshotDal.LoadChainOptionsSnapshots(db => {
				return db.ChainOptionsSnapshots.Where(s => ids.Contains(s.Id)).ToList();
			}).Cast<IChainOptionsSnapshot>().ToList();
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

		public virtual void ClearSnapshots() {
			// clear everything, most probably a digest is upcomming
			this.StandardAccountSnapshotsDal.Clear();

			this.JointAccountSnapshotsDal.Clear();
			this.AccountKeysSnapshotDal.Clear();
			this.AccreditationCertificateAccountSnapshotsDal.Clear();
			this.ChainOptionsSnapshotDal.Clear();
		}

		public void UpdateSnapshotDigestFromDigest(IAccountSnapshotDigestChannelCard accountSnapshotDigestChannelCard) {
			if(accountSnapshotDigestChannelCard is IStandardAccountSnapshotDigestChannelCard simpleAccountSnapshotDigestChannelCard) {
				STANDARD_ACCOUNT_SNAPSHOT entry = new STANDARD_ACCOUNT_SNAPSHOT();

				accountSnapshotDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

				this.StandardAccountSnapshotsDal.UpdateSnapshotDigestFromDigest(db => {

					STANDARD_ACCOUNT_SNAPSHOT result = db.StandardAccountSnapshots.SingleOrDefault(c => c.AccountId == entry.AccountId);

					if(result != null) {
						db.StandardAccountSnapshots.Remove(result);
						db.SaveChanges();
					}

					db.StandardAccountSnapshots.Add(entry);
					db.SaveChanges();
				}, entry);

			} else if(accountSnapshotDigestChannelCard is IJointAccountSnapshotDigestChannelCard jointAccountSnapshotDigestChannelCard) {
				JOINT_ACCOUNT_SNAPSHOT entry = new JOINT_ACCOUNT_SNAPSHOT();

				accountSnapshotDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

				this.JointAccountSnapshotsDal.UpdateSnapshotDigestFromDigest(db => {

					JOINT_ACCOUNT_SNAPSHOT result = db.JointAccountSnapshots.SingleOrDefault(c => c.AccountId == entry.AccountId);

					if(result != null) {
						db.JointAccountSnapshots.Remove(result);
						db.SaveChanges();
					}

					db.JointAccountSnapshots.Add(entry);
					db.SaveChanges();
				}, entry);
			}
		}

		public void UpdateAccountKeysFromDigest(IStandardAccountKeysDigestChannelCard standardAccountKeysDigestChannelCard) {
			STANDARD_ACCOUNT_KEY_SNAPSHOT entry = new STANDARD_ACCOUNT_KEY_SNAPSHOT();

			standardAccountKeysDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

			this.AccountKeysSnapshotDal.UpdateSnapshotDigestFromDigest(db => {

				STANDARD_ACCOUNT_KEY_SNAPSHOT result = db.StandardAccountkeysSnapshots.SingleOrDefault(e => (e.AccountId == entry.AccountId) && (e.OrdinalId == entry.OrdinalId));

				if(result != null) {
					db.StandardAccountkeysSnapshots.Remove(result);
					db.SaveChanges();
				}

				db.StandardAccountkeysSnapshots.Add(entry);
				db.SaveChanges();
			}, entry);
		}

		public void UpdateAccreditationCertificateFromDigest(IAccreditationCertificateDigestChannelCard accreditationCertificateDigestChannelCard) {
			ACCREDITATION_CERTIFICATE_SNAPSHOT entry = new ACCREDITATION_CERTIFICATE_SNAPSHOT();

			accreditationCertificateDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

			this.AccreditationCertificateAccountSnapshotsDal.UpdateSnapshotDigestFromDigest(db => {

				ACCREDITATION_CERTIFICATE_SNAPSHOT result = db.AccreditationCertificates.SingleOrDefault(c => c.CertificateId == entry.CertificateId);

				if(result != null) {
					db.AccreditationCertificates.Remove(result);
					db.SaveChanges();
				}

				db.AccreditationCertificates.Add(entry);
				db.SaveChanges();
			}, entry);
		}

		public void UpdateChainOptionsFromDigest(IChainOptionsDigestChannelCard chainOptionsDigestChannelCard) {
			CHAIN_OPTIONS_SNAPSHOT entry = new CHAIN_OPTIONS_SNAPSHOT();

			chainOptionsDigestChannelCard.ConvertToSnapshotEntry(entry, this.GetCardUtils());

			this.ChainOptionsSnapshotDal.UpdateSnapshotDigestFromDigest(db => {

				CHAIN_OPTIONS_SNAPSHOT result = db.ChainOptionsSnapshots.SingleOrDefault(c => c.Id == entry.Id);

				if(result != null) {
					db.ChainOptionsSnapshots.Remove(result);
					db.SaveChanges();
				}

				db.ChainOptionsSnapshots.Add(entry);
				db.SaveChanges();
			});
		}

		public void EnsureChainOptionsCreated() {
			this.ChainOptionsSnapshotDal.EnsureEntryCreated(db => {

				CHAIN_OPTIONS_SNAPSHOT state = db.ChainOptionsSnapshots.SingleOrDefault();

				// make sure there is a single and unique state
				if(state == null) {
					state = new CHAIN_OPTIONS_SNAPSHOT();
					db.ChainOptionsSnapshots.Add(state);
					db.SaveChanges();
				}
			});
		}

		protected TEntity QueryDbSetEntityEntry<TEntity>(DbSet<TEntity> dbSet, Expression<Func<TEntity, bool>> predicate)
			where TEntity : class {

			return dbSet.Local.SingleOrDefault(predicate.Compile()) ?? dbSet.SingleOrDefault(predicate);
		}

		protected List<TEntity> QueryDbSetEntityEntries<TEntity>(DbSet<TEntity> dbSet, Expression<Func<TEntity, bool>> predicate)
			where TEntity : class {

			var results = dbSet.Local.Where(predicate.Compile()).ToList();

			if(!results.Any()) {
				results = dbSet.Where(predicate.Compile()).ToList();
			}

			return results;
		}

		protected ISnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> GetSpecializedSnapshotsModificationHistoryStack(ISnapshotHistoryStackSet snapshotsModificationHistoryStack) {
			return (ISnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>) snapshotsModificationHistoryStack;

		}

		protected virtual void RunCompiledTransactionSets(Dictionary<long, List<Action<STANDARD_ACCOUNT_SNAPSHOT_CONTEXT>>> compiledSimpleTransactions, Dictionary<long, List<Action<JOINT_ACCOUNT_SNAPSHOT_CONTEXT>>> compiledJointTransactions, Dictionary<long, List<Action<STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT>>> compiledAccountKeysTransactions, Dictionary<long, List<Action<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT>>> compiledAccreditationCertificatesTransactions, Dictionary<long, List<Action<CHAIN_OPTIONS_SNAPSHOT_CONTEXT>>> compiledChainOptionsTransactions) {

			List<(STANDARD_ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> simpleTransactions = null;
			List<(JOINT_ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> jointTransactions = null;
			List<(STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> accountKeysTransactions = null;
			List<(ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> accreditationCertificatesTransactions = null;
			List<(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> chainOptionsTransactions = null;

			try {
				if(compiledSimpleTransactions != null) {
					simpleTransactions = this.StandardAccountSnapshotsDal.PerformProcessingSet(compiledSimpleTransactions);
				}

				if(compiledJointTransactions != null) {
					jointTransactions = this.JointAccountSnapshotsDal.PerformProcessingSet(compiledJointTransactions);
				}

				if(compiledAccountKeysTransactions != null) {
					accountKeysTransactions = this.AccountKeysSnapshotDal.PerformProcessingSet(compiledAccountKeysTransactions);
				}

				if(compiledAccreditationCertificatesTransactions != null) {
					accreditationCertificatesTransactions = this.AccreditationCertificateAccountSnapshotsDal.PerformProcessingSet(compiledAccreditationCertificatesTransactions);
				}

				if(compiledChainOptionsTransactions != null) {
					chainOptionsTransactions = this.ChainOptionsSnapshotDal.PerformProcessingSet(compiledChainOptionsTransactions);
				}

				void SaveChanges(List<(DbContext db, IDbContextTransaction transaction)> transactions) {
					if(transactions != null) {
						foreach((DbContext db, IDbContextTransaction transaction) entry in transactions) {
							entry.db.SaveChanges();
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

				var simple = simpleTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();
				var joint = jointTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();
				var keys = accountKeysTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();
				var certificates = accreditationCertificatesTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();
				var options = chainOptionsTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList();

				SaveChanges(simple);
				SaveChanges(joint);
				SaveChanges(keys);
				SaveChanges(certificates);
				SaveChanges(options);

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

				ClearTransactions(simpleTransactions?.Select(e => ((DbContext) e.db, e.transaction)).ToList());
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

		public void UpdateSnapshotEntry(STANDARD_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			// any account that is barebones, we delete to save space. 
			bool isNullEntry = this.IsAccountEntryNull(accountSnapshotEntry);

			this.StandardAccountSnapshotsDal.UpdateSnapshotEntry(db => {
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

				db.SaveChanges();
			}, accountSnapshotEntry);
		}

		public void UpdateSnapshotEntry(JOINT_ACCOUNT_SNAPSHOT accountSnapshotEntry) {

			// any account that is barebones, we delete to save space. 
			bool isNullEntry = this.IsAccountEntryNull(accountSnapshotEntry);

			this.JointAccountSnapshotsDal.UpdateSnapshotEntry(db => {
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

				db.SaveChanges();
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

		private STANDARD_ACCOUNT_SNAPSHOT_DAL simpleAccountSnapshotDal;

		protected STANDARD_ACCOUNT_SNAPSHOT_DAL StandardAccountSnapshotsDal {
			get {
				lock(this.locker) {
					if(this.simpleAccountSnapshotDal == null) {
						this.simpleAccountSnapshotDal = this.centralCoordinator.ChainDalCreationFactory.CreateStandardAccountSnapshotDal<STANDARD_ACCOUNT_SNAPSHOT_DAL>(GROUP_SIZE, this.GetStandardAccountSnapshotsPath(), this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);
					}
				}

				return this.simpleAccountSnapshotDal;
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

		protected virtual STANDARD_ACCOUNT_SNAPSHOT PrepareNewStandardAccountSnapshots(STANDARD_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, AccountId temporaryHashId, IStandardAccountSnapshot source) {
			STANDARD_ACCOUNT_SNAPSHOT snapshot = new STANDARD_ACCOUNT_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.StandardAccountSnapshots.Add(snapshot);

			foreach(STANDARD_ACCOUNT_FEATURE_SNAPSHOT attribute in snapshot.AppliedFeatures) {
				db.StandardAccountSnapshotAttributes.Add(attribute);
			}

			return snapshot;
		}

		protected void UpdateFeatures<ACCOUNT_SNAPSHOT, ACCOUNT_FEATURE_SNAPSHOT>(ACCOUNT_SNAPSHOT snapshot, AccountId accountId, DbSet<ACCOUNT_FEATURE_SNAPSHOT> features)
			where ACCOUNT_SNAPSHOT : class, IAccountSnapshotEntry<ACCOUNT_FEATURE_SNAPSHOT>
			where ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeatureEntry {

			var existingAttributes = this.QueryDbSetEntityEntries(features, a => a.AccountId == accountId.ToLongRepresentation());

			var snapshotAttributes = snapshot.AppliedFeatures.ToList();

			var certificateIds = existingAttributes.Select(a => a.CertificateId).ToList();
			var snapshotCertificates = snapshotAttributes.Select(a => a.CertificateId).ToList();

			// build the delta
			var newAttributes = snapshotAttributes.Where(a => !certificateIds.Contains(a.CertificateId)).ToList();
			var modifyAttributes = snapshotAttributes.Where(a => certificateIds.Contains(a.CertificateId)).ToList();
			var removeAttributes = existingAttributes.Where(a => !snapshotCertificates.Contains(a.CertificateId)).ToList();

			foreach(ACCOUNT_FEATURE_SNAPSHOT attribute in newAttributes) {
				features.Add(attribute);
			}

			foreach(ACCOUNT_FEATURE_SNAPSHOT attribute in modifyAttributes) {

				ACCOUNT_FEATURE_SNAPSHOT dbEntry = existingAttributes.Single(a => a.CertificateId == attribute.CertificateId);
				this.CardUtils.Copy(attribute, dbEntry);
			}

			foreach(ACCOUNT_FEATURE_SNAPSHOT attribute in removeAttributes) {
				features.Remove(attribute);
			}
		}

		protected virtual STANDARD_ACCOUNT_SNAPSHOT PrepareUpdateStandardAccountSnapshots(STANDARD_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, IStandardAccountSnapshot source) {
			STANDARD_ACCOUNT_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.StandardAccountSnapshots, a => a.AccountId == accountId.ToLongRepresentation());
			this.CardUtils.Copy(source, snapshot);

			this.UpdateFeatures(snapshot, accountId, db.StandardAccountSnapshotAttributes);

			return snapshot;
		}

		protected virtual STANDARD_ACCOUNT_SNAPSHOT PrepareDeleteStandardAccountSnapshots(STANDARD_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId) {
			STANDARD_ACCOUNT_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.StandardAccountSnapshots, a => a.AccountId == accountId.ToLongRepresentation());
			db.StandardAccountSnapshots.Remove(snapshot);

			return snapshot;
		}

		protected virtual JOINT_ACCOUNT_SNAPSHOT PrepareNewJointAccountSnapshots(JOINT_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, AccountId temporaryHashId, IJointAccountSnapshot source) {
			JOINT_ACCOUNT_SNAPSHOT snapshot = new JOINT_ACCOUNT_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.JointAccountSnapshots.Add(snapshot);

			return snapshot;
		}

		protected virtual JOINT_ACCOUNT_SNAPSHOT PrepareUpdateJointAccountSnapshots(JOINT_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId, IJointAccountSnapshot source) {
			JOINT_ACCOUNT_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.JointAccountSnapshots, a => a.AccountId == accountId.ToLongRepresentation());
			this.CardUtils.Copy(source, snapshot);

			this.UpdateFeatures(snapshot, accountId, db.JointAccountSnapshotAttributes);

			return snapshot;
		}

		protected virtual JOINT_ACCOUNT_SNAPSHOT PrepareDeleteJointAccountSnapshots(JOINT_ACCOUNT_SNAPSHOT_CONTEXT db, AccountId accountId) {
			JOINT_ACCOUNT_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.JointAccountSnapshots, a => a.AccountId == accountId.ToLongRepresentation());
			db.JointAccountSnapshots.Remove(snapshot);

			return snapshot;
		}

		protected virtual STANDARD_ACCOUNT_KEY_SNAPSHOT PrepareNewAccountKeysSnapshots(STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, (long AccountId, byte OrdinalId) key, IAccountKeysSnapshot source) {
			STANDARD_ACCOUNT_KEY_SNAPSHOT snapshot = new STANDARD_ACCOUNT_KEY_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.StandardAccountkeysSnapshots.Add(snapshot);

			return snapshot;
		}

		protected virtual STANDARD_ACCOUNT_KEY_SNAPSHOT PrepareUpdateAccountKeysSnapshots(STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, (long AccountId, byte OrdinalId) key, IAccountKeysSnapshot source) {
			STANDARD_ACCOUNT_KEY_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.StandardAccountkeysSnapshots, a => (a.AccountId == key.AccountId) && (a.OrdinalId == key.OrdinalId));
			this.CardUtils.Copy(source, snapshot);

			return snapshot;
		}

		protected virtual STANDARD_ACCOUNT_KEY_SNAPSHOT PrepareDeleteAccountKeysSnapshots(STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT db, (long AccountId, byte OrdinalId) key) {
			STANDARD_ACCOUNT_KEY_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.StandardAccountkeysSnapshots, a => (a.AccountId == key.AccountId) && (a.OrdinalId == key.OrdinalId));
			db.StandardAccountkeysSnapshots.Remove(snapshot);

			return snapshot;
		}

		protected virtual ACCREDITATION_CERTIFICATE_SNAPSHOT PrepareNewAccreditationCertificatesSnapshots(ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT db, int certificateId, IAccreditationCertificateSnapshot source) {
			ACCREDITATION_CERTIFICATE_SNAPSHOT snapshot = new ACCREDITATION_CERTIFICATE_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.AccreditationCertificates.Add(snapshot);

			return snapshot;
		}

		protected virtual ACCREDITATION_CERTIFICATE_SNAPSHOT PrepareUpdateAccreditationCertificatesSnapshots(ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT db, int certificateId, IAccreditationCertificateSnapshot source) {
			ACCREDITATION_CERTIFICATE_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.AccreditationCertificates, a => a.CertificateId == certificateId);
			this.CardUtils.Copy(source, snapshot);

			return snapshot;
		}

		protected virtual ACCREDITATION_CERTIFICATE_SNAPSHOT PrepareDeleteAccreditationCertificatesSnapshots(ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT db, int certificateId) {
			ACCREDITATION_CERTIFICATE_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.AccreditationCertificates, a => a.CertificateId == certificateId);
			db.AccreditationCertificates.Remove(snapshot);

			return snapshot;
		}

		protected virtual CHAIN_OPTIONS_SNAPSHOT PrepareNewChainOptionSnapshots(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, int id, IChainOptionsSnapshot source) {
			CHAIN_OPTIONS_SNAPSHOT snapshot = new CHAIN_OPTIONS_SNAPSHOT();
			this.CardUtils.Copy(source, snapshot);

			db.ChainOptionsSnapshots.Add(snapshot);

			return snapshot;
		}

		protected virtual CHAIN_OPTIONS_SNAPSHOT PrepareUpdateChainOptionSnapshots(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, int id, IChainOptionsSnapshot source) {
			CHAIN_OPTIONS_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.ChainOptionsSnapshots, a => a.Id == id);
			this.CardUtils.Copy(source, snapshot);

			return snapshot;
		}

		protected virtual CHAIN_OPTIONS_SNAPSHOT PrepareDeleteChainOptionSnapshots(CHAIN_OPTIONS_SNAPSHOT_CONTEXT db, int id) {
			CHAIN_OPTIONS_SNAPSHOT snapshot = this.QueryDbSetEntityEntry(db.ChainOptionsSnapshots, a => a.Id == id);
			db.ChainOptionsSnapshots.Remove(snapshot);

			return snapshot;
		}

	#endregion

	}
}