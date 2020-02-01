using System;
using System.IO.Abstractions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils {
	public class FileSpecs {

		private readonly IFileSystem fileSystem;
		private bool? filesExists;
		private uint? fileSize;

		public FileSpecs(string filePath, IFileSystem fileSystem) {
			this.fileSystem = fileSystem;

			this.FilePath = filePath;
			this.fileSize = null;
			this.filesExists = null;
		}

		public string FilePath { get; set; }

		public uint FileSize {
			get {
				if(this.fileSize == null) {
					this.fileSize = (uint) this.GetFileSize(this.FilePath);
				}

				return this.fileSize.Value;
			}
		}

		public bool FileEmpty => this.FileSize == 0;

		public bool FileExists {
			get {
				if(!this.filesExists.HasValue) {
					this.filesExists = this.fileSystem.File.Exists(this.FilePath);
				}

				return this.filesExists.Value;
			}
		}

		public void EnsureFilesExist() {

			if(!this.FileExists) {
				// first, ensure the files exist
				FileExtensions.EnsureFileExists(this.FilePath, this.fileSystem);

				this.filesExists = true;
			}
		}

		private long GetFileSize(string filename) {

			this.EnsureFilesExist();

			return this.fileSystem.FileInfo.FromFileName(filename).Length;
		}

		public void ResetSizes() {
			this.fileSize = null;
		}

		public void Write(in Span<byte> data) {
			FileExtensions.OpenWrite(this.FilePath, data, this.fileSystem);
			this.ResetSizes();
		}

		public void Write(SafeArrayHandle data) {
			FileExtensions.OpenWrite(this.FilePath, data, this.fileSystem);
			this.ResetSizes();
		}

		public void Append(in Span<byte> data) {
			FileExtensions.OpenAppend(this.FilePath, data, this.fileSystem);
			this.ResetSizes();
		}

		public void Append(SafeArrayHandle data) {
			FileExtensions.OpenAppend(this.FilePath, data, this.fileSystem);
			this.ResetSizes();
		}

		public void Truncate(long length) {
			FileExtensions.Truncate(this.FilePath, length, this.fileSystem);
			this.ResetSizes();
		}

		public SafeArrayHandle ReadBytes(long offset, int dataLength) {
			return FileExtensions.ReadBytes(this.FilePath, offset, dataLength, this.fileSystem);
		}

		public SafeArrayHandle ReadAllBytes() {
			return FileExtensions.ReadAllBytes(this.FilePath, this.fileSystem);
		}

		public void Delete() {
			if(!this.FileExists) {
				this.fileSystem.File.Delete(this.FilePath);
				this.ResetSizes();
				this.filesExists = false;
			}
		}
	}
}