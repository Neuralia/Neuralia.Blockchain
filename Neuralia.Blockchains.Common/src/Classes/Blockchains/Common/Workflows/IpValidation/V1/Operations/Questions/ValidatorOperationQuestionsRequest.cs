using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1.Operations.Questions {
	public class ValidatorOperationQuestionsRequest : Versionable<IPValidatorOperationType>, IValidatorOperationRequest {
		public IElectionBlockQuestion SecondTierQuestion { get; set; }
		public IElectionDigestQuestion DigestQuestion { get; set; }
		public IElectionBlockQuestion FirstTierQuestion { get; set; }

		public Action<IDataDehydrator, ValidatorOperationQuestionsRequest> DehydrationCallback { get; set; }
		
		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
			this.SecondTierQuestion = null;
			bool isQuestionSet = rehydrator.ReadBool();

			if(isQuestionSet) {
				this.SecondTierQuestion = ElectionQuestionRehydrator.Rehydrate(rehydrator) as IElectionBlockQuestion;
			}

			isQuestionSet = rehydrator.ReadBool();

			if(isQuestionSet) {
				this.DigestQuestion = ElectionQuestionRehydrator.Rehydrate(rehydrator) as IElectionDigestQuestion;
			}

			isQuestionSet = rehydrator.ReadBool();

			if(isQuestionSet) {
				this.FirstTierQuestion = ElectionQuestionRehydrator.Rehydrate(rehydrator) as IElectionBlockQuestion;
			}
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.DehydrationCallback(dehydrator, this);
		}

		protected override ComponentVersion<IPValidatorOperationType> SetIdentity() {
			return (IPValidatorOperationTypes.Instance.Questions, 1, 0);
		}
	}
}