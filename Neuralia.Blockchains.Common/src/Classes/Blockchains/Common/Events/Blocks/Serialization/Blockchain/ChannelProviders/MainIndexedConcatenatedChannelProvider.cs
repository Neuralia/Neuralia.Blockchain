using System.IO;
using System.IO.Abstractions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders {
	/// <summary>
	///     This provider ensures that the file is concatenated in a single file for all blocks, and their content indexed in
	///     the main block index
	/// </summary>
	public class MainIndexedConcatenatedChannelProvider : ChannelProvider {

		private const string DATA_FILE = "Data";
		private const string FILE_NAME_TEMPLATE = "{0}.neuralia";

		protected readonly string filename;

		public MainIndexedConcatenatedChannelProvider(string filename, string folderPath, IFileSystem fileSystem) : base(filename, folderPath, fileSystem) {
			this.filename = filename;
		}

		public FileSpecs DataFile => this.fileSpecs[DATA_FILE];

		protected override void ResetAllFileSpecs(uint adjustedBlockId, (int index, long startingBlockId) blockIndex) {

			this.fileSpecs.Add(DATA_FILE, new FileSpecs(this.GetFile(blockIndex), this.fileSystem));
		}

		public string GetFile((int index, long startingBlockId) blockIndex) {

			return Path.Combine(this.GetBlocksIndexFolderPath(blockIndex.index), this.GetFileName());
		}

		public string GetFileName() {

			return string.Format(FILE_NAME_TEMPLATE, this.filename);
		}
	}
}