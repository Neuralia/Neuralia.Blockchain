using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1 {

	public class ValidatorRequest : IValidatorRequest {

		public long Password { get; set; }

		public BlockchainType Chain { get; set; }

		public byte Version => 1;

		public IElectionQuestion Question { get; set; }

		public IValidatorRequest Rehydrate(IDataRehydrator rehydrator) {

			int version = rehydrator.ReadByte();
			this.Password = rehydrator.ReadLong();
			this.Chain = rehydrator.ReadUShort();

			bool isQuestionSet = rehydrator.ReadBool();

			if(isQuestionSet) {
				this.Question = ElectionQuestionRehydrator.Rehydrate(rehydrator);
			}

			return this;
		}
	}
}