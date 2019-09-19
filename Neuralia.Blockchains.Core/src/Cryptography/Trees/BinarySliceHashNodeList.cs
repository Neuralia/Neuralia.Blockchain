using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {

	/// <summary>
	///     A special hash node list that will split a byte array into slices and expose them as nodes to hash
	/// </summary>
	public class BinarySliceHashNodeList : IHashNodeList {

		private readonly SafeArrayHandle content = SafeArrayHandle.Create();
		private readonly int sizeSize = 64;

		
		public BinarySliceHashNodeList(SafeArrayHandle content, int sizeSize = 64) {
			this.content.Entry = content.Entry;
			this.sizeSize = sizeSize;
			this.Count = (int) Math.Ceiling((double) content.Length / this.sizeSize);

		}

		public SafeArrayHandle this[int i] {
			get {
				if(i >= this.Count) {
					throw new IndexOutOfRangeException();
				}

				int length = this.content.Length - (i * this.sizeSize);

				if(length > this.sizeSize) {
					length = this.sizeSize;
				}

				return this.content.Entry.SliceReference(i * this.sizeSize, length);
			}
		}

		public int Count { get; }
	}
}