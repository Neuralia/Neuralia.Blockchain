using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {
	public interface IAccreditationCertificatesSnapshotDal : ISnapshotDal {
	}

	public interface IAccreditationCertificatesSnapshotDal<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> : IAccreditationCertificatesSnapshotDal, ISnapshotDal<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT : IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry {

		Task<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificate(Func<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> operation, int certificateId);
		Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(Func<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>>> operation);

		Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> GetAccreditationCertificates(Func<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, Task<List<ACCREDITATION_CERTIFICATE_SNAPSHOT>>> operation, List<int> certificateIds);

		Task Clear();
		Task UpdateSnapshotDigestFromDigest(Func<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, Task> operation, ACCREDITATION_CERTIFICATE_SNAPSHOT accountSnapshotEntry);

		Task<List<(ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)>> PerformProcessingSet(Dictionary<long, List<Func<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, LockContext, Task>>> actions);
	}
}