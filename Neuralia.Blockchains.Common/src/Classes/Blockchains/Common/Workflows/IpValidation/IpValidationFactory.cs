using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation {
	public static class IpValidationFactory {
		public static (byte version, IValidatorRequest request, IMinerResponse response) RehydrateRequest(IByteArray data) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			// skip the optionsBase
			rehydrator.SkipByte();

			byte version = rehydrator.ReadByte();

			switch(version) {
				case 1:

					return (version, new ValidatorRequest().Rehydrate(rehydrator), new MinerResponse());

				default:

					throw new ApplicationException("Invalid ip validation request version");
			}
		}
	}
}