using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation {
	public interface IMinerResponse {

		AccountId AccountId { get; set; }
		ResponseType Response { get; set; }
		void Rehydrate(IDataRehydrator rehydrator);
		IByteArray Dehydrate();
	}
}