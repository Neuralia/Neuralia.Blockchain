using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {

	public interface IAccreditationCertificateSnapshotAccountEntry : IAccreditationCertificateSnapshotAccount {

		long Id { get; set; }
		int CertificateId { get; set; }
	}

}