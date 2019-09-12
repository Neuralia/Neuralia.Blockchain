using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive.V1.TopLowestHashes {
	public class TopLowestHashesRepresentativeBallotingSelector : PassiveRepresentativeBallotingSelector<TopLowestHashesRepresentativeBallotingRules> {

		public TopLowestHashesRepresentativeBallotingSelector(TopLowestHashesRepresentativeBallotingRules representativeBallotingRules) : base(representativeBallotingRules) {
		}

		public override Dictionary<AccountId, IPassiveElectedChoice> SelectRepresentatives(Dictionary<AccountId, IPassiveElectedChoice> elected) {
			if(this.RepresentativeBallotingRules.Version == PassiveRepresentativeBallotingMethodTypes.Instance.TopLowestHashes) {
				return this.PerformTopLowestHashesRepresentativeSelection(elected);
			}

			throw new ApplicationException("Invalid context type");
		}

		protected Dictionary<AccountId, IPassiveElectedChoice> PerformTopLowestHashesRepresentativeSelection(Dictionary<AccountId, IPassiveElectedChoice> elected) {

			var representatives = new Dictionary<AccountId, IPassiveElectedChoice>();

			// this will give us the X lowest hashes among X elected
			var primeRepresentatives = elected.Select(r => (r.Key, hash: new BigInteger(r.Value.ElectionHash.ToExactByteArrayCopy()))).OrderBy(v => v.hash).Take(this.RepresentativeBallotingRules.Amount).Select(r => r.Key);

			// let's select our up to X prime elected
			foreach(var elect in elected.Where(r => primeRepresentatives.Contains(r.Key))) {
				representatives.Add(elect.Key, new PassiveElectedChoice {TransactionIds = elect.Value.TransactionIds, PeerShareType = elect.Value.PeerShareType, ElectionHash = elect.Value.ElectionHash, DelegateAccountId = elect.Value.DelegateAccountId});
			}

			return representatives;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}
	}
}