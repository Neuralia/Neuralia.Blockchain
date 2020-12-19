using System.Threading.Tasks;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageRouter {
		Task HandleCompletedMessage(IMessageEntry entry, ProtocolFactory.CompressedMessageBytesReceived callback, IProtocolTcpConnection connection);
	}
}