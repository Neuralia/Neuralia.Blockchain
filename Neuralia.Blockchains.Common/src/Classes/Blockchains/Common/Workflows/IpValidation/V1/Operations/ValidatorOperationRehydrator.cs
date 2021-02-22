using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1.Operations.Questions;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods {
	public static class ValidatorOperationRehydrator {

		public static IValidatorOperationRequest RehydrateRequest(IDataRehydrator rehydrator) {

			ComponentVersion<IPValidatorOperationType> version = rehydrator.RehydrateRewind<ComponentVersion<IPValidatorOperationType>>();

			IValidatorOperationRequest entry = null;

			if(version.Type == IPValidatorOperationTypes.Instance.Questions) {
				if(version == (1, 0)) {
					entry = new ValidatorOperationQuestionsRequest();
				}
			}

			if(entry == null) {
				throw new ApplicationException("Invalid request type");
			}

			entry.Rehydrate(rehydrator);

			return entry;
		}
		
		public static IValidatorOperationResponse RehydrateResponse(IDataRehydrator rehydrator) {

			ComponentVersion<IPValidatorOperationType> version = rehydrator.RehydrateRewind<ComponentVersion<IPValidatorOperationType>>();

			IValidatorOperationResponse entry = null;

			if(version.Type == IPValidatorOperationTypes.Instance.Questions) {
				if(version == (1, 0)) {
					entry = new ValidatorOperationsQuestionsResponse();
				}
			}

			if(entry == null) {
				throw new ApplicationException("Invalid response type");
			}

			entry.Rehydrate(rehydrator);

			return entry;
		}
	}
}