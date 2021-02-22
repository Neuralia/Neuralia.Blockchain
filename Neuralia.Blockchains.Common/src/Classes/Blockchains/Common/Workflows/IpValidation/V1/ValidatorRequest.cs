using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1 {

	public class ValidatorRequest : Versionable, IValidatorRequest {

		public List<IValidatorOperationRequest> Operations { get; } = new List<IValidatorOperationRequest>();

		public SafeArrayHandle Secret { get; set; }

		public BlockchainType Chain { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.Secret);
			dehydrator.Write(this.Chain.Value);
			
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();

			tool.Value = this.Operations.Count;
			tool.Dehydrate(dehydrator);

			foreach(var operation in this.Operations) {
				operation.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
			this.Secret = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			this.Chain = rehydrator.ReadUShort();
			
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();

			this.Operations.Clear();
			tool.Rehydrate(rehydrator);
			int count = tool.Value;

			for(int i = 0; i < count; i++) {

				IValidatorOperationRequest operation = ValidatorOperationRehydrator.RehydrateRequest(rehydrator);
				
				operation.Rehydrate(rehydrator);

				this.Operations.Add(operation);
			}
		}

		protected override ComponentVersion SetIdentity() {
			return (1,0);
		}
	}
}