using System;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS {

	[Flags]
	public enum XMSSOperationModes {
		Signature = 1 << 0,
		Verification = 1 << 1,
		Both = Signature | Verification
	}
}