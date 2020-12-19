using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.Gates {

	public interface IStandardAccountGates : IAccountGates{
		
		byte[] TransactionKeyGate { get; set; }
		byte[] MessageKeyGate { get; set; }
		byte[] ChangeKeyGate { get; set; }
		byte[] SuperKeyGate { get; set; }
		byte[] ValidatorSignatureKeyGate { get; set; }
		byte[] ValidatorSecretKeyGate { get; set; }
	}
}