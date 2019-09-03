using System;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public abstract class MessageHash<T> : IMessageHash<T> {

		private readonly IHasher<T> hasher;
		private readonly object locker = new object();

		protected T hash;

		public MessageHash(IByteArray message = null) {

			this.hasher = this.CreateHasher();

			this.SetHash(message);
		}

		public abstract void WriteHash(IDataDehydrator dh);
		public abstract void ReadHash(IDataRehydrator dr);

		public void SetHash(IByteArray message) {

			if((message != null) && !message.IsEmpty) {
				this.hash = this.HashMessage(message);
			}
		}

		public virtual T HashMessage(IByteArray message) {
			if((message == null) || message.IsEmpty) {
				throw new ApplicationException("Cannot hash a null message");
			}

			lock(this.locker) {
				return this.hasher.Hash(message);
			}
		}

		public T Hash => this.hash;

		public bool CompareHash(IByteArray messasge) {
			// first, lets compare the hashes
			T hash = this.HashMessage(messasge);

			return this.hash.Equals(hash);
		}

		protected abstract IHasher<T> CreateHasher();
	}
}