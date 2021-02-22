using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1.Operations.Questions {
	
	public class ValidatorOperationsQuestionsResponse : Versionable<IPValidatorOperationType>, IValidatorOperationResponse {
		long? SecondTierAnswer { get; set; }
		long? DigestTierAnswer { get; set; }
		long? FirstTierAnswer { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);
			
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			
			dehydrator.Write(this.SecondTierAnswer != null);

			if(this.SecondTierAnswer != null) {
				tool.Value = this.SecondTierAnswer.Value;
				tool.Dehydrate(dehydrator);
			}
			
			dehydrator.Write(this.DigestTierAnswer != null);

			if(this.DigestTierAnswer != null) {
				tool.Value = this.DigestTierAnswer.Value;
				tool.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.FirstTierAnswer != null);

			if(this.FirstTierAnswer != null) {
				tool.Value = this.FirstTierAnswer.Value;
				tool.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
			this.SecondTierAnswer = null;
			bool isAnswerSet = rehydrator.ReadBool();

			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			
			if(isAnswerSet) {
				tool.Rehydrate(rehydrator);
				this.SecondTierAnswer = tool.Value;
			}

			this.DigestTierAnswer = null;
			isAnswerSet           = rehydrator.ReadBool();

			if(isAnswerSet) {
				tool.Rehydrate(rehydrator);
				this.DigestTierAnswer = tool.Value;
			}
			
			this.FirstTierAnswer = null;
			isAnswerSet = rehydrator.ReadBool();

			if(isAnswerSet) {
				tool.Rehydrate(rehydrator);
				this.FirstTierAnswer = tool.Value;
			}
		}

		protected override ComponentVersion<IPValidatorOperationType> SetIdentity() {
			return (IPValidatorOperationTypes.Instance.Questions, 1, 0);
		}
	}
}