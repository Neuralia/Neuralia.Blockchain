using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public interface IMessageBuilder {
		SafeArrayHandle BuildTinyMessage(SafeArrayHandle message);
		SafeArrayHandle BuildSmallMessage(SafeArrayHandle message);
		SafeArrayHandle BuildMediumMessage(SafeArrayHandle message);
		SafeArrayHandle BuildLargeMessage(SafeArrayHandle message);
		ISplitMessageEntry BuildSplitMessage(SafeArrayHandle message);
	}
}