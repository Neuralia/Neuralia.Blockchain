using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations {
	public class AccreditationCertificateSnapshotAccountEntry : AccreditationCertificateSnapshotAccount, IAccreditationCertificateSnapshotAccountEntry {

		public long Id { get; set; }
		public int CertificateId { get; set; }
	}
}