using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	public interface IHashNodeList : IDisposable2{
		//List<IByteArray> Nodes { get; }
		IByteArray this[int i] { get; }
		int Count { get; }
	}
}