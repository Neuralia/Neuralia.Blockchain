using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools;
using Zio;
using Zio.FileSystems;

namespace Neuralia.Blockchains.Core.Tools {

	//TODO: the Zio wrapping (especially UPath conversion) could be optimized for speed.
	public class FileSystemWrapper : IDisposableExtended {

		// we keep a physical instance to translate paths
		private readonly PhysicalFileSystem physicalFileSystem;
		private readonly bool isWindows;
		
		public FileSystemWrapper(IFileSystem fileSystem) {
			this.FileSystem = fileSystem;

			if(this.FileSystem is PhysicalFileSystem physicalFileSystem) {
				this.physicalFileSystem = physicalFileSystem;
			} else {
				this.physicalFileSystem = new PhysicalFileSystem();
			}

			// windows has some particularities
			this.isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
		}

		public IFileSystem FileSystem { get; }

		public bool IsPhysical => this.FileSystem is PhysicalFileSystem;
		public bool IsMemory => this.FileSystem is MemoryFileSystem;

        /// <summary>
        ///     Run either sync of async
        /// </summary>
        /// <param name="sync"></param>
        /// <param name="async"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private Task<T> RunDual<T>(Func<T> sync, Func<Task<T>> async) {
			if(this.IsMemory) {
				return Task.FromResult(sync());
			}

			return async();
		}

		private Task RunDual(Action sync, Func<Task> async) {
			if(this.IsMemory) {
				sync();

				return Task.CompletedTask;
			}

			return async();
		}

		public static FileSystemWrapper CreatePhysical() {
			return new FileSystemWrapper(new PhysicalFileSystem());
		}

		public static FileSystemWrapper CreateMemory() {
			return new FileSystemWrapper(new MemoryFileSystem());
		}

        /// <summary>
        ///     Convert a regular path to an internally recognized path format for Zio
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileSystem"></param>
        /// <returns></returns>
        public string GetInternalFileSystemPath(string path) {
			return this.physicalFileSystem.ConvertPathFromInternal(path).FullName;
		}

        /// <summary>
        ///     Convert a Zio path to a regular filesystem path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileSystem"></param>
        /// <returns></returns>
        public string GetRegularFileSystemPath(UPath path) {
			return this.physicalFileSystem.ConvertPathToInternal(path);
		}

		private IEnumerable<string> ConvertPathsBack(IEnumerable<UPath> paths) {
			return paths.Select(this.GetRegularFileSystemPath);
		}

		/// <summary>
		/// windows seems to ahve an occasional issue of file locking. on windows, we reset the attributes
		/// </summary>
		/// <param name="path"></param>
		private void SetWindowsAttributes(string path) {
			
			if(this.isWindows) {
				// for other oses, we dont need to do this
				this.FileSystem.SetAttributes(path, FileAttributes.Normal);
			}
		}
		
		public void CreateDirectory(string path) {
			var adjustedPath = this.GetInternalFileSystemPath(path);
			this.FileSystem.CreateDirectory(adjustedPath);
			this.SetWindowsAttributes(adjustedPath);
		}

		public bool DirectoryExists(string path) {
			return this.FileSystem.DirectoryExists(this.GetInternalFileSystemPath(path));
		}

		public void MoveDirectory(string srcPath, string destPath) {
			var adjustedSrcPath = this.GetInternalFileSystemPath(srcPath);
			var adjustedDestPath = this.GetInternalFileSystemPath(destPath);
			
			this.SetWindowsAttributes(adjustedSrcPath);
			this.FileSystem.MoveDirectory(adjustedSrcPath, adjustedDestPath);
			this.SetWindowsAttributes(adjustedDestPath);
		}

		public void DeleteDirectory(string path, bool isRecursive) {
			var adjustedPath = this.GetInternalFileSystemPath(path);
			this.SetWindowsAttributes(adjustedPath);
			this.FileSystem.DeleteDirectory(adjustedPath, isRecursive);
		}

