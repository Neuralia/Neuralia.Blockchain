using System.Collections.Generic;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active.V1.EncryptedSecret {
	public class EncryptedSecretRepresentativeBallotingRules : ActiveRepresentativeBallotingRules {

		public EncryptedSecretRepresentativeBallotingRules() {

		}

		public EncryptedSecretRepresentativeBallotingRules(Dictionary<Enums.MiningTiers, ushort> miningTierTotals) : base(miningTierTotals) {

		}

		protected override ComponentVersion<ActiveRepresentativeBallotingMethodType> SetIdentity() {
			return (Top10LowestHashes: ActiveRepresentativeBallotingMethodTypes.Instance.EncryptedSecret, 1, 0);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			return nodeList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}
	}
}