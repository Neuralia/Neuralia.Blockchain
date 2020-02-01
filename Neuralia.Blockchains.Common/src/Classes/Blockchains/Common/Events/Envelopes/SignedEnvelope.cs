using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface ISignedEnvelope : IEnvelope {

		SafeArrayHandle Hash { get; }
		IEnvelopeSignature SignatureBase { get; }
		bool IsSecretSignature { get; }
		List<int> AccreditationCertificates { get; }

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

		public List<int> AccreditationCertificates { get; } = new List<int>();
		public bool IsSecretSignature => this.Signature is ISecretEnvelopeSignature;

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Signature.GetStructuresArray());
			nodeList.Add(this.AccreditationCertificates.Count);

			foreach(int entry in this.AccreditationCertificates) {

				nodeList.Add(entry);
			}
			return nodeList;
		}

		protected override void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.WriteNonNullable(this.Hash);
			this.Signature.Dehydrate(dehydrator);
			
			bool any = this.AccreditationCertificates.Any();
			dehydrator.Write(any);

			if(any) {
				dehydrator.Write((byte) this.AccreditationCertificates.Count);

				foreach(int entry in this.AccreditationCertificates) {

					dehydrator.Write(entry);
				}
			}
		}

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			this.Hash.Entry = rehydrator.ReadNonNullableArray();

			this.Signature = (SIGNATURE_TYPE) EnvelopeSignatureFactory.Rehydrate(rehydrator);
			
			this.AccreditationCertificates.Clear();
			bool any = rehydrator.ReadBool();

			if(any) {
				int count = rehydrator.ReadByte();

				for(int i = 0; i < count; i++) {
					this.AccreditationCertificates.Add(rehydrator.ReadInt());
				}
			}
		}
	}
}