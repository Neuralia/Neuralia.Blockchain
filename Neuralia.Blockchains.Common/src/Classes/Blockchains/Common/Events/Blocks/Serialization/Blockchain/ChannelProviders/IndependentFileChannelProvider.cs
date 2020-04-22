using System.IO;

using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Core.Tools;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders {
	/// <summary>
	///     This provider ensures that the data is saved in its own file, one per block
	/// </summary>
	public class IndependentFileChannelProvider : ChannelProvider {

		private const string DATA_FILE = "Data";
		private const string FILE_NAME_TEMPLATE = "{0}.{1}.neuralia";

		protected readonly string filename;

		public IndependentFileChannelProvider(string filename, string folderPath, FileSystemWrapper fileSystem) : base(filename, folderPath, fileSystem) {
			this.filename = filename;
		}

		public FileSpecs DataFile => this.fileSpecs[DATA_FILE];

		protected override void ResetAllFileSpecs(uint adjustedBlockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			this.fileSpecs.Add(DATA_FILE, new FileSpecs(this.GetFile(adjustedBlockId, blockIndex), this.fileSystem));
		}

		public string GetFile(uint adjustedBlockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return Path.Combine(this.GetBlocksIndexFolderPath(blockIndex.index), this.GetFileName(adjustedBlockId));
		}

		public string GetFileName(uint adjustedBlockId) {

			return string.Format(FILE_NAME_TEMPLATE, this.filename, adjustedBlockId);
		}
	}
}