using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public class MessageHashNull : MessageHash<byte> {

		public MessageHashNull(SafeArrayHandle message = null) {

		}

		public override byte HashMessage(SafeArrayHandle message) {
			return 0;
		}

		protected override IHasher<byte> CreateHasher() {
			return null;
		}

		public override void WriteHash(IDataDehydrator dh) {
			// do nothing
		}

		public override void ReadHash(IDataRehydrator dr) {
			// do nothing
		}
	}
}