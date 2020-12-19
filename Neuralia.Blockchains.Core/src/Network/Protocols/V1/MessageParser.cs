using System;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Large;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Medium;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Small;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split.Messages;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Tiny;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1 {
	public class MessageParser : IMessageParser {
		private readonly SafeArrayHandle buffer;

		private readonly object locker = new object();

		public MessageParser(SafeArrayHandle buffer) {
			this.buffer = buffer;
		}

		public IMessageEntry RehydrateHeader(ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters) {

			// byte 0 is the protocol version.

			MessageHeader.MessageTypes messageType = (MessageHeader.MessageTypes) this.buffer[1];

			int offset = 2; // 1 because we skip the protocol and the message type bytes

			IMessageEntry messageEntry = null;

			// if we have message types that store the header size, lets read it here
			switch(messageType) {
				case MessageHeader.MessageTypes.Tiny:

					if(!protocolMessageFilters.HasOption(TcpConnection.ProtocolMessageTypes.Tiny)) {
						throw new ApplicationException("Message type is not supported by the protocol");
					}

					messageEntry = new TinyMessageEntry();

					break;

				case MessageHeader.MessageTypes.Small:

					if(!protocolMessageFilters.HasOption(TcpConnection.ProtocolMessageTypes.Small)) {
						throw new ApplicationException("Message type is not supported by the protocol");
					}

					messageEntry = new SmallMessageEntry();

					break;

				case MessageHeader.MessageTypes.Medium:

					if(!protocolMessageFilters.HasOption(TcpConnection.ProtocolMessageTypes.Medium)) {
						throw new ApplicationException("Message type is not supported by the protocol");
					}

					messageEntry = new MediumMessageEntry();

					break;

				case MessageHeader.MessageTypes.Large:

					if(!protocolMessageFilters.HasOption(TcpConnection.ProtocolMessageTypes.Large)) {
						throw new ApplicationException("Message type is not supported by the protocol");
					}

					messageEntry = new LargeMessageEntry();

					break;

				case MessageHeader.MessageTypes.Split:

					if(!protocolMessageFilters.HasOption(TcpConnection.ProtocolMessageTypes.Split)) {
						throw new ApplicationException("Message type is not supported by the protocol");
					}

					messageEntry = new SplitMessageEntry();

					break;

				case MessageHeader.MessageTypes.SplitSliceRequest:

					if(!protocolMessageFilters.HasOption(TcpConnection.ProtocolMessageTypes.Split)) {
						throw new ApplicationException("Message type is not supported by the protocol");
					}

					messageEntry = new SliceRequestMessageEntry();

					break;

				case MessageHeader.MessageTypes.SplitSliceResponse:

					if(!protocolMessageFilters.HasOption(TcpConnection.ProtocolMessageTypes.Split)) {
						throw new ApplicationException("Message type is not supported by the protocol");
					}

					messageEntry = new SliceResponseMessageEntry();

					break;

				default:

					throw new ApplicationException("Unsupported header type");
			}

			messageEntry.RebuildHeader(this.buffer.Branch());

			return messageEntry;

		}
	}
}