using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.AccreditationCertificates.SdkProviders {
	public abstract class SdkProviderAccreditationCertificateMetadata : AccreditationCertificateMetadata {

		public override int CertificateId => 1;

		protected override ComponentVersion<AccreditationCertificateType> SetIdentity() {
			return (AccreditationCertificateTypes.Instance.SDK_PROVIDER, 1, 0);
		}
	}
}