using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {
	public interface IAccreditationCertificateSnapshotEntry : IAccreditationCertificateSnapshot {
	}

	public interface IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT> : IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT>, IAccreditationCertificateSnapshotEntry
		where ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT : IAccreditationCertificateSnapshotAccountEntry {
	}
}