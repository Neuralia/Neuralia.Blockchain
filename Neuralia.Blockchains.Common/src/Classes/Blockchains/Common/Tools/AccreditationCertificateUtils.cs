using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	public static class AccreditationCertificateUtils {

		public static bool AnyValid(List<IAccreditationCertificateSnapshot> certificates) {
			return AnyValid<IAccreditationCertificateSnapshot>(certificates);
		}

		public static bool AnyValid<ACCREDITATION_CERTIFICATE_SNAPSHOT>(List<ACCREDITATION_CERTIFICATE_SNAPSHOT> certificates)
			where ACCREDITATION_CERTIFICATE_SNAPSHOT : IAccreditationCertificateSnapshot {
			bool valid = false;

			return certificates.Any(e => {
				bool isValid = false;
				CheckValidity(e, () => isValid = true, null);

				return isValid;
			});
		}

		public static bool IsValid(IAccreditationCertificateSnapshot certificate) {
			bool valid = false;

			CheckValidity(certificate, () => valid = true, null);

			return valid;
		}

		public static void CheckValidity(IAccreditationCertificateSnapshot certificate, Action valid, Action Invalid) {
			if(certificate != null) {
				if(certificate.CertificateState == Enums.CertificateStates.Revoked) {
					if(Invalid != null) {
						Invalid();
					}

					return;
				}

				if(certificate.EmissionDate > DateTimeEx.CurrentTime) {
					if(Invalid != null) {
						Invalid();
					}

					return;
				}

				if(certificate.ValidUntil < DateTimeEx.CurrentTime) {
					if(Invalid != null) {
						Invalid();
					}

					return;
				}

				if(valid != null) {
					valid();
				}
			} else {
				if(Invalid != null) {
					Invalid();
				}
			}
		}
	}
}