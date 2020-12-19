using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Autographs {
	public abstract class AutographBase : IBinarySerializable {

		public virtual void Rehydrate(IDataRehydrator rehydrator) {
			
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

		}
	}
}