using System;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {

	/// <summary>
	///     A special hash node list that will split a byte array into slices and expose them as nodes to hash
	/// </summary>
	public class BinarySliceHashNodeList : IHashNodeList {

		private readonly ByteArray content;
		private readonly int sizeSize = 64;

		public BinarySliceHashNodeList(ByteArray content, int sizeSize = 64) {
			this.content = content;
			this.sizeSize = sizeSize;
			this.Count = (int) Math.Ceiling((double) content.Length / this.sizeSize);

		}

		public IByteArray this[int i] {
			get {
				if(i >= this.Count) {
					throw new IndexOutOfRangeException();
				}

				int length = this.content.Length - (i * this.sizeSize);

				if(length > this.sizeSize) {
					length = this.sizeSize;
				}

				return this.content.SliceReference(i * this.sizeSize, length);
			}
		}

		public int Count { get; }
	}
}