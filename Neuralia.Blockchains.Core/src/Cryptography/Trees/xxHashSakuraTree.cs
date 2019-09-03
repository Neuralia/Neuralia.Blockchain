using System;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.System.Data.HashFunction.xxHash;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	/// <summary>
	///     xxhash is great for 64 bit non cryptographic hashes
	/// </summary>
	public class xxHashSakuraTree : SakuraTree {
		private static readonly IxxHash hasher;

		static xxHashSakuraTree() {
			xxHashConfig XxHashConfig = new xxHashConfig {HashSizeInBits = 64, Seed = 4745261967123280399UL};

			hasher = xxHashFactory.Instance.Create(XxHashConfig);
		}

		protected override IByteArray GenerateHash(IByteArray entry) {
			return (ByteArray) hasher.ComputeHash(entry.Bytes, entry.Offset, entry.Length).Hash;
		}

		public ulong HashULong(IHashNodeList nodeList) {
			IByteArray hash = this.HashBytes(nodeList);

			return BitConverter.ToUInt64(hash.Bytes, hash.Offset);
		}

		public long HashLong(IHashNodeList nodeList) {
			IByteArray hash = this.HashBytes(nodeList);

			long result = BitConverter.ToInt64(hash.Bytes, hash.Offset);

			hash.Return();

			return result;
		}
	}
}