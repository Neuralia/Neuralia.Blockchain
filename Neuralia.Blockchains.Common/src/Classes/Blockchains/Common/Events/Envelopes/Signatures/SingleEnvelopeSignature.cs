using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	public interface ISingleEnvelopeSignature {
	}

	public interface ISingleEnvelopeSignature<T> : IEnvelopeSignature, ISingleEnvelopeSignature
		where T : IAccountSignature, new() {
		T AccountSignature { get; }
	}

	public abstract class SingleEnvelopeSignature<T> : EnvelopeSignature, ISingleEnvelopeSignature<T>
		where T : IAccountSignature, new() {
		public SingleEnvelopeSignature() {
			this.AccountSignature = new T();

			if(this.AccountSignature == null) {
				throw new ApplicationException("Account signature must be set");
			}
		}

		public T AccountSignature { get; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.AccountSignature.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.AccountSignature.Rehydrate(rehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			hashNodeList.Add(this.AccountSignature.GetStructuresArray());

			return hashNodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("AccountSignature", this.AccountSignature);
		}
	}
}