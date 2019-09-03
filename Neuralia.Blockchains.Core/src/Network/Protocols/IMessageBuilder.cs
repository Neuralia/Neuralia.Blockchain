using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageBuilder {
		IByteArray BuildTinyMessage(IByteArray message);
		IByteArray BuildSmallMessage(IByteArray message);
		IByteArray BuildMediumMessage(IByteArray message);
		IByteArray BuildLargeMessage(IByteArray message);
		ISplitMessageEntry BuildSplitMessage(IByteArray message);
	}
}