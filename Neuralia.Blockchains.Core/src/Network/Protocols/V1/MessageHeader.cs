using System;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1 {
	public abstract class MessageHeader : IMessageHeader {

		public enum MessageTypes : byte {
			Tiny = 1,
			Small = 2,
			Medium = 3,
			Large = 4,
			Split = 5,
			SplitSliceRequest = 6,
			SplitSliceResponse = 7
		}

		public const ushort MAXIMUM_HEADER_SIZE = 100;
		public const byte PREFIX_SIZE = 2; // the protocol version and then the message type bytes.
		public const byte PREFIX_HEADER_LENGTH_SIZE = sizeof(ushort); // the protocol version and then the message type bytes.

		protected int messageLength;

		public MessageHeader() {
			this.Version = MessageBuilder.ProtocolVersion.Version;

			this.Initialize();

			if(this.MessageType == 0) {
				throw new ApplicationException("The message type must be set");
			}

			this.SetCreateHash();
		}

		public MessageHeader(int messageLength, SafeArrayHandle message) : this() {

			this.CheckMessageSize(messageLength);

			this.SetCreateHash(message);
		}

		public MessageHeader(int messageLength, IMessageHash hash) : this() {

			this.CheckMessageSize(messageLength);

			this.messageLength = messageLength;

			this.Hash = hash;
		}

		public MessageTypes MessageType { get; protected set; }

		protected abstract int MaxiumMessageSize { get; }

		public byte Version { get; }

		public IMessageHash Hash { get; private set; }

		public void Dehydrate(IDataDehydrator dh) {

			// always start with the protocol version
			dh.Write(this.Version);

			// the first byte should always be the message type
			dh.Write((byte) this.MessageType);

			this.DehydrateHeader(dh);
		}

		public int MessageLength {
			get => this.messageLength;
			set => this.messageLength = value;
		}

		public int MessageOffset { get; private set; }

		public void Rehydrate(SafeArrayHandle data) {

			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data)) {

				this.PerformInitialRehydration(rehydrator);
			}
		}

		public abstract int GetMaximumHeaderSize();

		protected abstract IMessageHash CreateHash(SafeArrayHandle message = null);

		private void SetCreateHash(SafeArrayHandle message = null) {
			if(this.Hash == null) {
				this.Hash = this.CreateHash();
			}

			this.Hash.SetHash(message);
		}

		protected virtual void DehydrateHeader(IDataDehydrator dh) {
			// ensure we write the size of this header
			dh.WriteWrappedContent(dehydrator => {

				this.Hash.WriteHash(dehydrator);

				this.DehydrateComponents(dehydrator);
			});
		}

		private void PerformInitialRehydration(IDataRehydrator rh) {
			// skip the protocol version
			rh.Forward(1);

			MessageTypes messageType = (MessageTypes) rh.ReadByte();

			if(messageType != this.MessageType) {
				throw new ApplicationException("Message type is invalid.");
			}

			this.RehydrateHeader(rh);

			// useless i know, but i leave it there in case the max size was to change
			this.CheckMessageSize();

			this.MessageOffset = rh.Offset;
		}

		protected virtual void RehydrateHeader(IDataRehydrator rh) {
			rh.SnapshotPosition();
			(int headerSize, int _, int sizeByteSize) = rh.ReadSectionSize();
			rh.Rewind2Snapshot();

			if(headerSize > MAXIMUM_HEADER_SIZE) {
				throw new ApplicationException("Header size is too big.");
			}

			// now we know what to expect, lets set the expected max size + prefix size, plus size of message content
			rh.UpdateMaximumReadSize(headerSize + PREFIX_SIZE + sizeByteSize);

			int startOffset = rh.Offset;

			rh.SkipSectionSize();

			this.Hash.ReadHash(rh);

			this.RehydrateComponents(rh);

			// lets check again, to ensure that we really did read less than maximum size
			if(rh.Offset != (startOffset + headerSize + sizeByteSize)) {
				throw new ApplicationException("We read more data than was advertised in the header length. Corrupt data.");
			}

			if(rh.Offset > this.GetMaximumHeaderSize()) {
				throw new ApplicationException("We read more data than was advertised in the header length. Corrupt data.");
			}
		}

		protected abstract void Initialize();

		protected abstract void DehydrateComponents(IDataDehydrator dh);
		protected abstract void RehydrateComponents(IDataRehydrator rh);

		protected virtual void CheckMessageSize() {
			if(this.MessageLength > this.MaxiumMessageSize) {
				throw new MessageTooLargeException($"Message size of type {this.MessageType.ToString()} cannot exceed {this.MaxiumMessageSize} bytes.");
			}
		}

		protected virtual void CheckMessageSize(int size) {
			if(size > this.MaxiumMessageSize) {
				throw new MessageTooLargeException($"Message size of type {this.MessageType.ToString()} cannot exceed {this.MaxiumMessageSize} bytes.");
			}
		}
	}
}