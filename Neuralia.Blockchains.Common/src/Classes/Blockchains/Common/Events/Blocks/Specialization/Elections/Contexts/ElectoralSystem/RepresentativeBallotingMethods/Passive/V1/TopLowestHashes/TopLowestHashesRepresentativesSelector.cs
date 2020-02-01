using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MoreLinq;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive.V1.TopLowestHashes {
	public class TopLowestHashesRepresentativeBallotingSelector : PassiveRepresentativeBallotingSelector<TopLowestHashesRepresentativeBallotingRules> {

		public TopLowestHashesRepresentativeBallotingSelector(TopLowestHashesRepresentativeBallotingRules representativeBallotingRules) : base(representativeBallotingRules) {
		}

		public override Dictionary<Enums.MiningTiers, Dictionary<AccountId, U>> SelectRepresentatives<U>(Dictionary<Enums.MiningTiers, Dictionary<AccountId, U>> elected) {
			if(this.RepresentativeBallotingRules.Version == PassiveRepresentativeBallotingMethodTypes.Instance.TopLowestHashes) {
				return this.PerformTopLowestHashesRepresentativeSelection(elected);
			}

			throw new ApplicationException("Invalid context type");
		}

		protected Dictionary<Enums.MiningTiers, Dictionary<AccountId, U>> PerformTopLowestHashesRepresentativeSelection<U>(Dictionary<Enums.MiningTiers, Dictionary<AccountId, U>> electedTiers) where U : IPassiveElectedChoice {

			var representativesTiers = new Dictionary<Enums.MiningTiers, Dictionary<AccountId, U>>();

			// this will give us the X lowest hashes among X elected
			foreach(var tier in electedTiers) {
				var primeRepresentatives = tier.Value.Select(r => (r.Key, hash: HashDifficultyUtils.GetBigInteger(r.Value.ElectionHash))).OrderBy(v => v.hash).Take(this.RepresentativeBallotingRules.GetTotal(tier.Key)).Select(r => r.Key);

				// let's select our up to X prime elected
				representativesTiers.Add(tier.Key, tier.Value.Where(r => primeRepresentatives.Contains(r.Key)).ToDictionary());
			}

			return representativesTiers;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}
	}
}