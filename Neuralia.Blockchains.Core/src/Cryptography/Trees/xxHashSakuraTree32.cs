using Neuralia.Blockchains.Core.Cryptography.xxHash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	/// <summary>
	///     xxhash is great for 64 bit non cryptographic hashes
	/// </summary>
	public class xxHashSakuraTree32 : SakuraTree<IxxHash> {


		static xxHashSakuraTree32() {

		}

		public xxHashSakuraTree32(Enums.ThreadMode threadMode = Enums.ThreadMode.ThreeQuarter) : base(threadMode) {
		}

		protected override IxxHash DigestFactory() {
			return new xxHash32();
		}

		protected override SafeArrayHandle GenerateHash(SafeArrayHandle entry, IxxHash hasher) {
			return (SafeArrayHandle)hasher.Hash(entry.Span);
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