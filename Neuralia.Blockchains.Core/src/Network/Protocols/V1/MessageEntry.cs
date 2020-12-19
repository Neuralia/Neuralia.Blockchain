using System;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols.V1 {
	public abstract class MessageEntry<HEADER_TYPE> : IMessageEntry<HEADER_TYPE>
		where HEADER_TYPE : class, IMessageHeader {

		private readonly SafeArrayHandle message;

		public MessageEntry(SafeArrayHandle message = null) {

			if((message != null) && message.HasData) {
				this.message = message.Branch();
				this.HeaderT = this.CreateHeader(message.Length, message);
			} else {
				this.message = SafeArrayHandle.Create();
				this.HeaderT = this.CreateHeader();
			}
		}

		protected virtual bool AllowEmptyMessage => false;

		public virtual int MessageLength {
			get => this.HeaderT.MessageLength;
			set => this.HeaderT.MessageLength = value;
		}

		public virtual SafeArrayHandle Dehydrate() {
			if(!this.IsComplete) {
				throw new ApplicationException("Impossible to dehydrate a message which is not completed yet. The binary message has not been set.");
			}

			using IDataDehydrator dh = DataSerializationFactory.CreateDehydrator();

			this.DehydrateHeader(dh);

			// add our actual message
			this.WriteMessage(dh);

			return dh.ToArray();
		}

		public IMessageHeader Header => this.HeaderT;

		public abstract void RebuildHeader(SafeArrayHandle buffer);

		public HEADER_TYPE HeaderT { get; protected set; }
		public virtual bool IsComplete => this.Message != null;
		public byte Version => this.HeaderT.Version;

		public virtual SafeArrayHandle Message => this.message;

		public void SetMessageContent(IDataRehydrator bufferRehydrator) {

			// The data is wrapped in metadata. we need to unwrap it.
			using(SafeArrayHandle unWrappedMessage = (SafeArrayHandle)bufferRehydrator.ReadArrayToEnd()) {

				// throw an exception to avoid bugs
				if((unWrappedMessage == null) || unWrappedMessage.IsEmpty) {
					if(!this.AllowEmptyMessage) {
						throw new ApplicationException("Empty or null messages are not allowed for the protocol message type");
					}

					this.ClearMessage();

					// a 0 length message, its like nothing, so we ignore it
					return;
				}

				this.ValidateMessageHash(unWrappedMessage);

				this.message.Entry = unWrappedMessage.Entry;
			}
		}

		protected virtual void WriteMessage(IDataDehydrator dh) {
			dh.WriteRawArray(this.Message);
		}

		protected void DehydrateHeader(IDataDehydrator dh) {
			this.Header.Dehydrate(dh);

			if(dh.Length > this.HeaderT.GetMaximumHeaderSize()) {
				throw new ApplicationException("Dehydrated header size is larger than maximum permissible value.");
			}
		}

		protected void ClearMessage() {
			this.message.Entry = null;
		}

		protected abstract HEADER_TYPE CreateHeader();
		protected abstract HEADER_TYPE CreateHeader(int messageLength, SafeArrayHandle message);

		protected virtual void ValidateMessageHash(SafeArrayHandle message) {
			// first, lets compare the hashes
			if(!this.HeaderT.Hash.CompareHash(message)) {
				throw new ApplicationException("The expected hash of the message from the header is different than the actual content");
			}
		}
		
	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		protected virtual void DisposeAll() {
			this.Message?.Dispose();
		}

		~MessageEntry() {
			this.Dispose(false);
		}

	#endregion
	}
}