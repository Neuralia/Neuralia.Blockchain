using System;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Large;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split {
	public class Slice : IDisposable2 {

		public const int MAXIMUM_SIZE = LargeMessageHeader.MAXIMUM_SIZE;
		public IByteArray bytes;
		public long hash;
		public int index;
		public int length;
		public int startIndex;

		public Slice(int index, int length, long hash) {
			this.index = index;
			this.length = length;

			this.hash = hash;
		}

		public Slice(int index, int startIndex, int length, IByteArray bytes) {
			this.index = index;
			this.startIndex = startIndex;
			this.length = length;
			this.bytes = bytes;

			this.hash = ComputeSliceHash(bytes);
		}

		public bool IsLoaded => this.bytes != null;

		public static long ComputeSliceHash(IByteArray bytes) {

			return HashingUtils.XxHasher64.Hash(bytes);
		}

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				try {
					this.bytes?.Dispose();
				} finally {
					this.IsDisposed = true;
				}
			}
		}

		~Slice() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}