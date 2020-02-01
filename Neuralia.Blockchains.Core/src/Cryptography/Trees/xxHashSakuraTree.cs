using System;

using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Data.HashFunction.xxHash;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	/// <summary>
	///     xxhash is great for 64 bit non cryptographic hashes
	/// </summary>
	public class xxHashSakuraTree : SakuraTree<IxxHash> {
		
		
		private static readonly xxHashConfig XxHashConfig = new xxHashConfig {HashSizeInBits = 64, Seed = 4745286767196280399UL};

		protected xxHashConfig GetxxConfig() {
			return new xxHashConfig {HashSizeInBits = 64, Seed = 4745286767196280399UL};
		}
		static xxHashSakuraTree() {
		}
		
		protected override IxxHash DigestFactory() {
			return xxHashFactory.Instance.Create(XxHashConfig);
		}

		protected override SafeArrayHandle GenerateHash(SafeArrayHandle entry, IxxHash hasher) {
			return ByteArray.WrapAndOwn( hasher.ComputeHash(entry.Bytes, entry.Offset, entry.Length).Hash);
		}

		public ulong HashULong(IHashNodeList nodeList) {
			using(SafeArrayHandle hash = this.HashBytes(nodeList)) {

				TypeSerializer.Deserialize(hash.Span, out ulong result);
				
				return result;
			}
		}

		public long HashLong(IHashNodeList nodeList) {
			using(SafeArrayHandle hash = this.HashBytes(nodeList)) {

				TypeSerializer.Deserialize(hash.Span, out long result);
				
				return result;
			}
		}

		public xxHashSakuraTree(Enums.ThreadMode threadMode = Enums.ThreadMode.ThreeQuarter) : base(threadMode) {
		}
	}
}