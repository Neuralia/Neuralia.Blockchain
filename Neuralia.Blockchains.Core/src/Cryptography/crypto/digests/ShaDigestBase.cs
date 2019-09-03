using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.SHA3;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;
using Org.BouncyCastle.Crypto;

namespace Neuralia.Blockchains.Core.Cryptography.crypto.digests {
	public abstract class ShaDigestBase : IDigest, IDisposable2 {

		private const int BYTE_LENGTH = 64;
		private MemoryBlock data;

		private List<object> datas;
		protected int DigestLength;
		private int length;
		private bool preFixed;
		private int prefixedOffset;

		protected HashAlgorithm sha = null;

		public abstract string AlgorithmName { get; }

		public virtual int GetDigestSize() {
			return this.DigestLength;
		}

		public virtual int GetByteLength() {
			return BYTE_LENGTH;
		}

		public void Update(byte input) {
			if(this.preFixed) {
				this.data[this.prefixedOffset++] = input;
			} else {
				this.LazyLoadDatas();

				this.datas.Add(input);
				this.length += 1;
			}
		}

		public void BlockUpdate(byte[] input, int inOff, int length) {
			if(this.preFixed) {
				this.data.CopyFrom(ref input, inOff, this.prefixedOffset, length);
				this.prefixedOffset += length;
			} else {
				this.LazyLoadDatas();

				this.datas.Add((input, inOff, length));
				this.length += length;
			}
		}

		public int DoFinal(byte[] output, int outOff) {

			if(this is Sha3ExternalDigest sha3) {
				sha3.DoFinalReturn(out IByteArray hash);

				hash.CopyTo(output, 0, outOff, hash.Length);

				hash.Return();
			} else {
				var hash2 = this.DoFinalReturn();

				Buffer.BlockCopy(hash2, 0, output, outOff, hash2.Length);

			}

			return this.DigestLength;
		}

		public void Reset() {

			this.datas?.Clear();

			this.length = 0;
			this.preFixed = false;
			this.prefixedOffset = 0;
		}

		public void BlockUpdate(IByteArray input) {
			this.BlockUpdate(input, 0, input.Length);
		}

		public void BlockUpdate(IByteArray input, int inOff, int length) {
			if(this.preFixed) {
				this.data.CopyFrom(input, inOff, this.prefixedOffset, length);
				this.prefixedOffset += length;
			} else {
				this.LazyLoadDatas();

				this.datas.Add((input.Bytes, input.Offset + inOff, length));
				this.length += length;
			}
		}

		/// <summary>
		///     A special method that allows us to skip the dynamic aspect and preset the size.
		/// </summary>
		/// <param name="length"></param>
		public void ResetFixed(int length) {
			this.preFixed = true;
			this.length = length;
			this.prefixedOffset = 0;

			if((this.data == null) || (this.data.Length < this.length)) {

				this.data?.Return();

				this.data = MemoryAllocators.Instance.cryptoAllocator.Take(length);
			}
		}

		private void LazyLoadDatas() {
			if(this.datas == null) {
				this.datas = new List<object>();
			}
		}

		public int DoFinal(byte[] output, int outOff, out IByteArray result) {

			this.DoFinalReturn(out result);

			return this.DigestLength;
		}

		public byte[] DoFinalReturn() {
			this.DoFinalReturn(out IByteArray result);

			return result.ToExactByteArray();
		}

		public void DoFinalReturn(out IByteArray hash) {

			if(!this.preFixed) {
				this.ResetFixed(this.length);

				int offset = 0;

				foreach(object item in this.datas) {
					if(item is IByteArray block) {
						this.data.CopyFrom(block, 0, offset, block.Length);
						offset += block.Length;
					} else if(item is ValueTuple<byte[], int, int> array) {
						var buffer = array.Item1;
						this.data.CopyFrom(ref buffer, array.Item2, offset, array.Item3);
						offset += array.Item3;
					} else if(item is byte smallByte) {
						this.data[offset] = smallByte;
						offset += 1;
					}
				}
			}

			if(this.sha is SHA3Managed sha3) {
				// this is a special case where we can use the MemoryBlock directly.
				hash = sha3.CustomComputeHash(this.data, 0, this.length);

			} else {
				hash = (ByteArray) this.sha.ComputeHash(this.data.Bytes, this.data.Offset, this.length);
			}

			this.Reset();
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {
				try {
					this.sha.Dispose();

					this.data?.Return();

				} finally {
					this.IsDisposed = true;
				}
			}
		}

		~ShaDigestBase() {
			this.Dispose(false);
		}

	#endregion

	}
}