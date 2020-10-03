using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation {
	public interface IMinerResponse {

		long?           SecondTierAnswer { get; set; }
		long?    DigestTierAnswer { get; set; }
		long?    FirstTierAnswer  { get; set; }
		
		AccountId       AccountId        { get; set; }
		ResponseType    Response         { get; set; }
		void            Rehydrate(IDataRehydrator rehydrator);
		SafeArrayHandle Dehydrate();
	}
}