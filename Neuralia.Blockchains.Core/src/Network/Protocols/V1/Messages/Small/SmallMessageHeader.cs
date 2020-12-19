using System;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1.Messages.Small {
	/// <summary>
	///     A somewhat small message that tried to reduce everything to a minimum, while still hashing the contents
	/// </summary>
	public class SmallMessageHeader : MessageHeader {

		public const ushort MAXIMUM_SIZE = 5000;

		public SmallMessageHeader() {

		}

		public SmallMessageHeader(int messageLength, SafeArrayHandle message) : base(messageLength, message) {
		}

		protected override int MaxiumMessageSize => MAXIMUM_SIZE;

		protected override void Initialize() {
			this.MessageType = MessageTypes.Small;
		}

		protected override void DehydrateComponents(IDataDehydrator dh) {

		}

		protected override void RehydrateComponents(IDataRehydrator rh) {

		}

		protected override IMessageHash CreateHash(SafeArrayHandle message = null) {
			return new MessageHashNull();
		}

		protected override void DehydrateHeader(IDataDehydrator dh) {
			// in this case, we serialize to a minimal size
			this.DehydrateComponents(dh);
		}

		public override int GetMaximumHeaderSize() {
			return PREFIX_SIZE;
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