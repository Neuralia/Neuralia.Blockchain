using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	public interface IHashNodeList {
		//List<ArrayWrapper> Nodes { get; }
		SafeArrayHandle this[int i] { get; }
		int Count { get; }
	}
}