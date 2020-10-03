using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1 {
	public class MessageRouter : IMessageRouter {

		public async Task HandleCompletedMessage(IMessageEntry messageEntry, ProtocolFactory.CompressedMessageBytesReceived callback, IProtocolTcpConnection connection) {

			if(messageEntry.Version != MessageBuilder.ProtocolVersion.Version) {
				throw new NotSupportedException("Unsupported protocol version");
			}

			if(messageEntry is ISplitMessageEntry largeMessageEntry) {
				this.HandleNewLargeMessage(largeMessageEntry, connection);

			} else if(messageEntry is ISliceRequestMessageEntry requestSliceMessageEntry) {

				this.HandleLargeMessageSliceRequest(requestSliceMessageEntry, connection);
			} else if(messageEntry is ISliceResponseMessageEntry responseSliceMessageEntry) {
				this.HandleLargeMessageSliceResponse(responseSliceMessageEntry, callback, connection);
			} else {
				if(messageEntry.IsComplete) {
					await callback(messageEntry.Message.Branch()).ConfigureAwait(false);
				}
			}
		}

		private void HandleNewLargeMessage(ISplitMessageEntry splitMessageEntry, IProtocolTcpConnection connection) {

			ISplitMessageEntry splitMessage = MessageCaches.ReceiveCaches.Get(splitMessageEntry.Hash);

			if(splitMessage == null) {

				// ensure it is cached
				MessageCaches.ReceiveCaches.AddEntry(splitMessageEntry);
			}

			// now we  request the missing pieces
			this.RequestLargeMessagePiece(splitMessageEntry, connection);
		}

		private void HandleLargeMessageSliceRequest(ISliceRequestMessageEntry requestSliceMessageEntry, IProtocolTcpConnection connection) {

			ISplitMessageEntry splitMessage = MessageCaches.SendCaches.Get(requestSliceMessageEntry.LargeMessageHash);

			if(splitMessage == null) {
				//TODO: what to do when message is not cached anymore?
				throw new ApplicationException("Client is requesting a large message that is not cached anymore");
			}

			// now we  request the missing pieces
			this.SendLargeMessagePiece(requestSliceMessageEntry, splitMessage, connection);
		}

		private void HandleLargeMessageSliceResponse(ISliceResponseMessageEntry responseSliceMessageEntry, ProtocolFactory.CompressedMessageBytesReceived callback, IProtocolTcpConnection connection) {

			ISplitMessageEntry splitMessageEntry = MessageCaches.ReceiveCaches.Get(responseSliceMessageEntry.LargeMessageHash);

			if(splitMessageEntry == null) {
				//TODO: what to do when message is not cached anymore?
				throw new ApplicationException("Server is responding to a large message that is not chached");
			}

			splitMessageEntry.SetSliceData(responseSliceMessageEntry);

			if(splitMessageEntry.IsComplete) {

				SafeArrayHandle assembledMessage = splitMessageEntry.AssembleCompleteMessage();

				//TODO: this is over, trigger the message complete event
				callback(assembledMessage, splitMessageEntry);

				return;
			}

			// now we  request the next missing pieces
			this.RequestLargeMessagePiece(splitMessageEntry, connection);
		}

		private void RequestLargeMessagePiece(ISplitMessageEntry splitMessageEntry, IProtocolTcpConnection connection) {

			if(splitMessageEntry.IsComplete) {
				//TODO: this is over, trigger the completion
				return;
			}

			using(SafeArrayHandle request = splitMessageEntry.CreateNextSliceRequestMessage()) {

				connection.SendSocketBytes(request);
			}
		}

		private void SendLargeMessagePiece(ISliceRequestMessageEntry requestSliceMessageEntry, ISplitMessageEntry splitMessageEntry, IProtocolTcpConnection connection) {

			using(SafeArrayHandle response = splitMessageEntry.CreateSliceResponseMessage(requestSliceMessageEntry)) {

				connection.SendSocketBytes(response);
			}
		}
	}
}