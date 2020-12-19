using System;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Large;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Split {
	public class Slice : IDisposableExtended {

		public const int MAXIMUM_SIZE = LargeMessageHeader.MAXIMUM_SIZE;
		public readonly SafeArrayHandle bytes = SafeArrayHandle.Create();
		public long hash;
		public int index;
		public int length;
		public int startIndex;

		public Slice(int index, int length, long hash) {
			this.index = index;
			this.length = length;

			this.hash = hash;
		}

		public Slice(int index, int startIndex, int length, SafeArrayHandle bytes) {
			this.index = index;
			this.startIndex = startIndex;
			this.length = length;
			this.bytes = bytes;

			this.hash = ComputeSliceHash(bytes);
		}

		public bool IsLoaded => this.bytes != null;

		public static long ComputeSliceHash(SafeArrayHandle bytes) {

			return HashingUtils.XxHash64(bytes);
		}

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.bytes?.Dispose();
			}

			this.IsDisposed = true;
		}

		~Slice() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}