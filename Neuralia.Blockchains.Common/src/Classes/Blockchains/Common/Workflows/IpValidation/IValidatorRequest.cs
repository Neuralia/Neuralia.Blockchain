using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation {
	public interface IValidatorRequest : IVersionable{
		SafeArrayHandle Secret { get; set; }
		BlockchainType Chain { get; set; }

		public List<IValidatorOperationRequest> Operations { get;  }
	}
}