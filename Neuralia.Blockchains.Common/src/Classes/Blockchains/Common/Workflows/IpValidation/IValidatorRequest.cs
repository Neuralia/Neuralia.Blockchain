using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation {
	public interface IValidatorRequest {
		long Password { get; set; }
		BlockchainType Chain { get; set; }

		byte Version { get; }
		IValidatorRequest Rehydrate(IDataRehydrator rehydrator);
		IByteArray Dehydrate(IDataDehydrator dehydrator);
	}
}