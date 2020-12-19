using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.V1 {
	public interface IActiveElectionContext : IElectionContext {
		List<IRepresentativeBallotingRules> RepresentativeBallotingRules { get; }
	}

	public abstract class ActiveElectionContext : ElectionContext, IActiveElectionContext {

		/// <summary>
		///     How do we perform the subsequent representatives elections. we allow both active and passive types in an active
		///     election
		/// </summary>
		public List<IRepresentativeBallotingRules> RepresentativeBallotingRules { get; } = new List<IRepresentativeBallotingRules>();

		public override void Rehydrate(IDataRehydrator rehydrator, IElectionContextRehydrationFactory electionContextRehydrationFactory) {

			base.Rehydrate(rehydrator, electionContextRehydrationFactory);

			this.RepresentativeBallotingRules.Clear();
			byte count = rehydrator.ReadByte();

			for(byte i = 0; i < count; i++) {
				this.RepresentativeBallotingRules.Add(RepresentativeBallotingMethodRehydrator.RehydrateActiveRules(rehydrator));
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
			return (ElectionContextTypes.Instance.Active, 1, 0);
		}
	}
}