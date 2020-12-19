using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IAccreditationCertificateSnapshotAccountSqliteEntry : IAccreditationCertificateSnapshotAccountEntry {
	}

	public class AccreditationCertificateSnapshotAccountSqliteEntry : AccreditationCertificateSnapshotAccount, IAccreditationCertificateSnapshotAccountSqliteEntry {

		public long Id { get; set; }
		public int CertificateId { get; set; }
	}
}