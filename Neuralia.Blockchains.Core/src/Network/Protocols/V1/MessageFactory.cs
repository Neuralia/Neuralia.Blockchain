using System;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Large;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Medium;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Small;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Tiny;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1 {
	public class MessageFactory : IMessageFactory {

		public IByteArray CreateMessage(IByteArray bytes, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters) {
			IMessageBuilder messageBuilder = new MessageBuilder();

			// here we compress our message, this is what will be sent on the wire
			if(bytes.Length <= TinyMessageHeader.MAXIMUM_SIZE) {

				if(protocolMessageFilters.MissesOption(TcpConnection.ProtocolMessageTypes.Tiny)) {
					throw new ApplicationException("Tiny messages are proscribed by the filtering rules");
				}

				return messageBuilder.BuildTinyMessage(bytes);
			}

			if(bytes.Length <= SmallMessageHeader.MAXIMUM_SIZE) {

				if(protocolMessageFilters.MissesOption(TcpConnection.ProtocolMessageTypes.Small)) {
					throw new ApplicationException("Small messages are proscribed by the filtering rules");
				}

				return messageBuilder.BuildSmallMessage(bytes);
			}

			if(bytes.Length <= MediumMessageHeader.MAXIMUM_SIZE) {

				if(protocolMessageFilters.MissesOption(TcpConnection.ProtocolMessageTypes.Medium)) {
					throw new ApplicationException("Medium messages are proscribed by the filtering rules");
				}

				return messageBuilder.BuildMediumMessage(bytes);
			}

			if(bytes.Length <= LargeMessageHeader.MAXIMUM_SIZE) {

				if(protocolMessageFilters.MissesOption(TcpConnection.ProtocolMessageTypes.Large)) {
					throw new ApplicationException("Large messages are proscribed by the filtering rules");
				}

				return messageBuilder.BuildLargeMessage(bytes);
			}

			return null;
		}

		public ISplitMessageEntry WrapBigMessage(IByteArray bytes, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters) {

			if(protocolMessageFilters.MissesOption(TcpConnection.ProtocolMessageTypes.Split)) {
				throw new ApplicationException("Split messages are proscribed by the filtering rules");
			}

			if(bytes.Length < SplitMessageHeader.MINIMUM_MESSAGE_SIZE) {
				throw new ApplicationException("Message is too small to use split messages");
			}

			if(bytes.Length > SplitMessageHeader.MAXIMUM_MESSAGE_SIZE) {
				throw new ApplicationException("Message is too large to use split messages");
			}

			IMessageBuilder messageBuilder = new MessageBuilder();

			return messageBuilder.BuildSplitMessage(bytes);
		}
	}

}