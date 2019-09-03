using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageHash {
		void WriteHash(IDataDehydrator dh);
		void ReadHash(IDataRehydrator dh);
		void SetHash(IByteArray mesasge);
		bool CompareHash(IByteArray messasge);
	}

	public interface IMessageHash<out T> : IMessageHash {
		T Hash { get; }
		T HashMessage(IByteArray messasge);
	}
}