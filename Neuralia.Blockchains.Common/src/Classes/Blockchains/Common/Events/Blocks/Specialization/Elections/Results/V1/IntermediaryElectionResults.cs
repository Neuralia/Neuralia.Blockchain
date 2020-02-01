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
		IElectionBlockQuestion SecondTierQuestion { get; set; }
		IElectionDigestQuestion DigestQuestion { get; set; }
		IElectionBlockQuestion FirstTierQuestion { get; set; }
	}

	public abstract class IntermediaryElectionResults : ElectionResult, IIntermediaryElectionResults {

		public IElectionBlockQuestion SecondTierQuestion { get; set; }
		public IElectionDigestQuestion DigestQuestion { get; set; }
		public IElectionBlockQuestion FirstTierQuestion { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree) {
			base.Rehydrate(rehydrator, transactionIndexesTree);

			bool questionSet = rehydrator.ReadBool();

			if(questionSet) {
				this.SecondTierQuestion = ElectionQuestionRehydrator.Rehydrate(rehydrator) as IElectionBlockQuestion;
			}
			
			questionSet = rehydrator.ReadBool();

			if(questionSet) {
				this.DigestQuestion = ElectionQuestionRehydrator.Rehydrate(rehydrator) as IElectionDigestQuestion;
			}
			
			questionSet= rehydrator.ReadBool();

			if(questionSet) {
				this.FirstTierQuestion = ElectionQuestionRehydrator.Rehydrate(rehydrator) as IElectionBlockQuestion;
			}
		}

		public abstract IElectedResults CreateElectedResult();

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("SecondTierQuestion", this.SecondTierQuestion);
			jsonDeserializer.SetProperty("FirstTierQuestion", this.FirstTierQuestion);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.SecondTierQuestion);
			nodeList.Add(this.FirstTierQuestion);
			
			return nodeList;
		}
	}
}