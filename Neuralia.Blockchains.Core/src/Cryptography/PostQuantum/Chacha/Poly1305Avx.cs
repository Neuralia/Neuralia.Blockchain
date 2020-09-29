#if NETCOREAPP3_1
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	/// <summary>
	///     an optimized poly1305 based on a clever mathematical optimization I read about on the web
	/// </summary>
	public unsafe class Poly1305Avx : IPoly1305, IDisposableExtended {
		private const int BLOCK_SIZE = 16;
		private const int KEY_SIZE = 32;

		private const int TAG_SIZE = BLOCK_SIZE;

		public const int H_INDEX_0 = 0;
		public const int H_INDEX_1 = 2;
		public const int H_INDEX_2 = 4;
		public const int H_INDEX_3 = 6;
		
		
		private readonly ByteArray buffer;
		private readonly ClosureWrapper<MemoryHandle> bufferHandle;
		private readonly uint* h;

		private readonly ByteArray hBuffer;
		private readonly ClosureWrapper<MemoryHandle> hHandle;
		private readonly uint* ptr;


		private uint d4;
		
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

		private readonly Poly1305AvxComponents.AvxState avxstate;

		public Poly1305Avx() {

			if(!Avx2.IsSupported) {
				throw new NotSupportedException();
			}
			
			this.hBuffer = ByteArray.Create<uint>(4);
			this.hHandle = this.hBuffer.Memory.Pin();
			this.h = (uint*) this.hHandle.Value.Pointer;
			
			this.buffer = ByteArray.Create<uint>(8);
			this.bufferHandle = this.buffer.Memory.Pin();
			this.ptr = (uint*) this.bufferHandle.Value.Pointer;
			
			this.avxstate = new Poly1305AvxComponents.AvxState();
		}

		public static int MacSize => TAG_SIZE;
		public static int BlockSize => BLOCK_SIZE;
		public static int KeySize => KEY_SIZE;

		public static SafeArrayHandle CreateMac() {
			return SafeArrayHandle.Create(MacSize);
		}

		public static SafeArrayHandle CreateKey() {
			return SafeArrayHandle.Create(KeySize);
		}

		public void Initialize(SafeArrayHandle key) {
			
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
			
			this.ptr[H_INDEX_0] = this.r0;
			this.ptr[H_INDEX_1] = this.r1;
			this.ptr[H_INDEX_2] = this.r2;
			this.ptr[H_INDEX_3] = this.r3;
			this.avxstate.rb0Vector = Avx.LoadVector256(this.ptr);
			
			this.ptr[H_INDEX_0] = this.rr3;
			this.ptr[H_INDEX_1] = this.r0;
			this.ptr[H_INDEX_2] = this.r1;
			this.ptr[H_INDEX_3] = this.r2;
			this.avxstate.rb1Vector = Avx.LoadVector256(this.ptr);
			
			this.ptr[H_INDEX_0] = this.rr2;
			this.ptr[H_INDEX_1] = this.rr3;
			this.ptr[H_INDEX_2] = this.r0;
			this.ptr[H_INDEX_3] = this.r1;
			this.avxstate.rb2Vector = Avx.LoadVector256(this.ptr);
			
			this.ptr[H_INDEX_0] = this.rr1;
			this.ptr[H_INDEX_1] = this.rr2;
			this.ptr[H_INDEX_2] = this.rr3;
			this.ptr[H_INDEX_3] = this.r0;
			this.avxstate.rb3Vector = Avx.LoadVector256(this.ptr);
			
			// now the last t4s
			this.ptr[H_INDEX_0] = this.rr0;
			this.ptr[H_INDEX_1] = this.rr1;
			this.ptr[H_INDEX_2] = this.rr2;
			this.ptr[H_INDEX_3] = this.rr3;
			this.avxstate.rb4Vector = Avx.LoadVector256(this.ptr);
			
			this.hBuffer.Clear();

			this.h4 = 0;

			Poly1305AvxComponents.SetD4One(ref this.d4, this.avxstate);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void UInt32ToByteLittleEndian(uint n, byte* macPtr, int off) {
			macPtr[off] = (byte) n;
			macPtr[off + 1] = (byte) (n >> 8);
			macPtr[off + 2] = (byte) (n >> 16);
			macPtr[off + 3] = (byte) (n >> 24);
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

					Poly1305AvxComponents.ProcessBlock((uint*) dynPtr, this.h, this.avxstate, ref this.h4, this.d4, this.r0, this.ptr);
					dynPtr += TAG_SIZE;
				}
				
				int remainder = len & 0xF;

				// if we have any remainder, this is where we process them
				if(remainder != 0) {
					var i = 0;

					for(; i < remainder; i++) {
						this.SetRemainderValue((uint*)dynPtr, i, dynPtr[i]);
					}

					// closing value
					Poly1305AvxComponents.SetD4Zero(ref this.d4, this.avxstate); // might need to add less then 2^130 to the final computation
					this.SetRemainderValue((uint*)dynPtr, i, 1);

					// let's process the remainder blocks
					Poly1305AvxComponents.ProcessBlock((uint*) dynPtr, this.h, this.avxstate, ref this.h4, this.d4, this.r0, this.ptr);
				}
			}

			// and prepare the finished product
			// we might need to subtract 2^130-5. we do this by ensuring the carry propagation
			
			var h0 = this.h[0];
			var h1 = this.h[1];
			var h2 = this.h[2];
			var h3 = this.h[3];

			ulong f0 = (ulong) 5 + h0;
			ulong f1 = (f0 >> 32) + h1;
			ulong f2 = (f1 >> 32) + h2;
			ulong f3 = (f2 >> 32) + h3;
			ulong f4 = (f3 >> 32) + this.h4;

			// at this point, f4 tells us how many times to subtract 2^130-5

			// (h + pad) less 2^130-5 if f4 is more than 3
			ulong t0 = ((f4 >> 2) * 5) + h0 + this.pad0;
			ulong t1 = (t0 >> 32) + h1 + this.pad1;
			ulong t2 = (t1 >> 32) + h2 + this.pad2;
			ulong t3 = (t2 >> 32) + h3 + this.pad3;

			// and we pack the final resulting mac
			if (mac == null){
				//pack in cipher buffer directly
				fixed (byte* dataPtr = data.Span)
				{
					var macPtr = dataPtr;
					macPtr += len;
					UInt32ToByteLittleEndian((uint)t0, macPtr, 0);
					UInt32ToByteLittleEndian((uint)t1, macPtr, 4);
					UInt32ToByteLittleEndian((uint)t2, macPtr, 8);
					UInt32ToByteLittleEndian((uint)t3, macPtr, 12);
				}
			}
            else
            {
				// pack in a mac buffer
				fixed (byte* macPtr = mac.Span)
				{
					UInt32ToByteLittleEndian((uint)t0, macPtr, 0);
					UInt32ToByteLittleEndian((uint)t1, macPtr, 4);
					UInt32ToByteLittleEndian((uint)t2, macPtr, 8);
					UInt32ToByteLittleEndian((uint)t3, macPtr, 12);
				}
			}
		}
		
		public void ComputeMac(SafeArrayHandle data, SafeArrayHandle key, int? length = null)
        {
			this.ComputeMac(data, key, default, length);

        }

		public static void VerifyMac(SafeArrayHandle data, SafeArrayHandle key, SafeArrayHandle mac, int? length = null) {
			var polyt1305 = new Poly1305Avx();
			using SafeArrayHandle tag = CreateMac();
			polyt1305.ComputeMac(data, key, tag, length);

			if(!ByteArray.EqualsConstantTime(tag.Entry, mac.Entry)) {
				throw new MacInvalidException();
			}
		}

#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {
				
				this.hHandle.Value.Dispose();
				this.hBuffer.Dispose();
				
				this.bufferHandle.Value.Dispose();
				this.buffer.Dispose();
			}

			this.IsDisposed = true;
		}

		~Poly1305Avx() {
			this.Dispose(false);
		}

#endregion

	}
}
#endif