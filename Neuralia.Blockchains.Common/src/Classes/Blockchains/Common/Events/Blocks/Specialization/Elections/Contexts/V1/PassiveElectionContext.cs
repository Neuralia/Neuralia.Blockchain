using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.V1 {

	public interface IPassiveElectionContext : IElectionContext {
		List<IRepresentativeBallotingRules> RepresentativeBallotingRules { get; }
	}

	public class PassiveElectionContext : ElectionContext, IPassiveElectionContext {

		/// <summary>
		///     How do we perform the subsequent representatives elections
		/// </summary>
		public List<IRepresentativeBallotingRules> RepresentativeBallotingRules { get; } = new List<IRepresentativeBallotingRules>();

		public override void Rehydrate(IDataRehydrator rehydrator, IElectionContextRehydrationFactory electionContextRehydrationFactory) {

			base.Rehydrate(rehydrator, electionContextRehydrationFactory);

			this.RepresentativeBallotingRules.Clear();
			byte count = rehydrator.ReadByte();

			for(byte i = 0; i < count; i++) {
				this.RepresentativeBallotingRules.Add(RepresentativeBallotingMethodRehydrator.RehydratePassiveRules(rehydrator));
			}
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.RepresentativeBallotingRules.Count);

			foreach(IRepresentativeBallotingRules ballot in this.RepresentativeBallotingRules) {
				nodeList.Add(ballot);
			}

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetArray("RepresentativeBallotingRules", this.RepresentativeBallotingRules);
		}

		protected override ComponentVersion<ElectionContextType> SetIdentity() {
			return (ElectionContextTypes.Instance.Passive, 1, 0);
		}
	}
}