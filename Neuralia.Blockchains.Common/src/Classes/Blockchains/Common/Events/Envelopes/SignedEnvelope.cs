using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface ISignedEnvelope : IEnvelope {

		SafeArrayHandle Hash { get; }
		IEnvelopeSignature SignatureBase { get; }
		bool IsSecretSignature { get; }
	}

	public interface ISignedEnvelope<BLOCKCHAIN_EVENT_TYPE, SIGNATURE_TYPE> : ISignedEnvelope, IEnvelope<BLOCKCHAIN_EVENT_TYPE>
		where BLOCKCHAIN_EVENT_TYPE : class, IBinarySerializable
		where SIGNATURE_TYPE : IEnvelopeSignature {

		SIGNATURE_TYPE Signature { get; set; }
	}

	public abstract class SignedEnvelope<BLOCKCHAIN_EVENT_TYPE, SIGNATURE_TYPE> : Envelope<BLOCKCHAIN_EVENT_TYPE, EnvelopeType>, ISignedEnvelope<BLOCKCHAIN_EVENT_TYPE, SIGNATURE_TYPE>
		where BLOCKCHAIN_EVENT_TYPE : class, IBinarySerializable, ITreeHashable
		where SIGNATURE_TYPE : IEnvelopeSignature {

		public SafeArrayHandle Hash { get;  } = SafeArrayHandle.Create();
		public SIGNATURE_TYPE Signature { get; set; }
		public IEnvelopeSignature SignatureBase => this.Signature;

		public bool IsSecretSignature => this.Signature is ISecretEnvelopeSignature;

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Signature.GetStructuresArray());

			return nodeList;
		}

		protected override void Dehydrate(IDataDehydrator dh) {
			dh.WriteNonNullable(this.Hash);
			this.Signature.Dehydrate(dh);
		}

		protected override void Rehydrate(IDataRehydrator rh) {
			this.Hash.Entry = rh.ReadNonNullableArray();

			this.Signature = (SIGNATURE_TYPE) EnvelopeSignatureFactory.Rehydrate(rh);
		}
	}
}