
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Core.Tools;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders {
	public abstract class ChannelProvider : ChannelFilesHandler {

		private const string FILE_NAME_TEMPLATE = "{0}.neuralia";

		protected ChannelProvider(string filename, string folderPath, FileSystemWrapper fileSystem) : base(folderPath, fileSystem) {
		}
	}
}