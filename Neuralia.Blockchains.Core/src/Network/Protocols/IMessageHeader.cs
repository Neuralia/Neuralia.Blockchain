using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageHeader {

		IMessageHash Hash { get; }
		int MessageLength { get; set; }
		int MessageOffset { get; }
		byte Version { get; }
		int GetMaximumHeaderSize();
		void Dehydrate(IDataDehydrator dh);
		void Rehydrate(SafeArrayHandle bytes);
	}
}