using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active.V1.EncryptedSecret {
	public class EncryptedSecretRepresentativeBallotingSelector : ActiveRepresentativeBallotingSelector<EncryptedSecretRepresentativeBallotingRules> {

		public EncryptedSecretRepresentativeBallotingSelector(EncryptedSecretRepresentativeBallotingRules representativeBallotingRules) : base(representativeBallotingRules) {
		}

		public override Dictionary<AccountId, IActiveElectedChoice> SelectRepresentatives(Dictionary<AccountId, IActiveElectedChoice> elected, IActiveRepresentativeBallotingProof proof) {
			if(this.RepresentativeBallotingRules.Version == ActiveRepresentativeBallotingMethodTypes.Instance.EncryptedSecret) {
				return this.PerformEncryptedSecretRepresentativeSelection(elected, proof);
			}

			throw new ApplicationException("Invalid context type");
		}

		public override IActiveRepresentativeBallotingApplication PrepareRepresentativeBallotingApplication(IActiveRepresentativeBallotingRules ballotRules) {

			EncryptedSecretRepresentativeBallotingApplication application = new EncryptedSecretRepresentativeBallotingApplication();

			//TODO: what should we prepare here?

			return application;
		}

		protected Dictionary<AccountId, IActiveElectedChoice> PerformEncryptedSecretRepresentativeSelection(Dictionary<AccountId, IActiveElectedChoice> elected, IActiveRepresentativeBallotingProof proof) {

			var representatives = new Dictionary<AccountId, IActiveElectedChoice>();

			// this will give us the X lowest hashes among X elected
			var primeRepresentatives = elected.Select(r => (r.Key, hash: new BigInteger(r.Value.ElectionHash.ToExactByteArrayCopy()))).OrderBy(v => v.hash).Take(this.RepresentativeBallotingRules.Amount).Select(r => r.Key);

			// let's select our up to X prime elected
			foreach(var elect in elected.Where(r => primeRepresentatives.Contains(r.Key))) {
				var entry = new ActiveElectedChoice {TransactionIds = elect.Value.TransactionIds, PeerShareType = elect.Value.PeerShareType, DelegateAccountId = elect.Value.DelegateAccountId};
				entry.ElectionHash.Entry = elect.Value.ElectionHash.Entry;
				representatives.Add(elect.Key, entry);
			}

			return representatives;
		}

		private void PrepareRepresentativeBallotingApplication() {

		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}
	}
}