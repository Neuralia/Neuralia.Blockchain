namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageRouter {
		void HandleCompletedMessage(IMessageEntry entry, ProtocolFactory.CompressedMessageBytesReceived callback, IProtocolTcpConnection connection);
	}
}