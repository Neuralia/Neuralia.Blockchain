using System;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;
using Org.BouncyCastle.Crypto;

namespace Neuralia.Blockchains.Core.Cryptography.Hash {

	public abstract class Hasher : IHasher<IByteArray> {
		protected readonly IDigest digest;

		public Hasher(IDigest digest) {
			this.digest = digest;
		}

		public virtual IByteArray Hash(IByteArray message) {
			IByteArray retValue = new ByteArray(this.digest.GetDigestSize());
			this.digest.BlockUpdate(message.Bytes, message.Offset, message.Length);
			this.digest.DoFinal(retValue.Bytes, retValue.Offset);

			return retValue;
		}

		public IByteArray Hash(byte[] message) {

			IByteArray buffer = MemoryAllocators.Instance.allocator.Take(message.Length);

			buffer.CopyFrom(message);

			IByteArray result = this.Hash(buffer);

			buffer.Return();

			return result;
		}

		public IByteArray HashTwo(IByteArray message1, IByteArray message2) {
			int len1 = 0;

			if(message1 != null) {
				len1 = message1.Length;
			}

			int len2 = 0;

			if(message2 != null) {
				len2 = message2.Length;
			}

			IByteArray buffer = MemoryAllocators.Instance.allocator.Take(len1 + len2);

			if(message1 != null) {
				message1.CopyTo(buffer);
			}

			if(message2 != null) {
				message2.CopyTo(buffer, len1);
			}

			// do the hash
			IByteArray result = this.Hash(buffer);

			buffer.Return();

			return result;
		}

		public IByteArray HashTwo(IByteArray message1, short message2) {
			return this.HashTwo(message1, (ByteArray) BitConverter.GetBytes(message2));
		}

		public IByteArray HashTwo(IByteArray message1, int message2) {
			return this.HashTwo(message1, (ByteArray) BitConverter.GetBytes(message2));
		}

		public IByteArray HashTwo(IByteArray message1, long message2) {
			return this.HashTwo(message1, (ByteArray) BitConverter.GetBytes(message2));
		}

		public IByteArray HashTwo(short message1, short message2) {
			return this.HashTwo((ByteArray) BitConverter.GetBytes(message1), (ByteArray) BitConverter.GetBytes(message2));
		}

		public IByteArray HashTwo(ushort message1, ushort message2) {
			return this.HashTwo((ByteArray) BitConverter.GetBytes(message1), (ByteArray) BitConverter.GetBytes(message2));
		}

		public IByteArray HashTwo(ushort message1, long message2) {
			return this.HashTwo((ByteArray) BitConverter.GetBytes(message1), (ByteArray) BitConverter.GetBytes(message2));
		}

		public IByteArray HashTwo(int message1, int message2) {
			return this.HashTwo((ByteArray) BitConverter.GetBytes(message1), (ByteArray) BitConverter.GetBytes(message2));
		}

		public IByteArray HashTwo(uint message1, uint message2) {
			return this.HashTwo((ByteArray) BitConverter.GetBytes(message1), (ByteArray) BitConverter.GetBytes(message2));
		}

		public IByteArray HashTwo(long message1, long message2) {
			return this.HashTwo((ByteArray) BitConverter.GetBytes(message1), (ByteArray) BitConverter.GetBytes(message2));
		}

		public IByteArray HashTwo(ulong message1, ulong message2) {
			return this.HashTwo((ByteArray) BitConverter.GetBytes(message1), (ByteArray) BitConverter.GetBytes(message2));
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public bool IsDisposed { get; private set; }

		public IByteArray HashTwo(IByteArray message1, ulong message2) {
			return this.HashTwo(message1, (ByteArray) BitConverter.GetBytes(message2));
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