		public void CopyDirectory(string srcPath, string destPath, bool overwrite) {
			var adjustedSrcPath = this.GetInternalFileSystemPath(srcPath);
			var adjustedDestPath = this.GetInternalFileSystemPath(destPath);
			
			this.FileSystem.CopyDirectory(adjustedSrcPath, this.FileSystem, adjustedDestPath, overwrite);
			this.SetWindowsAttributes(adjustedDestPath);
		}
		
		public void CopyFile(string srcPath, string destPath, bool overwrite) {
			var adjustedSrcPath = this.GetInternalFileSystemPath(srcPath);
			var adjustedDestPath = this.GetInternalFileSystemPath(destPath);
			
			this.FileSystem.CopyFile(adjustedSrcPath, adjustedDestPath, overwrite);
			this.SetWindowsAttributes(adjustedDestPath);
		}

		public void ReplaceFile(string srcPath, string destPath, string destBackupPath, bool ignoreMetadataErrors) {
			this.FileSystem.ReplaceFile(this.GetInternalFileSystemPath(srcPath), this.GetInternalFileSystemPath(destPath), this.GetInternalFileSystemPath(destBackupPath), ignoreMetadataErrors);
		}

		public long GetFileLength(string path) {
			return this.FileSystem.GetFileLength(this.GetInternalFileSystemPath(path));
		}

		public bool FileExists(string path) {
			return this.FileSystem.FileExists(this.GetInternalFileSystemPath(path));
		}

		public void MoveFile(string srcPath, string destPath) {
			var adjustedSrcPath = this.GetInternalFileSystemPath(srcPath);
			var adjustedDestPath = this.GetInternalFileSystemPath(destPath);
			this.SetWindowsAttributes(adjustedSrcPath);
			this.FileSystem.MoveFile(adjustedSrcPath, adjustedDestPath);
			this.SetWindowsAttributes(adjustedDestPath);
		}

		public void DeleteFile(string path) {
			var adjustedPath = this.GetInternalFileSystemPath(path);
			this.SetWindowsAttributes(adjustedPath);
			this.FileSystem.DeleteFile(adjustedPath);
		}

