using System.IO;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders {
	public class ErasableFileChannelProvider : IndependentFileChannelProvider {

		public ErasableFileChannelProvider(string filename, string folderPath, FileSystemWrapper fileSystem) : base(filename, folderPath, fileSystem) {
		}

		protected override string GetBlocksIndexFolderPath(long index) {

			return Path.Combine(base.GetBlocksIndexFolderPath(index), "erasables");
		}
	}
}