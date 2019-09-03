using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public class MessageHash32 : MessageHash<int> {

		public MessageHash32(IByteArray message = null) : base(message) {

		}

		protected override IHasher<int> CreateHasher() {
			return new xxHasher32();
		}

		public override void WriteHash(IDataDehydrator dh) {
			dh.Write(this.hash);
		}

		public override void ReadHash(IDataRehydrator dr) {
			this.hash = dr.ReadInt();
		}
	}
}