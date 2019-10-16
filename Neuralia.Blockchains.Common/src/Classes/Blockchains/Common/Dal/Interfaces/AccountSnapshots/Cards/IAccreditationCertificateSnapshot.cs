using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {

	public interface IAccreditationCertificateSnapshot : ISnapshot, ITypedCollectionExposure<IAccreditationCertificateSnapshotAccount> {
		int CertificateId { get; set; }
		ComponentVersion CertificateVersion { get; set; }
 
		//type: AccreditationCertificateTypes
		int CertificateType { get; set; }
		Enums.CertificateApplicationTypes ApplicationType { get; set; }
		Enums.CertificateStates CertificateState { get; set; }

		DateTime EmissionDate { get; set; }
		DateTime ValidUntil { get; set; }

		long AssignedAccount { get; set; }

		string Application { get; set; }
		string Organisation { get; set; }
		string Url { get; set; }

		Enums.CertificateAccountPermissionTypes CertificateAccountPermissionType { get; set; }
		int PermittedAccountCount { get; set; }
	}

	public interface IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT> : IAccreditationCertificateSnapshot
		where ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT : IAccreditationCertificateSnapshotAccount {

		List<ACCREDITATION_CERTIFICATE_SNAPSHOT_ACCOUNT> PermittedAccounts { get; }
	}

}