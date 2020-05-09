using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1 {
	public interface IActiveFinalElectionResults : IFinalElectionResults {

		List<IActiveRepresentativeBallotingProof> RepresentativeBallotingProof { get; }
	}

	public abstract class ActiveFinalElectionResults : FinalElectionResults, IActiveFinalElectionResults {

		public List<IActiveRepresentativeBallotingProof> RepresentativeBallotingProof { get; } = new List<IActiveRepresentativeBallotingProof>();

		public override void Rehydrate(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree) {
			base.Rehydrate(rehydrator, transactionIndexesTree);

			ushort count = rehydrator.ReadUShort();

			this.RepresentativeBallotingProof.Clear();

			for(int i = 0; i < count; i++) {

				IActiveRepresentativeBallotingProof entry = RepresentativeBallotingMethodRehydrator.RehydrateActiveProof(rehydrator);

				this.RepresentativeBallotingProof.Add(entry);
			}
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetArray("RepresentativeBallotingProof", this.RepresentativeBallotingProof);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.RepresentativeBallotingProof);

			return nodeList;
		}

		protected override ComponentVersion<ElectionContextType> SetIdentity() {
			return (ElectionContextTypes.Instance.Active, 1, 0);
		}
	}
}