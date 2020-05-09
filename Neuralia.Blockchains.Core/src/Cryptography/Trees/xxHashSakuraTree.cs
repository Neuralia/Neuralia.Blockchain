using Neuralia.Blockchains.Core.Cryptography.xxHash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	/// <summary>
	///     xxhash is great for 64 bit non cryptographic hashes
	/// </summary>
	public class xxHashSakuraTree : SakuraTree<IxxHash> {


		static xxHashSakuraTree() {
		}

		public xxHashSakuraTree(Enums.ThreadMode threadMode = Enums.ThreadMode.ThreeQuarter) : base(threadMode) {
		}
		

		protected override IxxHash DigestFactory() {
			return new xxHash64();
		}

		protected override SafeArrayHandle GenerateHash(SafeArrayHandle entry, IxxHash hasher) {
			return hasher.Hash(entry.Span);
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
	}
}