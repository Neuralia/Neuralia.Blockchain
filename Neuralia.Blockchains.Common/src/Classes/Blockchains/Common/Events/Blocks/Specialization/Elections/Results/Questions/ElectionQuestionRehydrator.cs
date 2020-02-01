using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions.V1;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods {
	public static class ElectionQuestionRehydrator {
		public static IElectionQuestion Rehydrate(IDataRehydrator rehydrator) {

			var version = rehydrator.RehydrateRewind<ComponentVersion<ElectionQuestionType>>();

			IElectionQuestion electionQuestion = null;

			if(version.Type == ElectionQuestionTypes.Instance.BlockTransactionIndex) {
				if((version.Major == 1) && (version.Minor == 0)) {
					electionQuestion = new BlockTransactionIdElectionQuestion();
				}
			}
			
			if(version.Type == ElectionQuestionTypes.Instance.BlockByteset) {
				if((version.Major == 1) && (version.Minor == 0)) {
					electionQuestion = new BlockBytesetElectionQuestion();
				}
			}

			if(version.Type == ElectionQuestionTypes.Instance.DigestByteset) {
				if((version.Major == 1) && (version.Minor == 0)) {
					electionQuestion = new DigestBytesetElectionQuestion();
				}
			}
			
			if(electionQuestion == null) {
				throw new ApplicationException("Invalid election question type");
			}

			electionQuestion.Rehydrate(rehydrator);

			return electionQuestion;
		}
	}
}