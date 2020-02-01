using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

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
		private readonly object locker = new object();
		
		public FileStreamSliceHashNodeList(string filename, IFileSystem fileSystem, int sizeSize = 64): this(filename, fileSystem.FileInfo.FromFileName(filename).Length, fileSystem, sizeSize){

		}

		public FileStreamSliceHashNodeList(string filename, long fileSize, IFileSystem fileSystem, int sizeSize = 64) {
			this.filename = filename;
			this.fileSystem = fileSystem;
			this.sizeSize = sizeSize;
			this.fileSize = fileSize;
			this.Count = (int) Math.Ceiling((double) this.fileSize / this.sizeSize)+1;
			this.buffer = new byte[this.sizeSize];
		}

		public SafeArrayHandle this[int i] {
			get {

				lock(this.locker) {
					if(this.fileStream == null) {

						this.fileStream = this.fileSystem.FileStream.Create(this.filename, FileMode.Open, FileAccess.Read);
					}
				}

				if(i >= this.Count) {
					throw new IndexOutOfRangeException();
				}

				if(i == 0) {
					// return the file size
					ByteArray size = ByteArray.Create(sizeof(long));
					TypeSerializer.Serialize(this.sizeSize, size.Span);

					return size;
				}
				
				int length = (int) (this.fileSize - ((i-1) * this.sizeSize));

				if(length > this.sizeSize) {
					length = this.sizeSize;
				}

				var localBuffer = this.buffer;

				if(length < localBuffer.Length) {
					localBuffer = new byte[length];
				}

				lock(this.locker) {
					this.fileStream.Seek((i - 1) * this.sizeSize, SeekOrigin.Begin);

					this.fileStream.Read(localBuffer, 0, length);
				}

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