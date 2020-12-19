using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation {
	public interface IValidatorRequest {
		SafeArrayHandle Secret { get; set; }
		BlockchainType Chain { get; set; }

		public IElectionBlockQuestion  SecondTierQuestion { get; set; }
		public IElectionDigestQuestion DigestQuestion     { get; set; }
		public IElectionBlockQuestion  FirstTierQuestion  { get; set; }
		
		byte                           Version            { get; }
		IValidatorRequest              Rehydrate(IDataRehydrator rehydrator);
	}
}