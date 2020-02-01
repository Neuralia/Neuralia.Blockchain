using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IAccreditationCertificateProvider: IChainProvider {

		bool IsTransactionCertificateValid(int accreditationCertificate, TransactionId transactionId, Enums.CertificateApplicationTypes applicationType);
		bool IsAnyTransactionCertificateValid(List<int> accreditationCertificate, TransactionId transactionId, Enums.CertificateApplicationTypes applicationType);
		(ImmutableList<AccountId> validDelegates, ImmutableList<AccountId> invalidDelegates) ValidateDelegateAccounts(ImmutableList<AccountId> potentialDelegateAccounts, Enums.CertificateApplicationTypes applicationType);
		
		IAccreditationCertificateSnapshotEntry GetAccountAccreditationCertificateBase(int certificateId);

	}

	public interface IAccreditationCertificateProvider<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> : IAccreditationCertificateProvider
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry {

		ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccountAccreditationCertificate(int certificateId);
		ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccountAccreditationCertificate(AccountId accountId, AccreditationCertificateType accreditationCertificateType, Enums.CertificateApplicationTypes certificateApplicationType);
		ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccountAccreditationCertificate(AccountId accountId, AccreditationCertificateType accreditationCertificateType);

	}

	
	public interface IAccreditationCertificateProvider<ACCREDITATION_CERTIFICATE_DAL, ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> :  IAccreditationCertificateProvider<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_DAL : class, IAccreditationCertificatesSnapshotDal<ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_CONTEXT : class, IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry {
		
	}

	/// <summary>
	///     A provider that offers the chain state parameters from the DB
	/// </summary>
	/// <typeparam name="ACCREDITATION_CERTIFICATE_DAL"></typeparam>
	/// <typeparam name="ACCREDITATION_CERTIFICATE_CONTEXT"></typeparam>
	/// <typeparam name="ACCREDITATION_CERTIFICATE_SNAPSHOT"></typeparam>
	public abstract class AccreditationCertificateProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCREDITATION_CERTIFICATE_DAL, ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> : IAccreditationCertificateProvider<ACCREDITATION_CERTIFICATE_DAL, ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ACCREDITATION_CERTIFICATE_DAL : class, IAccreditationCertificatesSnapshotDal<ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_CONTEXT : class, IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry, new() {
		private readonly CENTRAL_COORDINATOR centralCoordinator;

		private readonly string folderPath;

		private readonly object locker = new object();

		protected readonly ITimeService timeService;

		private ACCREDITATION_CERTIFICATE_DAL chainStateDal;

		private ACCREDITATION_CERTIFICATE_SNAPSHOT chainStateEntry;

		public AccreditationCertificateProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;

		}

		protected IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> SnapshotProvider => (IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>) this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase;

		private IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> AccountSnapshotsProvider => (IAccountSnapshotsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>) this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase;

		public bool IsTransactionCertificateValid(int accreditationCertificate, TransactionId transactionId, Enums.CertificateApplicationTypes applicationType) {

			ACCREDITATION_CERTIFICATE_SNAPSHOT certificateEntry = this.SnapshotProvider.GetAccreditationCertificate(accreditationCertificate, transactionId.Account, AccreditationCertificateTypes.Instance.THIRD_PARTY, applicationType);

			return AccreditationCertificateUtils.IsValid(certificateEntry);
		}

		public bool IsAnyTransactionCertificateValid(List<int> accreditationCertificate, TransactionId transactionId, Enums.CertificateApplicationTypes applicationType) {

			var certificateEntries = this.SnapshotProvider.GetAccreditationCertificate(accreditationCertificate, transactionId.Account, AccreditationCertificateTypes.Instance.THIRD_PARTY, applicationType);

			return AccreditationCertificateUtils.AnyValid(certificateEntries);
		}

		/// <summary>
		///     ensure the set of accounts are valid on chain
		/// </summary>
		/// <param name="ponetialDelegateAccounts"></param>
		public (ImmutableList<AccountId> validDelegates, ImmutableList<AccountId> invalidDelegates) ValidateDelegateAccounts(ImmutableList<AccountId> potentialDelegateAccounts, Enums.CertificateApplicationTypes applicationType) {

			var invalidDelegates = new List<AccountId>();
			var certificateSnapshots = this.SnapshotProvider.GetAccreditationCertificates(potentialDelegateAccounts, new[] {AccreditationCertificateTypes.Instance.DELEGATE, AccreditationCertificateTypes.Instance.SDK_PROVIDER}, applicationType);
			var returnedAccounts = certificateSnapshots.Select(s => s.AssignedAccount.ToAccountId()).ToList();

			// now validate them

			// first, remove any certificate we did not find. no accreditation.
			invalidDelegates.AddRange(potentialDelegateAccounts.Where(a => !returnedAccounts.Contains(a)));

			// now we validate the certificate
			foreach(ACCREDITATION_CERTIFICATE_SNAPSHOT certificate in certificateSnapshots) {

				bool valid = false;

				AccreditationCertificateUtils.CheckValidity(certificate, () => valid = true, null);

				if(!valid) {
					invalidDelegates.Add(certificate.AssignedAccount.ToAccountId());
				}
			}

			return (potentialDelegateAccounts.Where(a => !invalidDelegates.Contains(a)).ToImmutableList(), invalidDelegates.ToImmutableList());
		}

		public ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccountAccreditationCertificate(AccountId accountId, AccreditationCertificateType accreditationCertificateType) {

			return this.GetAccountAccreditationCertificate(accountId, accreditationCertificateType, Enums.CertificateApplicationTypes.Abstract);
		}
		
		public ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccountAccreditationCertificate(AccountId accountId, AccreditationCertificateType accreditationCertificateType, Enums.CertificateApplicationTypes certificateApplicationType) {

			return this.SnapshotProvider.GetAccreditationCertificates(accountId, accreditationCertificateType, certificateApplicationType).SingleOrDefault();
		}

		public IAccreditationCertificateSnapshotEntry GetAccountAccreditationCertificateBase(int certificateId) {
			return this.GetAccountAccreditationCertificate(certificateId);
		}
		
		public ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccountAccreditationCertificate(int certificateId){

			return this.SnapshotProvider.GetAccreditationCertificate(certificateId);
		}
	
		protected virtual ACCREDITATION_CERTIFICATE_SNAPSHOT CreateNewEntry() {
			ACCREDITATION_CERTIFICATE_SNAPSHOT sqliteEntry = new ACCREDITATION_CERTIFICATE_SNAPSHOT();

			return sqliteEntry;
		}
	}
}