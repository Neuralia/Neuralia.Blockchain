using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {

	public interface IAccreditationCertificateSnapshotSqliteDal : IAccreditationCertificatesSnapshotDal {
	}

	public interface IAccreditationCertificateSnapshotSqliteDal<ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> : IIndexedSqliteDal<IAccreditationCertificateSnapshotSqliteContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>>, IAccreditationCertificateSnapshotSqliteDal, IAccreditationCertificatesSnapshotDal<ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_CONTEXT : class, IAccreditationCertificateSnapshotSqliteContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotSqliteEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountSqliteEntry {
	}

	public abstract class AccreditationCertificateSnapshotSqliteDal<ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> : IndexedSqliteDal<ACCREDITATION_CERTIFICATE_CONTEXT>, IAccreditationCertificateSnapshotSqliteDal<ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_CONTEXT : DbContext, IAccreditationCertificateSnapshotSqliteContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : AccreditationCertificateSnapshotSqliteEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : AccreditationCertificateSnapshotAccountSqliteEntry {

		protected AccreditationCertificateSnapshotSqliteDal(int groupSize, string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateAccreditationCertificateSnapshotContext<ACCREDITATION_CERTIFICATE_CONTEXT>, serializationType) {
		}

		public Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(Func<ACCREDITATION_CERTIFICATE_CONTEXT, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> operation, int certificateId) {

			return this.PerformOperationAsync(operation, this.GetKeyGroup(certificateId));
		}

		public Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(Func<ACCREDITATION_CERTIFICATE_CONTEXT, Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>>> operation) {
			return this.QueryAllAsync(operation);
		}

		public Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(Func<ACCREDITATION_CERTIFICATE_CONTEXT, Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>>> operation, List<int> certificateIds) {
			return this.QueryAllAsync(operation, certificateIds.Select(e => (long)e).ToList());
		}

		public Task UpdateSnapshotDigestFromDigest(Func<ACCREDITATION_CERTIFICATE_CONTEXT, Task> operation, ACCREDITATION_CERTIFICATE_SNAPSHOT accountSnapshotEntry) {

			return this.PerformOperationAsync(operation, this.GetKeyGroup(accountSnapshotEntry.CertificateId));
		}

		public Task<List<(ACCREDITATION_CERTIFICATE_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<long, List<Func<ACCREDITATION_CERTIFICATE_CONTEXT, LockContext, Task>>> actions) {
			return this.PerformProcessingSetHoldTransactions(actions);
		}
	}
}