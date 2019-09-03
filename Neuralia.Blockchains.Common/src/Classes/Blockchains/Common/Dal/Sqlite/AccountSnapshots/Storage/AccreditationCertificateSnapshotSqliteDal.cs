using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.Tools;

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

		protected AccreditationCertificateSnapshotSqliteDal(long groupSize, string folderPath, ServiceSet serviceSet, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(groupSize, folderPath, serviceSet, chainDalCreationFactory.CreateAccreditationCertificateSnapshotContext<ACCREDITATION_CERTIFICATE_CONTEXT>, serializationType) {
		}

		public ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificate(Func<ACCREDITATION_CERTIFICATE_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT> operation, int certificateId) {

			return this.PerformOperation(operation, this.GetKeyGroup(certificateId));
		}

		public List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(Func<ACCREDITATION_CERTIFICATE_CONTEXT, List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> operation) {
			return this.QueryAll(operation);
		}

		public List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(Func<ACCREDITATION_CERTIFICATE_CONTEXT, List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> operation, List<int> certificateIds) {
			return this.QueryAll(operation, certificateIds.Cast<long>().ToList());
		}

		public void Clear() {
			foreach(string file in this.GetAllFileGroups()) {
				if(File.Exists(file)) {
					File.Delete(file);
				}
			}
		}

		public void UpdateSnapshotDigestFromDigest(Action<ACCREDITATION_CERTIFICATE_CONTEXT> operation, ACCREDITATION_CERTIFICATE_SNAPSHOT accountSnapshotEntry) {

			this.PerformOperation(operation, this.GetKeyGroup(accountSnapshotEntry.CertificateId));
		}

		public new List<(ACCREDITATION_CERTIFICATE_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<ACCREDITATION_CERTIFICATE_CONTEXT>>> actions) {
			return this.PerformProcessingSetHoldTransactions(actions);
		}
	}
}