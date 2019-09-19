using System;

using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Data.HashFunction.xxHash;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	/// <summary>
	///     xxhash is great for 64 bit non cryptographic hashes
	/// </summary>
	public class xxHashSakuraTree32 : SakuraTree {
		private static readonly IxxHash hasher;

		static xxHashSakuraTree32() {
			xxHashConfig XxHashConfig = new xxHashConfig {HashSizeInBits = 32, Seed = 4745282367123280399UL};

			hasher = xxHashFactory.Instance.Create(XxHashConfig);
		}

		protected override SafeArrayHandle GenerateHash(SafeArrayHandle entry) {
			return (ByteArray) hasher.ComputeHash(entry.Bytes, entry.Offset, entry.Length).Hash;
		}

		public uint HashUInt(IHashNodeList nodeList) {
			using(SafeArrayHandle hash = this.HashBytes(nodeList)) {

				TypeSerializer.Deserialize(hash.Span, out uint result);
				
				return result;
			}
		}

		public int HashInt(IHashNodeList nodeList) {
			using(SafeArrayHandle hash = this.HashBytes(nodeList)) {

				TypeSerializer.Deserialize(hash.Span, out int result);
				
				return result;
			}
		}
	}
}