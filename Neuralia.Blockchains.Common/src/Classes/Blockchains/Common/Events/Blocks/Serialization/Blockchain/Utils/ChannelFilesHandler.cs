using System.Collections.Generic;
using System.IO;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils {
	public abstract class ChannelFilesHandler {
		protected readonly Dictionary<string, FileSpecs> fileSpecs = new Dictionary<string, FileSpecs>();
		protected readonly FileSystemWrapper fileSystem;

		protected uint? adjustedBlockId;
		protected (long index, long startingBlockId, long endingBlockId) blockIndex;

		public ChannelFilesHandler(string folderPath, FileSystemWrapper fileSystem) {
			this.FolderPath = folderPath;
			this.fileSystem = fileSystem;
		}

		public string FolderPath { get; set; }

		public void ResetFileSpecs(uint adjustedBlockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			this.adjustedBlockId = adjustedBlockId;
			this.blockIndex = blockIndex;

			this.fileSpecs.Clear();

			this.ResetAllFileSpecs(adjustedBlockId, blockIndex);

			this.EnsureFilesCreated();
		}

		protected abstract void ResetAllFileSpecs(uint adjustedBlockId, (long index, long startingBlockId, long endingBlockId) blockIndex);

		public virtual void EnsureFilesCreated() {

			foreach(FileSpecs fileSpec in this.fileSpecs.Values) {
				fileSpec.EnsureFilesExist();
			}
		}

		protected virtual string GetBlocksIndexFolderPath(long index) {

			return Path.Combine(this.FolderPath, $"{index}");
		}
	}
}