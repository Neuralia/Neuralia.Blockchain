using System;
using System.IO.Abstractions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils {
	public class BlockChannelUtils {

		[Flags]
		public enum BlockChannelTypes : ushort {
			None = 0,
			HighHeader = 1,
			LowHeader = 1 << 1,
			Contents = 1 << 2,
			LargeContents = 1 << 3,
			Erasables = 1 << 4,
			Slots = 1 << 5,
			Keys = 1 << 6,

			// combinations
			Headers = HighHeader | LowHeader,
			All = HighHeader | LowHeader | Contents | LargeContents | Erasables | Slots | Keys
		}

		private const string HIGH_BLOCK_BASE_FILE_NAME = "blocks.high";
		private const string LOW_BLOCK_BASE_FILE_NAME = "blocks.low";
		private const string CONTENTS_BASE_FILE_NAME = "contents";
		private const string LARGE_CONTENTS_BASE_FILE_NAME = "large-contents";
		private const string ERASABLES_BASE_FILE_NAME = "erasables";
		private const string SLOTS_BASE_FILE_NAME = "slots";
		private const string KEYS_BASE_FILE_NAME = "keys";

		public static readonly BlockChannelTypes[] AllIndividualTypes = {BlockChannelTypes.HighHeader, BlockChannelTypes.LowHeader, BlockChannelTypes.Contents, BlockChannelTypes.LargeContents, BlockChannelTypes.Erasables, BlockChannelTypes.Slots, BlockChannelTypes.Keys};
		public static readonly BlockChannelTypes[] MainIndexChannels = {BlockChannelTypes.HighHeader, BlockChannelTypes.LowHeader, BlockChannelTypes.Contents, BlockChannelTypes.LargeContents, BlockChannelTypes.Slots, BlockChannelTypes.Keys};

		public static Func<ChannelProvider> GetProviderFactory(BlockChannelTypes type, string folderPath, IFileSystem fileSystem) {
			switch(type) {
				case BlockChannelTypes.HighHeader:

					return () => new MainIndexedConcatenatedChannelProvider(HIGH_BLOCK_BASE_FILE_NAME, folderPath, fileSystem);

				case BlockChannelTypes.LowHeader:

					return () => new MainIndexedConcatenatedChannelProvider(LOW_BLOCK_BASE_FILE_NAME, folderPath, fileSystem);

				case BlockChannelTypes.Contents:

					return () => new MainIndexedConcatenatedChannelProvider(CONTENTS_BASE_FILE_NAME, folderPath, fileSystem);

				case BlockChannelTypes.LargeContents:

					return () => new MainIndexedConcatenatedChannelProvider(LARGE_CONTENTS_BASE_FILE_NAME, folderPath, fileSystem);

				case BlockChannelTypes.Erasables:

					return () => new ErasableFileChannelProvider(ERASABLES_BASE_FILE_NAME, folderPath, fileSystem);

				case BlockChannelTypes.Slots:

					return () => new MainIndexedConcatenatedChannelProvider(SLOTS_BASE_FILE_NAME, folderPath, fileSystem);
				case BlockChannelTypes.Keys:

					return () => new MainIndexedConcatenatedChannelProvider(KEYS_BASE_FILE_NAME, folderPath, fileSystem);
			}

			return null;
		}

		public static void RunForFlags(BlockChannelTypes channels, Action<BlockChannelTypes> action) {

			foreach(BlockChannelTypes type in AllIndividualTypes) {
				if(channels.HasFlag(type)) {
					action(type);
				}
			}
		}
	}
}