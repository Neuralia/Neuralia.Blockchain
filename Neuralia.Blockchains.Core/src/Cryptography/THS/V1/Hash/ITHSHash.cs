using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Hash {
	public interface ITHSHash : IDisposableExtended {
		int HashType { get; }
		SafeArrayHandle Hash(SafeArrayHandle message);
	}
}