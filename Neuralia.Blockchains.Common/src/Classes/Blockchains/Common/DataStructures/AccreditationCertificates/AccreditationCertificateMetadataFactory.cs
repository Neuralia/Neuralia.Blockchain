using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.AccreditationCertificates.MiningClusters;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.AccreditationCertificates.SdkProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.AccreditationCertificates {
	public abstract class AccreditationCertificateMetadataFactory {

		public AccreditationCertificateMetadata RehydrateMetadata(SafeArrayHandle data) {

			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			var version = rehydrator.RehydrateRewind<ComponentVersion<AccreditationCertificateType>>();

			if(version.Type == AccreditationCertificateTypes.Instance.SDK_PROVIDER) {
				return this.CreateSdkProviderAccreditationCertificateMetadata(version);
			}

			if(version.Type == AccreditationCertificateTypes.Instance.MINING_CLUSTER) {
				return this.CreateMiningClusterAccreditationCertificateMetadata(version);
			}

			return null;
		}

		protected abstract SdkProviderAccreditationCertificateMetadata CreateSdkProviderAccreditationCertificateMetadata(ComponentVersion version);

		protected abstract MiningClusterAccreditationCertificateMetadata CreateMiningClusterAccreditationCertificateMetadata(ComponentVersion version);
	}
}