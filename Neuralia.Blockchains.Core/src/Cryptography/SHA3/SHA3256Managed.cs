using Neuralia.Blockchains.Tools.Data.Allocation;

namespace Neuralia.Blockchains.Core.Cryptography.SHA3 {
	public class SHA3256Managed : SHA3Managed {
		public SHA3256Managed(FixedByteAllocator allocator) : base(256, allocator) {
		}
	}
}