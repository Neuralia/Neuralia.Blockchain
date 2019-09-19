using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageFactory {
		SafeArrayHandle CreateMessage(SafeArrayHandle bytes, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters);
		ISplitMessageEntry WrapBigMessage(SafeArrayHandle bytes, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters);
	}
}