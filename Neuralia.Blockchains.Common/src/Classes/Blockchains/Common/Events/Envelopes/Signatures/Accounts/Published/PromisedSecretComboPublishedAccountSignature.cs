using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published {

	public interface IPromisedSecretComboPublishedAccountSignature : IPromisedSecretPublishedAccountSignature {
	}

	public class PromisedSecretComboPublishedAccountSignature : PromisedSecretPublishedAccountSignature, IPromisedSecretComboPublishedAccountSignature {
		public long PromisedNonce1 { get; set; }
		public long PromisedNonce2 { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.PromisedNonce1);
			nodelist.Add(this.PromisedNonce2);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.PromisedNonce1);
			dehydrator.Write(this.PromisedNonce2);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.PromisedNonce1 = rehydrator.ReadLong();
			this.PromisedNonce2 = rehydrator.ReadLong();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("PromisedNonce1", this.PromisedNonce1);
			jsonDeserializer.SetProperty("PromisedNonce2", this.PromisedNonce2);
		}
	}
}