		public Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share = FileShare.None) {
			return this.FileSystem.OpenFile(this.GetInternalFileSystemPath(path), mode, access, share);
		}

		public FileAttributes GetAttributes(string path) {
			return this.FileSystem.GetAttributes(this.GetInternalFileSystemPath(path));
		}

		public void SetAttributes(string path, FileAttributes attributes) {
			this.FileSystem.SetAttributes(this.GetInternalFileSystemPath(path), attributes);
		}

		public DateTime GetCreationTime(string path) {
			return this.FileSystem.GetCreationTime(this.GetInternalFileSystemPath(path));
		}

		public void SetCreationTime(string path, DateTime time) {
			this.FileSystem.SetCreationTime(this.GetInternalFileSystemPath(path), time);
		}

		public DateTime GetLastAccessTime(string path) {
			return this.FileSystem.GetLastAccessTime(this.GetInternalFileSystemPath(path));
		}

		public void SetLastAccessTime(string path, DateTime time) {
			this.FileSystem.SetLastAccessTime(this.GetInternalFileSystemPath(path), time);
		}

		public DateTime GetLastWriteTime(string path) {
			return this.FileSystem.GetLastWriteTime(this.GetInternalFileSystemPath(path));
		}

		public void SetLastWriteTime(string path, DateTime time) {
			this.FileSystem.SetLastWriteTime(this.GetInternalFileSystemPath(path), time);
		}

		public IEnumerable<string> EnumeratePaths(string path, string searchPattern, SearchOption searchOption, SearchTarget searchTarget) {
			return this.ConvertPathsBack(this.FileSystem.EnumeratePaths(this.GetInternalFileSystemPath(path), searchPattern, searchOption, searchTarget));
		}

		public byte[] ReadAllBytes(string path) {
			return this.FileSystem.ReadAllBytes(this.GetInternalFileSystemPath(path));
		}

		public Task<byte[]> ReadAllBytesAsync(string path) {
			return this.RunDual(() => this.ReadAllBytes(path), () => File.ReadAllBytesAsync(path));
		}

		public string ReadAllText(string path) {
			return this.FileSystem.ReadAllText(this.GetInternalFileSystemPath(path));
		}

		public Task<string> ReadAllTextAsync(string path) {
			return this.RunDual(() => this.ReadAllText(path), () => File.ReadAllTextAsync(path));
		}

		public string ReadAllText(string path, Encoding encoding) {
			return this.FileSystem.ReadAllText(this.GetInternalFileSystemPath(path), encoding);
		}

		public void WriteAllBytes(string path, byte[] content) {
			this.FileSystem.WriteAllBytes(this.GetInternalFileSystemPath(path), content);
		}

		public Task WriteAllBytesAsync(string path, byte[] content) {
			return this.RunDual(() => this.WriteAllBytes(path, content), () => File.WriteAllBytesAsync(path, content));
		}

		public string[] ReadAllLines(string path) {
			return this.FileSystem.ReadAllLines(this.GetInternalFileSystemPath(path));
		}

		public Task<string[]> ReadAllLinesAsync(string path) {
			return this.RunDual(() => this.ReadAllLines(path), () => File.ReadAllLinesAsync(path));
		}

		public string[] ReadAllLines(string path, Encoding encoding) {
			return this.FileSystem.ReadAllLines(this.GetInternalFileSystemPath(path), encoding);
		}

		public void WriteAllText(string path, string content) {
			this.FileSystem.WriteAllText(this.GetInternalFileSystemPath(path), content);
		}

		public Task WriteAllTextAsync(string path, string content) {
			return this.RunDual(() => this.WriteAllText(path, content), () => File.WriteAllTextAsync(path, content));
		}

		public void WriteAllText(string path, string content, Encoding encoding) {
			this.FileSystem.WriteAllText(this.GetInternalFileSystemPath(path), content, encoding);
		}

		public void AppendAllText(string path, string content) {
			this.FileSystem.AppendAllText(this.GetInternalFileSystemPath(path), content);
		}

		public Task AppendAllTextAsync(string path, string content) {
			return this.RunDual(() => this.AppendAllText(path, content), () => File.AppendAllTextAsync(path, content));
		}

		public void AppendAllText(string path, string content, Encoding encoding) {
			this.FileSystem.AppendAllText(this.GetInternalFileSystemPath(path), content, encoding);
		}

		public Stream CreateFile(string path) {
			return this.FileSystem.CreateFile(this.GetInternalFileSystemPath(path));
		}

		public void CreateEmptyFile(string path) {
			using(this.CreateFile(path)) {
				// nothing to do
			}
		}
		
		public IEnumerable<string> EnumerateDirectories(string path) {
			return this.ConvertPathsBack(this.FileSystem.EnumerateDirectories(this.GetInternalFileSystemPath(path)));
		}

		public IEnumerable<string> EnumerateDirectories(string path, string searchPattern) {
			return this.ConvertPathsBack(this.FileSystem.EnumerateDirectories(this.GetInternalFileSystemPath(path), searchPattern));
		}

		public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) {
			return this.ConvertPathsBack(this.FileSystem.EnumerateDirectories(this.GetInternalFileSystemPath(path), searchPattern, searchOption));
		}

		public IEnumerable<string> EnumerateFiles(string path) {
			return this.ConvertPathsBack(this.FileSystem.EnumerateFiles(this.GetInternalFileSystemPath(path)));
		}

		public IEnumerable<string> EnumerateFiles(string path, string searchPattern) {
			return this.ConvertPathsBack(this.FileSystem.EnumerateFiles(this.GetInternalFileSystemPath(path), searchPattern));
		}

		public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) {
			return this.ConvertPathsBack(this.FileSystem.EnumerateFiles(this.GetInternalFileSystemPath(path), searchPattern, searchOption));
		}

		public IEnumerable<string> EnumeratePaths(string path) {
			return this.ConvertPathsBack(this.FileSystem.EnumeratePaths(this.GetInternalFileSystemPath(path)));
		}

		public IEnumerable<string> EnumeratePaths(string path, string searchPattern) {
			return this.ConvertPathsBack(this.FileSystem.EnumeratePaths(this.GetInternalFileSystemPath(path), searchPattern));
		}

		public IEnumerable<string> EnumeratePaths(string path, string searchPattern, SearchOption searchOption) {
			return this.ConvertPathsBack(this.FileSystem.EnumeratePaths(this.GetInternalFileSystemPath(path), searchPattern, searchOption));
		}

		public IEnumerable<FileEntry> EnumerateFileEntries(string path) {
			return this.FileSystem.EnumerateFileEntries(this.GetInternalFileSystemPath(path));
		}

		public IEnumerable<FileEntry> EnumerateFileEntries(string path, string searchPattern) {
			return this.FileSystem.EnumerateFileEntries(this.GetInternalFileSystemPath(path), searchPattern);
		}

		public IEnumerable<FileEntry> EnumerateFileEntries(string path, string searchPattern, SearchOption searchOption) {
			return this.FileSystem.EnumerateFileEntries(this.GetInternalFileSystemPath(path), searchPattern, searchOption);
		}

		public IEnumerable<DirectoryEntry> EnumerateDirectoryEntries(string path) {
			return this.FileSystem.EnumerateDirectoryEntries(this.GetInternalFileSystemPath(path));
		}

		public IEnumerable<DirectoryEntry> EnumerateDirectoryEntries(string path, string searchPattern) {
			return this.FileSystem.EnumerateDirectoryEntries(this.GetInternalFileSystemPath(path), searchPattern);
		}

		public IEnumerable<DirectoryEntry> EnumerateDirectoryEntries(string path, string searchPattern, SearchOption searchOption) {
			return this.FileSystem.EnumerateDirectoryEntries(this.GetInternalFileSystemPath(path), searchPattern, searchOption);
		}

		public IEnumerable<FileSystemEntry> EnumerateFileSystemEntries(string path) {
			return this.FileSystem.EnumerateFileSystemEntries(this.GetInternalFileSystemPath(path));
		}

		public IEnumerable<FileSystemEntry> EnumerateFileSystemEntries(string path, string searchPattern) {
			return this.FileSystem.EnumerateFileSystemEntries(this.GetInternalFileSystemPath(path), searchPattern);
		}

		public IEnumerable<FileSystemEntry> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption, SearchTarget searchTarget = SearchTarget.Both) {
			return this.FileSystem.EnumerateFileSystemEntries(this.GetInternalFileSystemPath(path), searchPattern, searchOption, searchTarget);
		}

		public FileSystemEntry GetFileSystemEntry(string path) {
			return this.FileSystem.GetFileSystemEntry(this.GetInternalFileSystemPath(path));
		}

		public FileEntry GetFileEntry(string filePath) {
			return this.FileSystem.GetFileEntry(this.GetInternalFileSystemPath(filePath));
		}

		public DirectoryEntry GetDirectoryEntry(string directoryPath) {
			return this.FileSystem.GetDirectoryEntry(this.GetInternalFileSystemPath(directoryPath));
		}

		public FileSystemEntry TryGetFileSystemEntry(string path) {
			return this.FileSystem.TryGetFileSystemEntry(this.GetInternalFileSystemPath(path));
		}

        /// <summary>
        ///     return all files in the filesystem
        /// </summary>
        /// <param name="fileSystem"></param>
        /// <param name="path"></param>
        /// <param name="searchPattern"></param>
        /// <param name="searchOption"></param>
        /// <returns></returns>
        public IEnumerable<string> AllFiles() {
			return this.ConvertPathsBack(this.FileSystem.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories));
		}

        /// <summary>
        ///     Return a file entry even if it does not exist on disk
        /// </summary>
        /// <param name="fileSystem"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public FileEntry GetFileEntryUnconditional(string filePath) {

			return new FileEntry(this.FileSystem, this.GetInternalFileSystemPath(filePath));
		}

        /// <summary>
        ///     Return a directory entry even if it does not exist on disk
        /// </summary>
        /// <param name="fileSystem"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public DirectoryEntry GetDirectoryEntryUnconditional(string directoryPath) {
			return new DirectoryEntry(this.FileSystem, this.GetInternalFileSystemPath(directoryPath));
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {
				this.FileSystem?.Dispose();
				this.physicalFileSystem?.Dispose();
			}

			this.IsDisposed = true;
		}

		~FileSystemWrapper() {
			this.Dispose(false);
		}

	#endregion

	}
}