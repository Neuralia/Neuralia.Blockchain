using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.AccreditationCertificates.MiningClusters {

	public abstract class MiningClusterAccreditationCertificateMetadata : AccreditationCertificateMetadata {

		public override int CertificateId => 2;

		protected override ComponentVersion<AccreditationCertificateType> SetIdentity() {
			return (AccreditationCertificateTypes.Instance.MINING_CLUSTER, 1, 0);
		}
	}
}