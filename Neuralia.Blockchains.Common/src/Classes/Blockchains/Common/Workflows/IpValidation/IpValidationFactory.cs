using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1.Operations.Questions;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation {
	public static class IpValidationFactory {
		public static IValidatorRequest RehydrateRequest(IDataRehydrator rehydrator) {

			// skip the operation code
			rehydrator.SkipByte();
			
			ComponentVersion version = rehydrator.RehydrateRewind<ComponentVersion>();

			IValidatorRequest entry = null;

			if(version == (1, 0)) {
				entry = new ValidatorRequest();
			}
			
			if(entry == null) {
				throw new ApplicationException("Invalid request type");
			}

			entry.Rehydrate(rehydrator);

			return entry;
		}
		
		public static IMinerResponse RehydrateResponse(IDataRehydrator rehydrator) {
			
			ComponentVersion version = rehydrator.RehydrateRewind<ComponentVersion>();

			IMinerResponse entry = null;

			if(version == (1, 0)) {
				entry = new MinerResponse();
			}
			
			if(entry == null) {
				throw new ApplicationException("Invalid response type");
			}

			entry.Rehydrate(rehydrator);

			return entry;
		}
	}
}