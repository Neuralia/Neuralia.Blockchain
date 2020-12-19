using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Crypto {
	public interface ITHSCrypto : IDisposableExtended {
		void EncryptStringToBytes(SafeArrayHandle message, SafeArrayHandle encrypted);
	}
}