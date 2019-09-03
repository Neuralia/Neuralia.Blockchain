using Neuralia.Blockchains.Tools.Data.Allocation;

namespace Neuralia.Blockchains.Core.Cryptography.SHA3 {
	public class SHA3512Managed : SHA3Managed {
		public SHA3512Managed(FixedByteAllocator allocator) : base(512, allocator) {
		}
	}
}