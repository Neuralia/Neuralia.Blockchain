using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts {

	public interface IAccountSignature : ITreeHashable, IBinarySerializable, IJsonSerializable {

		byte Version { get; }
		IByteArray Autograph { get; set; }
	}

	public class AccountSignature : IAccountSignature {

		public byte Version { get; private set; } = 1;
		public IByteArray Autograph { get; set; }

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodelist = new HashNodeList();

			nodelist.Add(this.Version);
			nodelist.Add(this.Autograph);

			return nodelist;
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Version);
			dehydrator.WriteNonNullable(this.Autograph);
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			this.Version = rehydrator.ReadByte();
			this.Autograph = rehydrator.ReadNonNullableArray();

		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("Version", this.Version);
			jsonDeserializer.SetProperty("Autograph", this.Autograph);
		}
	}
}