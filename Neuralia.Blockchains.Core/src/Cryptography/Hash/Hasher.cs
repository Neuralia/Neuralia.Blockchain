using System;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Hash {

	public abstract class Hasher : IHasher<SafeArrayHandle> {
		protected readonly IHashDigest digest;

		public Hasher(IHashDigest digest) {
			this.digest = digest;
		}

		public virtual SafeArrayHandle Hash(SafeArrayHandle message) {
			SafeArrayHandle retValue = SafeArrayHandle.Create(this.digest.GetDigestSize());
			this.digest.BlockUpdate(message.Bytes, message.Offset, message.Length);
			this.digest.DoFinal(retValue.Bytes, retValue.Offset);

			return (SafeArrayHandle)retValue;
		}

		public SafeArrayHandle Hash(byte[] message) {

			using(SafeArrayHandle buffer = SafeArrayHandle.Create(message.Length)) {

				buffer.Entry.CopyFrom(message.AsSpan());

				return this.Hash(buffer);
			}
		}

		public SafeArrayHandle HashTwo(SafeArrayHandle message1, SafeArrayHandle message2) {
			int len1 = 0;

			if(message1 != null) {
				len1 = message1.Length;
			}

			int len2 = 0;

			if(message2 != null) {
				len2 = message2.Length;
			}

			using(SafeArrayHandle buffer = SafeArrayHandle.Create(len1 + len2)) {

				if(message1 != null) {
					message1.CopyTo(buffer.Entry);
				}

				if(message2 != null) {
					message2.CopyTo(buffer.Entry, len1);
				}

				// do the hash
				return this.Hash(buffer);
			}
		}

		public SafeArrayHandle HashTwo(SafeArrayHandle message1, short message2) {
			return this.HashTwo(message1, TypeSerializer.Serialize(message2));
		}

		public SafeArrayHandle HashTwo(SafeArrayHandle message1, int message2) {
			return this.HashTwo(message1, TypeSerializer.Serialize(message2));
		}

		public SafeArrayHandle HashTwo(SafeArrayHandle message1, long message2) {
			return this.HashTwo(message1, TypeSerializer.Serialize(message2));
		}

		public SafeArrayHandle HashTwo(short message1, short message2) {
			return this.HashTwo(TypeSerializer.Serialize(message1), TypeSerializer.Serialize(message2));
		}

		public SafeArrayHandle HashTwo(ushort message1, ushort message2) {
			return this.HashTwo(TypeSerializer.Serialize(message1), TypeSerializer.Serialize(message2));
		}

		public SafeArrayHandle HashTwo(ushort message1, long message2) {
			return this.HashTwo(TypeSerializer.Serialize(message1), TypeSerializer.Serialize(message2));
		}

		public SafeArrayHandle HashTwo(int message1, int message2) {
			return this.HashTwo(TypeSerializer.Serialize(message1), TypeSerializer.Serialize(message2));
		}

		public SafeArrayHandle HashTwo(uint message1, uint message2) {
			return this.HashTwo(TypeSerializer.Serialize(message1), TypeSerializer.Serialize(message2));
		}

		public SafeArrayHandle HashTwo(long message1, long message2) {
			return this.HashTwo(TypeSerializer.Serialize(message1), TypeSerializer.Serialize(message2));
		}

		public SafeArrayHandle HashTwo(ulong message1, ulong message2) {
			return this.HashTwo(TypeSerializer.Serialize(message1), TypeSerializer.Serialize(message2));
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public bool IsDisposed { get; private set; }

		public SafeArrayHandle HashTwo(SafeArrayHandle message1, ulong message2) {
			return this.HashTwo(message1, (SafeArrayHandle)TypeSerializer.Serialize(message2));
		}

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {
				if(this.digest is IDisposable disposable) {
					disposable.Dispose();
				}
			}

			this.IsDisposed = true;
		}
	}
}