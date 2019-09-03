using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations {

	public class AccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT> : IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT : IAccreditationCertificateSnapshotAccount {

		public int CertificateId { get; set; }
		public ComponentVersion CertificateVersion { get; set; }
		public AccreditationCertificateType CertificateType { get; set; }
		public Enums.CertificateApplicationTypes ApplicationType { get; set; }
		public Enums.CertificateStates CertificateState { get; set; }
		public DateTime EmissionDate { get; set; }
		public DateTime ValidUntil { get; set; }
		public long AssignedAccount { get; set; }
		public string Application { get; set; }
		public string Organisation { get; set; }
		public string Url { get; set; }
		public Enums.CertificateAccountPermissionTypes CertificateAccountPermissionType { get; set; }
		public int PermittedAccountCount { get; set; }
		public List<ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT> PermittedAccounts { get; } = new List<ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT>();

		public void ClearCollection() {
			this.PermittedAccounts.Clear();
		}

		public void CreateNewCollectionEntry(out IAccreditationCertificateSnapshotAccount result) {
			TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.CreateNewCollectionEntry(this.PermittedAccounts, out result);
		}

		public void AddCollectionEntry(IAccreditationCertificateSnapshotAccount entry) {
			TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.AddCollectionEntry(entry, this.PermittedAccounts);
		}

		public void RemoveCollectionEntry(Func<IAccreditationCertificateSnapshotAccount, bool> predicate) {
			TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.RemoveCollectionEntry(predicate, this.PermittedAccounts);
		}

		public IAccreditationCertificateSnapshotAccount GetCollectionEntry(Func<IAccreditationCertificateSnapshotAccount, bool> predicate) {
			return TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.GetCollectionEntry(predicate, this.PermittedAccounts);
		}

		public List<IAccreditationCertificateSnapshotAccount> GetCollectionEntries(Func<IAccreditationCertificateSnapshotAccount, bool> predicate) {
			return TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.GetCollectionEntries(predicate, this.PermittedAccounts);
		}

		public ImmutableList<IAccreditationCertificateSnapshotAccount> CollectionCopy => TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.GetCollection(this.PermittedAccounts);
	}
}