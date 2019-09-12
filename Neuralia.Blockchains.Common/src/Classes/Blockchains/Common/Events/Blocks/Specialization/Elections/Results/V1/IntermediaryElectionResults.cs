using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1 {
	public interface IIntermediaryElectionResults : IElectionResult {
		
		IElectedResults CreateElectedResult();
		IElectionQuestion SimpleQuestion { get; set; }
		IElectionQuestion HardQuestion { get; set; }
	}

	public abstract class IntermediaryElectionResults : ElectionResult, IIntermediaryElectionResults {

		public IElectionQuestion SimpleQuestion { get; set; }
		public IElectionQuestion HardQuestion { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree) {
			base.Rehydrate(rehydrator, transactionIndexesTree);

			bool simpleQuestionSet = rehydrator.ReadBool();

			if(simpleQuestionSet) {
				this.SimpleQuestion = ElectionQuestionRehydrator.Rehydrate(rehydrator);
			}
			
			bool hardQuestionSet = rehydrator.ReadBool();

			if(hardQuestionSet) {
				this.HardQuestion = ElectionQuestionRehydrator.Rehydrate(rehydrator);
			}
		}

		public abstract IElectedResults CreateElectedResult();

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();
			

			return nodeList;
		}
	}
}