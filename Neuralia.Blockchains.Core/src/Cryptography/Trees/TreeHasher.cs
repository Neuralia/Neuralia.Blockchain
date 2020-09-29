using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	public abstract class TreeHasher {
		public abstract SafeArrayHandle Hash(IHashNodeList nodes);
	}
}