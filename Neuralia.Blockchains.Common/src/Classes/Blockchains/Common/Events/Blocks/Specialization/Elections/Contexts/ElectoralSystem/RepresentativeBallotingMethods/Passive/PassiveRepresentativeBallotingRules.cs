using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive {
	public interface IPassiveRepresentativeBallotingRules : IRepresentativeBallotingRules<PassiveRepresentativeBallotingMethodType> {
	}

	/// <summary>
	///     By what method do we select who will get to be the prime elected candidate and the representative of the election
	///     and by what rules should we operate
	/// </summary>
	public abstract class PassiveRepresentativeBallotingRules : RepresentativeBallotingRules<PassiveRepresentativeBallotingMethodType>, IPassiveRepresentativeBallotingRules {

		public PassiveRepresentativeBallotingRules() {

		}
		
		public PassiveRepresentativeBallotingRules(ushort firstTierTotal, ushort secondTierTotal, ushort thirdTierTotal) : base(firstTierTotal, secondTierTotal, thirdTierTotal) {

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