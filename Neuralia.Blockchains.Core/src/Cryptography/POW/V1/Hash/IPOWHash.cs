using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.POW.V1.Hash {
	public interface IPOWHash : IDisposableExtended {
		SafeArrayHandle Hash(SafeArrayHandle message);
		int HashType { get; }
	}
}