using System;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Tiny {
	/// <summary>
	///     This is a tiny and fast message that reduces header size to a minimum and does not hash the contents. this is the
	///     bare minimum.
	/// </summary>
	public class TinyMessageHeader : MessageHeader {

		public const byte MAXIMUM_SIZE = byte.MaxValue;

		public TinyMessageHeader() {

		}

		public TinyMessageHeader(int messageLength, IByteArray message) : base(messageLength, message) {
		}

		protected override int MaxiumMessageSize => MAXIMUM_SIZE;

		protected override void Initialize() {
			this.MessageType = MessageTypes.Tiny;
		}

		protected override void DehydrateComponents(IDataDehydrator dh) {

		}

		protected override void RehydrateComponents(IDataRehydrator rh) {

		}

		protected override IMessageHash CreateHash(IByteArray message = null) {
			return new MessageHashNull();
		}

		protected override void DehydrateHeader(IDataDehydrator dh) {
			// in this case, we serialize to a minimal size
			this.DehydrateComponents(dh);
		}

		public override int GetMaximumHeaderSize() {
			return PREFIX_SIZE; // yup, its a tiny heder ;)
		}

		protected override void RehydrateHeader(IDataRehydrator rh) {

			int startOffset = rh.Offset;

			this.RehydrateComponents(rh);

			// lets check again, to ensure that we really did read less than maximum size
			if(rh.Offset != this.GetMaximumHeaderSize()) {
				throw new ApplicationException("We read more data than was advertised in the header length. Corrupt data.");
			}
		}
	}
}