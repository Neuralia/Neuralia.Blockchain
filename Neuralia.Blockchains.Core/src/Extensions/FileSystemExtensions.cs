using Neuralia.Blockchains.Core.Tools;
using Zio;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class FileSystemExtensions {

		public static string ToOsPath(this FileSystemEntry entry, FileSystemWrapper fileSystemWrapper) {

			if((fileSystemWrapper == null) || (entry == null)) {
				return "";
			}

			return fileSystemWrapper.GetRegularFileSystemPath(entry.Path);
		}

		public static long GetFileLength(this FileSystemEntry entry) {

			if(entry == null) {
				return 0;
			}

			return entry.FileSystem.GetFileLength(entry.Path);
		}
	}
}