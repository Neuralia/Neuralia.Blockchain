using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures {


	public interface ITHSEnvelopeSignature : IEnvelopeSignature {
		THSSolutionSet Solution { get; set; }
		THSRulesSet RuleSet  { get; set; }
	}

	public class THSEnvelopeSignature : EnvelopeSignature, ITHSEnvelopeSignature {

		public THSEnvelopeSignature() {

		}

		public THSSolutionSet Solution { get; set; } = new THSSolutionSet();
		public THSRulesSet RuleSet { get; set; } = new THSRulesSet();

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();
			
			nodeList.Add(this.RuleSet);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			this.Solution.Dehydrate(dehydrator);
			this.RuleSet.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			this.Solution.Rehydrate(rehydrator);
			this.RuleSet.Rehydrate(rehydrator);
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetProperty("THSSolution", this.Solution);
			jsonDeserializer.SetProperty("RuleSet", this.RuleSet);
		}

		protected override ComponentVersion<EnvelopeSignatureType> SetIdentity() {
			return (EnvelopeSignatureTypes.Instance.THS, 1, 0);
		}
	}
}