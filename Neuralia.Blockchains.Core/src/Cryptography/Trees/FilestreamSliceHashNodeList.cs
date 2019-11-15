using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {

	/// <summary>
	///     A special hash node list that will split a file into slices and expose them as nodes to hash
	/// </summary>
	public class FileStreamSliceHashNodeList : IHashNodeList, IDisposableExtended {
		private readonly byte[] buffer;

		private readonly string filename;
		private readonly long fileSize;
		private readonly IFileSystem fileSystem;
		private readonly int sizeSize = 64;

		private Stream fileStream;
		
		public FileStreamSliceHashNodeList(string filename, IFileSystem fileSystem, int sizeSize = 64) {
			this.filename = filename;
			this.fileSystem = fileSystem;
			this.sizeSize = sizeSize;
			this.fileSize = fileSystem.FileInfo.FromFileName(filename).Length;
			this.Count = (int) Math.Ceiling((double) this.fileSize / this.sizeSize);
			this.buffer = new byte[this.sizeSize];
		}

		public FileStreamSliceHashNodeList(string filename, long fileSize, IFileSystem fileSystem, int sizeSize = 64) {
			this.filename = filename;
			this.fileSystem = fileSystem;
			this.sizeSize = sizeSize;
			this.fileSize = fileSize;
			this.Count = (int) Math.Ceiling((double) this.fileSize / this.sizeSize);
			this.buffer = new byte[this.sizeSize];
		}

		public SafeArrayHandle this[int i] {
			get {

				if(this.fileStream == null) {

					this.fileStream = this.fileSystem.FileStream.Create(this.filename, FileMode.Open, FileAccess.Read);
				}

				if(i >= this.Count) {
					throw new IndexOutOfRangeException();
				}

				int length = (int) (this.fileSize - (i * this.sizeSize));

				if(length > this.sizeSize) {
					length = this.sizeSize;
				}

				var localBuffer = this.buffer;

				if(length < localBuffer.Length) {
					localBuffer = new byte[length];
				}

				this.fileStream.Seek(i * this.sizeSize, SeekOrigin.Begin);

				this.fileStream.Read(localBuffer, 0, length);

				var result = ByteArray.Create(localBuffer.Length);
				result.CopyFrom(localBuffer.AsSpan());
				
				return result;
			}
		}

		public int Count { get; }

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			
			if(disposing && !this.IsDisposed) {
				try {
					this.fileStream?.Dispose();
					this.fileStream = null;
				} catch(Exception ex) {

				}
			}

			this.IsDisposed = true;
		}

		~FileStreamSliceHashNodeList() {
			this.Dispose(false);
		}

	#endregion
	}
}