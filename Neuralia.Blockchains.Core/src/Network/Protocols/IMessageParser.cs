using Neuralia.Blockchains.Tools.General.ExclusiveOptions;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageParser {
		IMessageEntry RehydrateHeader(ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters);
	}
}