using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils {
	public abstract class ChannelFilesHandler {
		protected readonly Dictionary<string, FileSpecs> fileSpecs = new Dictionary<string, FileSpecs>();
		protected readonly IFileSystem fileSystem;

		protected readonly string folderPath;

		protected uint? adjustedBlockId;
		protected (int index, long startingBlockId) blockIndex;

		public ChannelFilesHandler(string folderPath, IFileSystem fileSystem) {
			this.folderPath = folderPath;
			this.fileSystem = fileSystem;
		}

		public void ResetFileSpecs(uint adjustedBlockId, (int index, long startingBlockId) blockIndex) {

			this.adjustedBlockId = adjustedBlockId;
			this.blockIndex = blockIndex;

			this.fileSpecs.Clear();

			this.ResetAllFileSpecs(adjustedBlockId, blockIndex);

			this.EnsureFilesCreated();
		}

		protected abstract void ResetAllFileSpecs(uint adjustedBlockId, (int index, long startingBlockId) blockIndex);

		public virtual void EnsureFilesCreated() {

			foreach(FileSpecs fileSpec in this.fileSpecs.Values) {
				fileSpec.EnsureFilesExist();
			}
		}

		protected virtual string GetBlocksIndexFolderPath(int index) {

			return Path.Combine(this.folderPath, $"{index}");
		}
	}
}