using System.Collections.Generic;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active {

	public interface IActiveRepresentativeBallotingSelector : IJsonSerializable {
		Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> SelectRepresentatives(Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> tierElected, IActiveRepresentativeBallotingProof proof);
		IActiveRepresentativeBallotingApplication PrepareRepresentativeBallotingApplication(IActiveRepresentativeBallotingRules ballotRules);
	}

	public abstract class ActiveRepresentativeBallotingSelector<T> : IActiveRepresentativeBallotingSelector
		where T : IActiveRepresentativeBallotingRules {

		protected readonly T RepresentativeBallotingRules;

		public ActiveRepresentativeBallotingSelector(T representativeBallotingRules) {
			this.RepresentativeBallotingRules = representativeBallotingRules;
		}

		public abstract Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> SelectRepresentatives(Dictionary<Enums.MiningTiers, Dictionary<AccountId, IActiveElectedChoice>> tierElected, IActiveRepresentativeBallotingProof proof);

		public abstract IActiveRepresentativeBallotingApplication PrepareRepresentativeBallotingApplication(IActiveRepresentativeBallotingRules ballotRules);

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {
		}
	}
}