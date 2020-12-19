using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	public abstract class TreeHasher {
		public abstract SafeArrayHandle Hash(IHashNodeList nodes);
	}
}