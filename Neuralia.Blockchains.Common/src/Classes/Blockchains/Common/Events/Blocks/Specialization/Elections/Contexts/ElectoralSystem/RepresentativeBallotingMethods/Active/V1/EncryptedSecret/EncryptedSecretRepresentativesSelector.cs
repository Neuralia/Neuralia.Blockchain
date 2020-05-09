using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MoreLinq;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active.V1.EncryptedSecret {
	public class EncryptedSecretRepresentativeBallotingSelector : ActiveRepresentativeBallotingSelector<EncryptedSecretRepresentativeBallotingRules> {

		public EncryptedSecretRepresentativeBallotingSelector(EncryptedSecretRepresentativeBallotingRules representativeBallotingRules) : base(representativeBallotingRules) {
		}

		public override Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> SelectRepresentatives(Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> electedTiers, IActiveRepresentativeBallotingProof proof) {
			if(this.RepresentativeBallotingRules.Version == ActiveRepresentativeBallotingMethodTypes.Instance.EncryptedSecret) {
				return this.PerformEncryptedSecretRepresentativeSelection(electedTiers, proof);
			}

			throw new ApplicationException("Invalid context type");
		}

		public override IActiveRepresentativeBallotingApplication PrepareRepresentativeBallotingApplication(IActiveRepresentativeBallotingRules ballotRules) {

			EncryptedSecretRepresentativeBallotingApplication application = new EncryptedSecretRepresentativeBallotingApplication();

			//TODO: what should we prepare here?

			return application;
		}

		protected Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> PerformEncryptedSecretRepresentativeSelection(Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> electedTiers, IActiveRepresentativeBallotingProof proof) {

			Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> representativesTiers = new Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>>();

			// this will give us the X lowest hashes among X elected
			foreach(KeyValuePair<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> tier in electedTiers) {
				IEnumerable<AccountId> primeRepresentatives = tier.Value.Select(r => (r.Key, hash: new BigInteger(r.Value.ElectionHash.ToExactByteArrayCopy()))).OrderBy(v => v.hash).Take(this.RepresentativeBallotingRules.GetTotal(tier.Key)).Select(r => r.Key);

				// let's select our up to X prime elected
				representativesTiers.Add(tier.Key, tier.Value.Where(r => primeRepresentatives.Contains(r.Key)).ToDictionary());
			}

			return representativesTiers;
		}

		private void PrepareRepresentativeBallotingApplication() {

		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}
	}
}