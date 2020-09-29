using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts;
using Neuralia.Blockchains.Core.Cryptography.POW.V1;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {


	public interface IPOWEnvelopeSignature : IEnvelopeSignature {
		long Nonce { get; set; }
		int Solution { get; set; }
		CPUPOWRulesSet RuleSet  { get; set; }
	}

	public class POWEnvelopeSignature : EnvelopeSignature, IPOWEnvelopeSignature {

		public POWEnvelopeSignature() {

		}

		public long Nonce { get; set; } = 0;
		public int Solution { get; set; } = 0;
		public CPUPOWRulesSet RuleSet { get; set; } = new CPUPOWRulesSet();

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();
			
			nodeList.Add(this.RuleSet);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			AdaptiveLong1_9 tool = new AdaptiveLong1_9();

			tool.Value = this.Nonce;
			tool.Dehydrate(dehydrator);

			dehydrator.Write(this.Solution);
			this.RuleSet.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			AdaptiveLong1_9 tool = new AdaptiveLong1_9();

			tool.Rehydrate(rehydrator);
			this.Nonce = tool.Value;

			this.Solution = rehydrator.ReadInt();

			this.RuleSet.Rehydrate(rehydrator);
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("PowNonce", this.Nonce);
			jsonDeserializer.SetProperty("PowSolution", this.Solution);
			jsonDeserializer.SetProperty("RuleSet", this.RuleSet);
		}

		protected override ComponentVersion<EnvelopeSignatureType> SetIdentity() {
			return (EnvelopeSignatureTypes.Instance.POW, 1, 0);
		}
	}
}