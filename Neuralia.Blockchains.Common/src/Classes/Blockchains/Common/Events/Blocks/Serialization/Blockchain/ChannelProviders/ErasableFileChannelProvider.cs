using System.IO;
using System.IO.Abstractions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders {
	public class ErasableFileChannelProvider : IndependentFileChannelProvider {

		public ErasableFileChannelProvider(string filename, string folderPath, IFileSystem fileSystem) : base(filename, folderPath, fileSystem) {
		}

		protected override string GetBlocksIndexFolderPath(int index) {

			return Path.Combine(base.GetBlocksIndexFolderPath(index), "erasables");
		}
	}
}