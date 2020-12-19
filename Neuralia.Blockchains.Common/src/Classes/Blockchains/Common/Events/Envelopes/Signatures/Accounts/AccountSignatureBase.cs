using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts {
	
	
	public interface IAccountSignatureBase : ITreeHashable, IBinarySerializable, IJsonSerializable {

		byte Version { get; }
	}

	public abstract class AccountSignatureBase : IAccountSignatureBase {

		public byte Version { get; private set; } = 1;

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodelist = new HashNodeList();

			nodelist.Add(this.Version);

			return nodelist;
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Version);
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			this.Version = rehydrator.ReadByte();

		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("Version", this.Version);
		}
	}
}