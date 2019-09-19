using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public class MessageHash64 : MessageHash<long> {

		public MessageHash64(SafeArrayHandle message = null) : base(message) {

		}

		protected override IHasher<long> CreateHasher() {
			return new xxHasher64();
		}

		public override void WriteHash(IDataDehydrator dh) {
			dh.Write(this.hash);
		}

		public override void ReadHash(IDataRehydrator dr) {
			this.hash = dr.ReadLong();
		}
	}
}