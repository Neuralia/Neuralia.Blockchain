using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active {
	public interface IActiveRepresentativeBallotingRules : IRepresentativeBallotingRules<ActiveRepresentativeBallotingMethodType> {
	}

	/// <summary>
	///     By what method do we select who will get to be the prime elected candidate and the representative of the election
	///     and by what rules should we operate
	/// </summary>
	public abstract class ActiveRepresentativeBallotingRules : RepresentativeBallotingRules<ActiveRepresentativeBallotingMethodType>, IActiveRepresentativeBallotingRules {

		public ActiveRepresentativeBallotingRules() {

		}

		public ActiveRepresentativeBallotingRules(Dictionary<Enums.MiningTiers, ushort> miningTierTotals) : base(miningTierTotals) {

		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}
	}
}