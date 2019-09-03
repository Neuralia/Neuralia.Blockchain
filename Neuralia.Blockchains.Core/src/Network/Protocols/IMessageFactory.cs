using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageFactory {
		IByteArray CreateMessage(IByteArray bytes, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters);
		ISplitMessageEntry WrapBigMessage(IByteArray bytes, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters);
	}
}