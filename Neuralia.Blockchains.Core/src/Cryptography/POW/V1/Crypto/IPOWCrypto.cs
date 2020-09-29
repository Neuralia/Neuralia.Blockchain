using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.POW.V1.Crypto {
	public interface IPOWCrypto : IDisposableExtended {
		void EncryptStringToBytes(SafeArrayHandle message, SafeArrayHandle encrypted);
	}
}