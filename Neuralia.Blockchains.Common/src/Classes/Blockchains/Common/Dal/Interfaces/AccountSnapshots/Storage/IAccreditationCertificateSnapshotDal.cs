using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage {
	public interface IAccreditationCertificatesSnapshotDal : ISnapshotDal {
	}

	public interface IAccreditationCertificatesSnapshotDal<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> : IAccreditationCertificatesSnapshotDal, ISnapshotDal<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT : IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry {

		ACCREDITATION_CERTIFICATE_SNAPSHOT GetAccreditationCertificate(Func<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_SNAPSHOT> operation, int certificateId);
		List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(Func<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> operation);

		List<ACCREDITATION_CERTIFICATE_SNAPSHOT> GetAccreditationCertificates(Func<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT, List<ACCREDITATION_CERTIFICATE_SNAPSHOT>> operation, List<int> certificateIds);

		void Clear();
		void UpdateSnapshotDigestFromDigest(Action<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT> operation, ACCREDITATION_CERTIFICATE_SNAPSHOT accountSnapshotEntry);

		List<(ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT db, IDbContextTransaction transaction)> PerformProcessingSet(Dictionary<long, List<Action<ACCREDITATION_CERTIFICATE_SNAPSHOT_CONTEXT>>> actions);
	}
}