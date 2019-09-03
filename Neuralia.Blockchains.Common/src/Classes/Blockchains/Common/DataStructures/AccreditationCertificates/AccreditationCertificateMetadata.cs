using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.AccreditationCertificates {

	public abstract class AccreditationCertificateMetadata : Versionable<AccreditationCertificateType> {

		public abstract int CertificateId { get; }

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			return base.GetStructuresArray();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
		}
	}
}