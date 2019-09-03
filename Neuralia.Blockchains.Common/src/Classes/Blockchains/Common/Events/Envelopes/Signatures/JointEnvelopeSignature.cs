using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {
	public interface IJointEnvelopeSignature : IEnvelopeSignature {
		List<IPublishedAccountSignature> AccountSignatures { get; }
	}

	public class JointEnvelopeSignature : EnvelopeSignature, IJointEnvelopeSignature {

		public List<IPublishedAccountSignature> AccountSignatures { get; } = new List<IPublishedAccountSignature>();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.AccountSignatures.Count);

			foreach(IPublishedAccountSignature signature in this.AccountSignatures) {
				signature.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				IPublishedAccountSignature signature = new PublishedAccountSignature();
				signature.Rehydrate(rehydrator);

				this.AccountSignatures.Add(signature);
			}
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			foreach(IPublishedAccountSignature signature in this.AccountSignatures) {
				hashNodeList.Add(signature.GetStructuresArray());
			}

			return hashNodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetArray("AccountSignatures", this.AccountSignatures);
		}

		protected override ComponentVersion<EnvelopeSignatureType> SetIdentity() {
			return (EnvelopeSignatureTypes.Instance.Joint, 1, 0);
		}
	}
}