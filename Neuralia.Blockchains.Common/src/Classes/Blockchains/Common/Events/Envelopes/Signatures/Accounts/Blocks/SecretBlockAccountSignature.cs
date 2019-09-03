using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public interface ISecretBlockAccountSignature : IBlockAccountSignature {
		long PromisedNonce1 { get; set; }
		long PromisedNonce2 { get; set; }
		IByteArray PromisedPublicKey { get; set; }
	}

	public class SecretBlockAccountSignature : BlockAccountSignature, ISecretBlockAccountSignature {

		public long PromisedNonce1 { get; set; }
		public long PromisedNonce2 { get; set; }
		public IByteArray PromisedPublicKey { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.PromisedNonce1);
			nodelist.Add(this.PromisedNonce2);
			nodelist.Add(this.PromisedPublicKey);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.PromisedNonce1);
			dehydrator.Write(this.PromisedNonce2);
			dehydrator.WriteNonNullable(this.PromisedPublicKey);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.PromisedNonce1 = rehydrator.ReadLong();
			this.PromisedNonce2 = rehydrator.ReadLong();
			this.PromisedPublicKey = rehydrator.ReadNonNullableArray();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("PromisedNonce1", this.PromisedNonce1);
			jsonDeserializer.SetProperty("PromisedNonce2", this.PromisedNonce2);
			jsonDeserializer.SetProperty("PromisedPublicKey", this.PromisedPublicKey);
		}
	}
}