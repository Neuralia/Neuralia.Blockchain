using System.Collections.Generic;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive {

	public interface IPassiveRepresentativeBallotingSelector : IJsonSerializable {
		Dictionary<AccountId, IPassiveElectedChoice> SelectRepresentatives(Dictionary<AccountId, IPassiveElectedChoice> elected);
	}

	public abstract class PassiveRepresentativeBallotingSelector<T> : IPassiveRepresentativeBallotingSelector
		where T : IPassiveRepresentativeBallotingRules {

		protected readonly T RepresentativeBallotingRules;

		public PassiveRepresentativeBallotingSelector(T representativeBallotingRules) {
			this.RepresentativeBallotingRules = representativeBallotingRules;
		}

		public abstract Dictionary<AccountId, IPassiveElectedChoice> SelectRepresentatives(Dictionary<AccountId, IPassiveElectedChoice> elected);

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {
		}
	}
}