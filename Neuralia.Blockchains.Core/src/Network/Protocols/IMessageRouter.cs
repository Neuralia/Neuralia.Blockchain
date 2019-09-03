namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageRouter {
		void HandleCompetedMessage(IMessageEntry entry, ProtocolFactory.CompressedMessageBytesReceived callback, IProtocolTcpConnection connection);
	}
}