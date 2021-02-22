using System.Collections.Generic;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation {
	public interface IMinerResponse : IVersionable {

		public List<IValidatorOperationResponse> Operations { get; }
		
		AccountId AccountId { get; set; }
		int ResponseCode { get; set; }
		ResponseType Response { get; set; }
		SoftwareVersion Compatibility { get; } 
		SafeArrayHandle Dehydrate();
	}
}