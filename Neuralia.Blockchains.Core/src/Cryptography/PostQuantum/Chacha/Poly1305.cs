using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {

	/// <summary>
	///     an optimized poly1305 based on a clever mathematical optimization I read about on the web
	/// </summary>
	public unsafe class Poly1305 : IPoly1305 {
		public const int BLOCK_SIZE = 16;
		public const int KEY_SIZE = 32;
		public const int MAC_SIZE = BLOCK_SIZE;
		
		private uint d4;
		
		private uint h0;
		private uint h1;
		private uint h2;
		private uint h3;
		private uint h4;

		private uint pad0;
		private uint pad1;
		private uint pad2;
		private uint pad3;

		private uint r0;
		private uint r1;
		private uint r2;
		private uint r3;

		private uint rr0;
		private uint rr1;
		private uint rr2;
		private uint rr3;

		
		public Poly1305() {

		}
		
		public static SafeArrayHandle CreateMac() {
			return SafeArrayHandle.Create(MAC_SIZE);
		}

		public static SafeArrayHandle CreateKey() {
			return SafeArrayHandle.Create(KEY_SIZE);
		}

		private void Initialize(SafeArrayHandle key) {
			
			if(key is null) {
				throw new ArgumentNullException(nameof(key));
			}

			if(key.Length != KEY_SIZE) {
				throw new ArgumentException("Poly1305 key must be 256 bits.");
			}
			
			// Extract r portion of key (and "clamp" the values)
			fixed(byte* pt = key.Span) {
				var keyPtr = (uint*)pt;
				
				this.r0 = keyPtr[0] & 0x0fffffff;
				this.r1 = keyPtr[1] & 0x0ffffffc;
				this.r2 = keyPtr[2] & 0x0ffffffc;
				this.r3 = keyPtr[3] & 0x0ffffffc;

				this.pad0 = keyPtr[4];
				this.pad1 = keyPtr[5];
				this.pad2 = keyPtr[6];
				this.pad3 = keyPtr[7];
			}

			this.rr0 = (this.r0 >> 2) * 5;
			this.rr1 = (this.r1 >> 2) + this.r1;
			this.rr2 = (this.r2 >> 2) + this.r2;
			this.rr3 = (this.r3 >> 2) + this.r3;

			this.h0 = 0;
			this.h1 = 0;
			this.h2 = 0;
			this.h3 = 0;
			this.h4 = 0;

			this.d4 = 1;
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void UInt32ToByteLittleEndian(uint n, byte* macPtr, int off) {
			macPtr[off] = (byte) n;
			macPtr[off + 1] = (byte) (n >> 8);
			macPtr[off + 2] = (byte) (n >> 16);
			macPtr[off + 3] = (byte) (n >> 24);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ProcessBlock(uint* dataPtr) {

			// here start with h + data (h + d). we dont do any carry propagation here
			ulong t0 = (ulong) this.h0 + dataPtr[0];
			ulong t1 = (ulong) this.h1 + dataPtr[1];
			ulong t2 = (ulong) this.h2 + dataPtr[2];
			ulong t3 = (ulong) this.h3 + dataPtr[3];
			uint t4 = this.h4 + this.d4;

			// now the core, we have (h + d) * r. we dont do any carry propagation here
			ulong i0 = (t0 * this.r0) + (t1 * this.rr3) + (t2 * this.rr2) + (t3 * this.rr1) + (t4 * this.rr0);
			ulong i1 = (t0 * this.r1) + (t1 * this.r0) + (t2 * this.rr3) + (t3 * this.rr2) + (t4 * this.rr1);
			ulong i2 = (t0 * this.r2) + (t1 * this.r1) + (t2 * this.r0) + (t3 * this.rr3) + (t4 * this.rr2);
			ulong i3 = (t0 * this.r3) + (t1 * this.r2) + (t2 * this.r1) + (t3 * this.r0) + (t4 * this.rr3);
			uint i4 = t4 * (this.r0 & 3);

			// now we do a partial reduction using a modulo of 2^130 - 5
			var f5 = (uint) (i4 + (i3 >> 32));
			ulong f0 = ((f5 >> 2) * 5) + (i0 & 0xffffffff);
			ulong f1 = (f0 >> 32) + (i1 & 0xffffffff) + (i0 >> 32);
			ulong f2 = (f1 >> 32) + (i2 & 0xffffffff) + (i1 >> 32);
			ulong f3 = (f2 >> 32) + (i3 & 0xffffffff) + (i2 >> 32);
			ulong f4 = (f3 >> 32) + (f5 & 3);

			this.h0 = (uint) f0;
			this.h1 = (uint) f1;
			this.h2 = (uint) f2;
			this.h3 = (uint) f3;
			this.h4 = (uint) f4;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SetRemainderValue(uint* dataPtr, int index, byte value) {
			dataPtr[index >> 2] |= (uint) value << ((index & 0x3) << 3);
		}

		public void ComputeMac(SafeArrayHandle data, SafeArrayHandle key, SafeArrayHandle mac = null, int? length = null) {
			if(data == null) {
				throw new ArgumentNullException(nameof(data), "data cannot be null");
			}

			if(mac != null && BLOCK_SIZE > mac.Length) {
				throw new ArgumentException("Output tag is too short.");
			}

			this.Initialize(key);

			int len = length.HasValue ? length.Value : data.Length;
			int numbBlocks = len >> 4;

			fixed(byte* dataPtr = data.Span) {
				var dynPtr = dataPtr;

				for(var i = 0; i < numbBlocks; i += 1) {

					this.ProcessBlock((uint*) dynPtr);
					dynPtr += MAC_SIZE;
				}

				int remainder = len & 0xF;

				// if we have any remainder, this is where we process them
				if(remainder != 0) {
					var i = 0;

					Span<byte> remainderBuffer = stackalloc byte[MAC_SIZE];

					fixed(byte* dataRemPtr = remainderBuffer) {
						for(; i < remainder; i++) {
							dataRemPtr[i] = dynPtr[i];
							this.SetRemainderValue((uint*) dataRemPtr, i, dataRemPtr[i]);
						}

						// closing value
						this.d4 = 0; // might need to add less then 2^130 to the final computation
						this.SetRemainderValue((uint*) dataRemPtr, i, 1);

						// let's process the remainder blocks
						this.ProcessBlock((uint*) dataRemPtr);
					}
				}
			}


			// and prepare the finished product
			// we might need to subtract 2^130-5. we do this by ensuring the carry propagation

			ulong f0 = (ulong) 5 + this.h0;
			ulong f1 = (f0 >> 32) + this.h1;
			ulong f2 = (f1 >> 32) + this.h2;
			ulong f3 = (f2 >> 32) + this.h3;
			ulong f4 = (f3 >> 32) + this.h4;

			// at this point, f4 tells us how many times to subtract 2^130-5

			// (h + pad) less 2^130-5 if f4 is more than 3
			ulong t0 = ((f4 >> 2) * 5) + this.h0 + this.pad0;
			ulong t1 = (t0 >> 32) + this.h1 + this.pad1;
			ulong t2 = (t1 >> 32) + this.h2 + this.pad2;
			ulong t3 = (t2 >> 32) + this.h3 + this.pad3;

			// and we pack the final resulting mac
			
			Span<byte> resultSpan = mac == null?data.Span.Slice(len, MAC_SIZE):mac.Span;
			
			fixed (byte* macPtr = resultSpan)
			{
				UInt32ToByteLittleEndian((uint)t0, macPtr, 0);
				UInt32ToByteLittleEndian((uint)t1, macPtr, 4);
				UInt32ToByteLittleEndian((uint)t2, macPtr, 8);
				UInt32ToByteLittleEndian((uint)t3, macPtr, 12);
			}
		}

		public static void VerifyMac(SafeArrayHandle data, SafeArrayHandle key, SafeArrayHandle mac, int? length = null) {
			VerifyMac(data, key, mac.Memory, length);
		}
		
		public static void VerifyMac(SafeArrayHandle data, SafeArrayHandle key, Memory<byte> mac, int? length = null) {
			var polyt1305 = new Poly1305();
			using SafeArrayHandle tag = CreateMac();
			polyt1305.ComputeMac(data, key, tag, length);

			if(!ByteArray.EqualsConstantTime(tag.Entry, mac.Span)) {
				throw new MacInvalidException();
			}
		}

		public void ComputeMac(SafeArrayHandle data, SafeArrayHandle key, int? length = null)
        {
			this.ComputeMac(data, key, default, length);

        }
    }
}