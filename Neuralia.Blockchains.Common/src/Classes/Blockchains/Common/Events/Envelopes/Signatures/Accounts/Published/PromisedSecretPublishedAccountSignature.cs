using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published {

	public interface IPromisedSecretPublishedAccountSignature : IPublishedAccountSignature, IPromisedSecretAccountSignature {
	}

	public class PromisedSecretPublishedAccountSignature : PublishedAccountSignature, IPromisedSecretPublishedAccountSignature {

		public SafeArrayHandle PromisedPublicKey { get; } = SafeArrayHandle.Create();

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.PromisedPublicKey);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.PromisedPublicKey);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.PromisedPublicKey.Entry = rehydrator.ReadNonNullableArray();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("PromisedPublicKey", this.PromisedPublicKey);
		}
	}
}