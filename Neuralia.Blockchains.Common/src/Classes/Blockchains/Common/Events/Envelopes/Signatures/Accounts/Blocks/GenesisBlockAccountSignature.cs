using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public interface IGenesisBlockAccountSignature : IBlockAccountSignature {
		SafeArrayHandle PublicKey { get; }
	}

	public class GenesisBlockAccountSignature : BlockAccountSignature, IGenesisBlockAccountSignature {
		public SafeArrayHandle PublicKey { get; } = SafeArrayHandle.Create();

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.PublicKey);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.PublicKey);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.PublicKey.Entry = rehydrator.ReadNonNullableArray();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("PublicKey", this.PublicKey);
		}
	}
}