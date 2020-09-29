using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime {
	
	/// <summary>
	/// NTRU Prime engine
	/// </summary>
	/// <remarks>This engine is a reimplementation based on the round 2 submission to NIST by [Daniel J. Bernstein, University of Illinois at Chicago, USA], [Chitchanok Chuengsatiansup, École Normale Supérieure de Lyon, France], [Tanja Lange, Technische Universiteit Eindhoven, Netherlands], [Christine van Vredendaal, Technische Universiteit Eindhoven, Netherlands]. https://ntruprime.cr.yp.to/.  no license from the authors of the NTRU Prime documents could be found we thus license it as the rest of the code in this library.</remarks>
	public unsafe class NtruPrimeEngine : IDisposableExtended {

		private readonly SHA512 hasher = new SHA512Managed();
		private NTRUPrimeApiParameters _apiParameters;
		private NTRUPrimeInternalParameters _params;

		private ByteArray randomBuffer;
		
		private readonly sbyte* fPtr;
		private readonly sbyte* gPtr;
		private readonly sbyte* rPtr;
		private readonly sbyte* vPtr;
		private readonly sbyte* fgPtr;

		private readonly ByteArray fsBuffer;
		private readonly ClosureWrapper<MemoryHandle> fsHandle;
		private readonly short* fsPtr;
		
		private readonly ByteArray gsBuffer;
		private readonly ClosureWrapper<MemoryHandle> gsHandle;
		private readonly short* gsPtr;
		
		private readonly ByteArray rsBuffer;
		private readonly ClosureWrapper<MemoryHandle> rsHandle;
		private readonly short* rsPtr;
		
		private readonly ByteArray vsBuffer;
		private readonly ClosureWrapper<MemoryHandle> vsHandle;
		private readonly short* vsPtr;
		
		private readonly ByteArray fg2Buffer;
		private readonly ClosureWrapper<MemoryHandle> fg2Handle;
		private readonly short* fg2Ptr;
		
		private readonly ByteArray LBuffer;
		private readonly ClosureWrapper<MemoryHandle> LHandle;
		private readonly uint* LPtr;
		
		private readonly ByteArray MBuffer;
		private readonly ClosureWrapper<MemoryHandle> MHandle;
		private readonly ushort* MPtr;
		
		private readonly ByteArray RBuffer;
		private readonly ClosureWrapper<MemoryHandle> RHandle;
		private readonly ushort* RPtr;

		private readonly ByteArray xHBuffer;
		private readonly ClosureWrapper<MemoryHandle> xHHandle;
		private readonly byte* xHPtr;
		
		private readonly ByteArray xHSBuffer;
		private readonly ClosureWrapper<MemoryHandle> xHSHandle;
		private readonly byte* xHSPtr;

		private int Ciphertexts_bytesConfirm_bytesLength;
		public NtruPrimeEngine() : this(NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_857) {
		}

		public NtruPrimeEngine(NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes strengthTypes) {
			this._apiParameters = new NTRUPrimeApiParameters(strengthTypes);
			this._params = new NTRUPrimeInternalParameters(strengthTypes);

			this.Ciphertexts_bytesConfirm_bytesLength = this._params.Ciphertexts_bytes + this._params.Confirm_bytes;
			
			this.randomBuffer = ByteArray.Create(this._params.p);

			this.fsBuffer = ByteArray.Create<short>(this._params.p + 1);
			this.fsHandle = this.fsBuffer.Memory.Pin();
			this.fsPtr = (short*) this.fsHandle.Value.Pointer;
			this.fPtr = (sbyte*) this.fsPtr;

			this.gsBuffer = ByteArray.Create<short>(this._params.p + 1);
			this.gsHandle = this.gsBuffer.Memory.Pin();
			this.gsPtr = (short*) this.gsHandle.Value.Pointer;
			this.gPtr = (sbyte*) this.gsPtr;

			this.vsBuffer = ByteArray.Create<short>(this._params.p + 1);
			this.vsHandle = this.vsBuffer.Memory.Pin();
			this.vsPtr = (short*) this.vsHandle.Value.Pointer;
			this.vPtr = (sbyte*) this.vsPtr;

			this.rsBuffer = ByteArray.Create<short>(this._params.p + 1);
			this.rsHandle = this.rsBuffer.Memory.Pin();
			this.rsPtr = (short*) this.rsHandle.Value.Pointer;
			this.rPtr = (sbyte*) this.rsPtr;

			
			this.fg2Buffer = ByteArray.Create<short>((this._params.p + this._params.p) - 1);
			this.fg2Handle = this.fg2Buffer.Memory.Pin();
			this.fg2Ptr = (short*) this.fg2Handle.Value.Pointer;
			this.fgPtr = (sbyte*) this.fg2Ptr;
			

			this.RBuffer = ByteArray.Create<ushort>(this._params.p);
			this.RHandle = this.RBuffer.Memory.Pin();
			this.RPtr = (ushort*) this.RHandle.Value.Pointer;

			this.MBuffer = ByteArray.Create<ushort>(this._params.p);
			this.MHandle = this.MBuffer.Memory.Pin();
			this.MPtr = (ushort*) this.MHandle.Value.Pointer;

			
			this.LBuffer = ByteArray.Create<uint>(this._params.p);
			this.LHandle = this.LBuffer.Memory.Pin();
			this.LPtr = (uint*) this.LHandle.Value.Pointer;

			
			this.xHBuffer = ByteArray.Create(this._params.Hash_bytes * 2);
			this.xHHandle = this.xHBuffer.Memory.Pin();
			this.xHPtr = (byte*) this.xHHandle.Value.Pointer;

			this.xHSBuffer = ByteArray.Create(this._params.Hash_bytes + this.Ciphertexts_bytesConfirm_bytesLength);
			this.xHSHandle = this.xHSBuffer.Memory.Pin();
			this.xHSPtr = (byte*) this.xHSHandle.Value.Pointer;

		}
		
		public (SafeArrayHandle pk, SafeArrayHandle sk) CryptoKemKeypair() {
			var pk = SafeArrayHandle.Create(this._apiParameters.PublicKeyBytes);
			var sk = SafeArrayHandle.Create(this._apiParameters.SecretKeyBytes);

			fixed(byte* pkPtr = pk.Span, skPtr = sk.Span) {
				this.CryptoKemKeypair(pkPtr, skPtr);
			}

			return (pk, sk);
		}

		public (SafeArrayHandle cypher, SafeArrayHandle session) CryptoKemEnc(in SafeArrayHandle plaintext, in SafeArrayHandle publicKey) {
			if(publicKey == null) {
				throw new ArgumentNullException(nameof(publicKey));
			}

			if(publicKey.IsNull || (publicKey.Length != this._apiParameters.PublicKeyBytes)) {
				throw new ArgumentException($"Array must be of length {this._apiParameters.PublicKeyBytes}", nameof(publicKey));
			}

			var ct = SafeArrayHandle.Create(this._apiParameters.CipherTextBytes);
			SafeArrayHandle session = plaintext.Clone();

			fixed(byte* ctPtr = ct.Span, ssPtr = session.Span, pkPtr = publicKey.Span) {
				this.CryptoKemEnc(ctPtr, ssPtr, pkPtr);
			}

			return (ct, session);
		}

		public SafeArrayHandle CryptoKemDec(in SafeArrayHandle ciphertext, in SafeArrayHandle secretKey) {
			if(secretKey == null) {
				throw new ArgumentNullException(nameof(secretKey));
			}

			if(secretKey.IsNull || (secretKey.Length != this._apiParameters.SecretKeyBytes)) {
				throw new ArgumentException($"Array must be of length {this._apiParameters.SecretKeyBytes}", nameof(secretKey));
			}

			if(ciphertext == null) {
				throw new ArgumentNullException(nameof(ciphertext));
			}

			if(ciphertext.IsNull || (ciphertext.Length != this._apiParameters.CipherTextBytes)) {
				throw new ArgumentException($"Array must be of length {this._apiParameters.CipherTextBytes}", nameof(ciphertext));
			}

			var ss = SafeArrayHandle.Create(NTRUPrimeInternalParameters.HASH_Size);

			fixed(byte* ssPtr = ss.Span, ctPtr = ciphertext.Span, skPtr = secretKey.Span) {

				this.CryptoKemDec(ssPtr, ctPtr, skPtr);
			}

			return ss;
		}

	#region SNtrup implementation

		/* return -1 if x!=0; else return 0 */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal int int16_nonzero_mask(short x) {
			var u = (ushort) x; // 0, else 1...65535
			uint v = u; // 0, else 1...65535
			v = (uint) -v; // 0, else 2^32-65535...2^32-1
			v >>= 31; // 0, else 1

			return (int) -v; // 0, else -1
		}

		/* return -1 if x<0; otherwise return 0 */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal int int16_negative_mask(short x) {
			var u = (ushort) x;
			u >>= 15;

			return -u;

			/* alternative with gcc -fwrapv: */
			/* x>>15 compiles to CPU's arithmetic right shift */
		}

		/* ----- arithmetic mod 3 */

		/* F3 is always represented as -1,0,1 */
		/* so ZZ_fromF3 is a no-op */

		/* x must not be close to top int16 */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal sbyte F3_freeze(short x) {
			return (sbyte) (Int32Helper.int32_mod_uint14(x + 1, 3) - 1);
		}

		/* ----- arithmetic mod q */
		/* always represented as -q12...q12 */
		/* so ZZ_fromFq is a no-op */
		/* x must not be close to top int32 */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal short Fq_freeze(int x) {
			int q = this._params.q;
			int qSet = this._params.q12;

			return (short) (Int32Helper.int32_mod_uint14(x + qSet, (ushort) q) - qSet);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal short Fq_recip(short a1) {
			var i = 1;
			short ai = a1;

			while(i < (this._params.q - 2)) {
				ai = this.Fq_freeze(a1 * ai);
				i += 1;
			}

			return ai;
		}

		/* ----- small polynomials */
		/* 0 if Weightw_is(r), else -1 */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal int Weightw_mask(sbyte* r) {
			var weight = 0;
			int i;

			for(i = 0; i < this._params.p; ++i) {
				weight += r[i] & 1;
			}

			return this.int16_nonzero_mask((short) (weight - this._params.w));
		}

		/* R3_fromR(R_fromRq(r)) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void R3_fromRq(sbyte* @out, short* r) {
			int i;

			for(i = 0; i < this._params.p; ++i) {
				@out[i] = this.F3_freeze(r[i]);
			}
		}

		/* h = f*g in the ring R3 */
		private void R3_mult(sbyte* h, sbyte* f, sbyte* g) {
			sbyte result;
			int i;
			int j;

			for(i = 0; i < this._params.p; ++i) {
				result = 0;

				for(j = 0; j <= i; ++j) {
					result = this.F3_freeze((short) (result + (f[j] * g[i - j])));
				}

				this.fgPtr[i] = result;
			}

			for(i = this._params.p; i < ((this._params.p + this._params.p) - 1); ++i) {
				result = 0;

				for(j = (i - this._params.p) + 1; j < this._params.p; ++j) {
					result = this.F3_freeze((short) (result + (f[j] * g[i - j])));
				}

				this.fgPtr[i] = result;
			}

			for(i = (this._params.p + this._params.p) - 2; i >= this._params.p; --i) {
				this.fgPtr[i - this._params.p] = this.F3_freeze((short) (this.fgPtr[i - this._params.p] + this.fgPtr[i]));
				this.fgPtr[(i - this._params.p) + 1] = this.F3_freeze((short) (this.fgPtr[(i - this._params.p) + 1] + this.fgPtr[i]));
			}

			Buffer.MemoryCopy(this.fgPtr, h, this._params.p, this._params.p);
		}

		/* returns 0 if recip succeeded; else -1 */

		private int R3_recip(sbyte* output, sbyte* input) {

			int i;
			int loop;
			int delta;
			int sign;
			int swap;
			sbyte t;

			// clear the amount in sbytes
			this.vsBuffer.Clear(0, this.vsBuffer.Length);
			this.rsBuffer.Clear(0, this.rsBuffer.Length);

			this.rPtr[0] = 1;

			this.fsBuffer.Clear(0, this._params.p);

			this.fPtr[0] = 1;
			this.fPtr[this._params.p - 1] = this.fPtr[this._params.p] = -1;

			for(i = 0; i < this._params.p; ++i) {
				this.gPtr[this._params.p - 1 - i] = input[i];
			}

			this.gPtr[this._params.p] = 0;

			delta = 1;

			for(loop = 0; loop < ((2 * this._params.p) - 1); ++loop) {
				for(i = this._params.p; i > 0; --i) {
					this.vPtr[i] = this.vPtr[i - 1];
				}

				this.vPtr[0] = 0;

				sign = -this.gPtr[0] * this.fPtr[0];
				swap = this.int16_negative_mask((short) -delta) & this.int16_nonzero_mask(this.gPtr[0]);
				delta ^= swap & (delta ^ -delta);
				delta += 1;

				for(i = 0; i < (this._params.p + 1); ++i) {
					t = (sbyte) (swap & (this.fPtr[i] ^ this.gPtr[i]));
					this.fPtr[i] ^= t;
					this.gPtr[i] ^= t;
					t = (sbyte) (swap & (this.vPtr[i] ^ this.rPtr[i]));
					this.vPtr[i] ^= t;
					this.rPtr[i] ^= t;
					
					this.gPtr[i] = this.F3_freeze((short) (this.gPtr[i] + (sign * this.fPtr[i])));
					this.rPtr[i] = this.F3_freeze((short) (this.rPtr[i] + (sign * this.vPtr[i])));
				}
				
				for(i = 0; i < this._params.p; ++i) {
					this.gPtr[i] = this.gPtr[i + 1];
				}

				this.gPtr[this._params.p] = 0;
			}

			sign = this.fPtr[0];

			for(i = 0; i < this._params.p; ++i) {
				output[i] = (sbyte) (sign * this.vPtr[this._params.p - 1 - i]);
			}

			return this.int16_nonzero_mask((short) delta);
		}

		/* ----- polynomials mod q */
		/* h = f*g in the ring Rq */
		private void Rq_mult_small(short* h, short* f, sbyte* g) {
			short result;
			int i;
			int j;

			for(i = 0; i < this._params.p; ++i) {
				result = 0;

				for(j = 0; j <= i; ++j) {
					result = this.Fq_freeze(result + (f[j] * g[i - j]));
				}

				this.fg2Ptr[i] = result;
			}

			for(i = this._params.p; i < ((this._params.p + this._params.p) - 1); ++i) {
				result = 0;

				for(j = (i - this._params.p) + 1; j < this._params.p; ++j) {
					result = this.Fq_freeze(result + (f[j] * g[i - j]));
				}

				this.fg2Ptr[i] = result;
			}

			for(i = (this._params.p + this._params.p) - 2; i >= this._params.p; --i) {
				this.fg2Ptr[i - this._params.p] = this.Fq_freeze(this.fg2Ptr[i - this._params.p] + this.fg2Ptr[i]);
				this.fg2Ptr[(i - this._params.p) + 1] = this.Fq_freeze(this.fg2Ptr[(i - this._params.p) + 1] + this.fg2Ptr[i]);
			}

			for(i = 0; i < this._params.p; ++i) {
				h[i] = this.fg2Ptr[i];
			}
		}

		/* h = 3f in Rq */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Rq_mult3(short* h, short* f) {
			int i;

			for(i = 0; i < this._params.p; ++i) {
				h[i] = this.Fq_freeze(3 * f[i]);
			}
		}

		/* out = 1/(3*in) in Rq */
		/* returns 0 if recip succeeded; else -1 */
		private int Rq_recip3(short* output, sbyte* input) {

			int i, loop, delta;
			int swap, t;
			int f0, g0;
			short scale;

			this.vsBuffer.Clear();
			this.rsBuffer.Clear();

			this.rsPtr[0] = this.Fq_recip(3);

			this.fsBuffer.Clear(0, this._params.p * sizeof(short));

			this.fsPtr[0] = 1;
			this.fsPtr[this._params.p - 1] = this.fsPtr[this._params.p] = -1;

			for(i = 0; i < this._params.p; ++i) {
				this.gsPtr[this._params.p - 1 - i] = input[i];
			}

			this.gsPtr[this._params.p] = 0;

			delta = 1;

			for(loop = 0; loop < ((2 * this._params.p) - 1); ++loop) {
				for(i = this._params.p; i > 0; --i) {
					this.vsPtr[i] = this.vsPtr[i - 1];
				}

				this.vsPtr[0] = 0;

				swap = this.int16_negative_mask((short) -delta) & this.int16_nonzero_mask(this.gsPtr[0]);
				delta ^= swap & (delta ^ -delta);
				delta += 1;

				for(i = 0; i < (this._params.p + 1); ++i) {
					t = swap & (this.fsPtr[i] ^ this.gsPtr[i]);
					this.fsPtr[i] ^= (short) t;
					this.gsPtr[i] ^= (short) t;
					t = swap & (this.vsPtr[i] ^ this.rsPtr[i]);
					this.vsPtr[i] ^= (short) t;
					this.rsPtr[i] ^= (short) t;
				}

				f0 = this.fsPtr[0];
				g0 = this.gsPtr[0];

				for(i = 0; i < (this._params.p + 1); ++i) {
					this.gsPtr[i] = this.Fq_freeze((f0 * this.gsPtr[i]) - (g0 * this.fsPtr[i]));
					this.rsPtr[i] = this.Fq_freeze((f0 * this.rsPtr[i]) - (g0 * this.vsPtr[i]));
				}

				for(i = 0; i < this._params.p; ++i) {
					this.gsPtr[i] = this.gsPtr[i + 1];
				}

				this.gsPtr[this._params.p] = 0;
			}

			scale = this.Fq_recip(this.fsPtr[0]);

			for(i = 0; i < this._params.p; ++i) {
				output[i] = this.Fq_freeze(scale * this.vsPtr[this._params.p - 1 - i]);
			}

			return this.int16_nonzero_mask((short) delta);
		}

		/* ----- rounded polynomials mod q */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Round(short* output, short* a) {

			for(int i = 0; i < this._params.p; ++i) {
				output[i] = (short) (a[i] - this.F3_freeze(a[i]));
			}
		}

		/* ----- sorting to generate short polynomial */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Short_fromlist(sbyte* output, uint* input) {

			int i;

			for(i = 0; i < this._params.w; ++i) {
				this.LPtr[i] = (uint) (input[i] & -2);
			}

			for(i = this._params.w; i < this._params.p; ++i) {
				this.LPtr[i] = (uint) ((input[i] & -3) | 1);
			}

			Uint32Helper.uint32_sort(this.LPtr, this._params.p);

			for(i = 0; i < this._params.p; ++i) {
				output[i] = (sbyte) ((this.LPtr[i] & 3) - 1);
			}
		}

		/* ----- underlying hash function */
		/* e.g., b = 0 means out = Hash0(in) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Hash(byte* output, int b, byte* input, int inlen) {
			var x = new byte[inlen + 1];

			x[0] = (byte) b;

			fixed(byte* xPtr = x) {
				Buffer.MemoryCopy(input, xPtr + 1, inlen, inlen);
			}

			byte[] h = this.hasher.ComputeHash(x);

			fixed(byte* hPtr = h) {

				Buffer.MemoryCopy(hPtr, output, this._params.Hash_bytes, this._params.Hash_bytes);
			}
		}

		/* ----- higher-level randomness */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Short_random(sbyte* output) {
			
			this.LBuffer.FillSafeRandom();

			this.Short_fromlist(output, this.LPtr);
		}

		/// <summary>
		/// produce a random result that is between -1, 0 and 1
		/// </summary>
		/// <param name="output"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Small_random(sbyte* output) {

			this.randomBuffer.FillSafeRandom();
			
			for(int i = 0; i < this._params.p; ++i) {
				byte random = this.randomBuffer[i];
				int value = 0;
				value += (random & 1);
				value += ((random & 2) >> 1);
				
				output[i] = (sbyte)(value -1);
			}
		}

		/* ----- Streamlined NTRU Prime Core */
		/* h,(f,ginv) = KeyGen() */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void KeyGen(short* h, sbyte* f, sbyte* ginv) {
			var g = new sbyte[this._params.p];
			var finv = new short[this._params.p];

			fixed(sbyte* gPtr = g) {
				for(;;) {
					this.Small_random(gPtr);

					if(this.R3_recip(ginv, gPtr) == 0) {
						break;
					}
				}

				fixed(short* finvPtr = finv) {
					this.Short_random(f);
					this.Rq_recip3(finvPtr, f); // always works
					this.Rq_mult_small(h, finvPtr, gPtr);
				}

			}

		}

		/* c = Encrypt(r,h) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Encrypt(short* c, sbyte* r, short* h) {
			var hr = new short[this._params.p];

			fixed(short* hrPtr = hr) {
				this.Rq_mult_small(hrPtr, h, r);
				this.Round(c, hrPtr);
			}

		}

		/* r = Decrypt(c,(f,ginv)) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Decrypt(sbyte* r, short* c, sbyte* f, sbyte* ginv) {
			var cf = new short[this._params.p];
			var cf3 = new short[this._params.p];
			var e = new sbyte[this._params.p];
			var ev = new sbyte[this._params.p];
			int mask;
			int i;

			fixed(short* cfPtr = cf, cf3Ptr = cf3) {

				this.Rq_mult_small(cfPtr, c, f);
				this.Rq_mult3(cf3Ptr, cfPtr);

				fixed(sbyte* ePtr = e, evPtr = ev) {
					this.R3_fromRq(ePtr, cf3Ptr);
					this.R3_mult(evPtr, ePtr, ginv);
					mask = this.Weightw_mask(evPtr); // 0 if weight w, else -1                    
				}
			}

			for(i = 0; i < this._params.w; ++i) {
				r[i] = (sbyte) (((ev[i] ^ 1) & ~mask) ^ 1);
			}

			for(i = this._params.w; i < this._params.p; ++i) {
				r[i] = (sbyte) (ev[i] & ~mask);
			}
		}

		/* ----- encoding small polynomials (including short polynomials) */
		/* these are the only functions that rely on p mod 4 = 1 */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Small_encode(byte* s, sbyte* f) {
			sbyte x;

			for(int i = 0; i < (this._params.p >> 2); ++i) {
				x = (sbyte) (*f++ + 1);
				x += (sbyte) ((*f++ + 1) << 2);
				x += (sbyte) ((*f++ + 1) << 4);
				x += (sbyte) ((*f++ + 1) << 6);
				*s++ = (byte) x;
			}

			x = (sbyte) (*f++ + 1);
			*s++ = (byte) x;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Small_decode(sbyte* f, byte* s) {
			byte x;
			int i;

			for(i = 0; i < (this._params.p >> 2); ++i) {
				x = *s++;
				*f++ = (sbyte) ((x & 3) - 1);
				x >>= 2;
				*f++ = (sbyte) ((x & 3) - 1);
				x >>= 2;
				*f++ = (sbyte) ((x & 3) - 1);
				x >>= 2;
				*f++ = (sbyte) ((x & 3) - 1);
			}

			x = *s++;
			*f++ = (sbyte) ((x & 3) - 1);
		}

		/* ----- encoding general polynomials */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Rq_encode(byte* s, short* r) {

			int i;

			for(i = 0; i < this._params.p; ++i) {
				this.RPtr[i] = (ushort) (r[i] + this._params.q12);
				this.MPtr[i] = (ushort) this._params.q;
			}
			
			EncoderHelper.Encode(s, this.RPtr, this.MPtr, this._params.p);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Rq_decode(short* r, byte* s) {

			int i;

			for(i = 0; i < this._params.p; ++i) {
				this.MPtr[i] = (ushort) this._params.q;
			}

			EncoderHelper.Decode(this.RPtr, s, this.MPtr, this._params.p);

			for(i = 0; i < this._params.p; ++i) {
				r[i] = (short) (this.RPtr[i] - this._params.q12);
			}
		}

		/* ----- encoding rounded polynomials */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Rounded_encode(byte* s, short* r) {

			int i;

			for(i = 0; i < this._params.p; ++i) {
				this.RPtr[i] = (ushort) (((r[i] + this._params.q12) * 10923) >> 15);
				this.MPtr[i] = (ushort) ((this._params.q + 2) / 3);
			}

			EncoderHelper.Encode(s, this.RPtr, this.MPtr, this._params.p);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Rounded_decode(short* r, byte* s) {

			int i;

			for(i = 0; i < this._params.p; ++i) {
				this.MPtr[i] = (ushort) ((this._params.q + 2) / 3);
			}

			EncoderHelper.Decode(this.RPtr, s, this.MPtr, this._params.p);
			

			for(i = 0; i < this._params.p; ++i) {
				r[i] = (short) ((this.RPtr[i] * 3) - this._params.q12);
			}
		}

		/* ----- Streamlined NTRU Prime Core plus encoding */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ZKeyGen(byte* pk, byte* sk) {
			var h = new short[this._params.p];
			var f = new sbyte[this._params.p];
			var v = new sbyte[this._params.p];

			fixed(sbyte* fPtr = f, vPtr = v) {

				fixed(short* hPtr = h) {
					this.KeyGen(hPtr, fPtr, vPtr);
					this.Rq_encode(pk, hPtr);
					this.Small_encode(sk, fPtr);
					sk += this._params.Small_bytes;
					this.Small_encode(sk, vPtr);
				}
			}
		}

		/* C = ZEncrypt(r,pk) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ZEncrypt(byte* C, sbyte* r, byte* pk) {
			var h = new short[this._params.p];
			var c = new short[this._params.p];

			fixed(short* hPtr = h, cPtr = c) {

				this.Rq_decode(hPtr, pk);
				this.Encrypt(cPtr, r, hPtr);
				this.Rounded_encode(C, cPtr);
			}
		}

		/* r = ZDecrypt(C,sk) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ZDecrypt(sbyte* r, byte* C, byte* sk) {
			var f = new sbyte[this._params.p];
			var v = new sbyte[this._params.p];
			var c = new short[this._params.p];

			fixed(sbyte* fPtr = f, vPtr = v) {
				fixed(short* cPtr = c) {
					this.Small_decode(fPtr, sk);
					sk += this._params.Small_bytes;
					this.Small_decode(vPtr, sk);
					this.Rounded_decode(cPtr, C);
					this.Decrypt(r, cPtr, fPtr, vPtr);
				}
			}
		}

		/* h = HashConfirm(r,pk,cache); cache is Hash4(pk) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void HashConfirm(byte* h, byte* r, byte* pk, byte* cache) {
			
			this.Hash(this.xHPtr, 3, r, this._params.Inputs_bytes);

			Buffer.MemoryCopy(cache, this.xHPtr + this._params.Hash_bytes, this._params.Hash_bytes, this._params.Hash_bytes);

			this.Hash(h, 2, this.xHPtr, this.xHBuffer.Length);
			
		}

		/* k = HashSession(b,y,z) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void HashSession(byte* k, int b, byte* y, byte* z) {
			
			this.Hash(this.xHSPtr, 3, y, this._params.Inputs_bytes);

			Buffer.MemoryCopy(z, this.xHSPtr + this._params.Hash_bytes, this.Ciphertexts_bytesConfirm_bytesLength, this.Ciphertexts_bytesConfirm_bytesLength);

			this.Hash(k, b, this.xHSPtr, this.xHSBuffer.Length);
		}

		/* pk,sk = KEM_KeyGen() */
		private void KEM_KeyGen(byte* pk, byte* sk) {
			int i;

			this.ZKeyGen(pk, sk);
			sk += this._params.SecretKeys_bytes;

			for(i = 0; i < this._params.PublicKeys_bytes; ++i) {
				*sk++ = pk[i];
			}

			///////////////////////////////////////
			//TODO: replace random in secret key with a more optimized version here
			using var buffer = ByteArray.Create(this._params.Inputs_bytes);
			buffer.FillSafeRandom();

			fixed(byte* ptr = buffer.Span) {

				Buffer.MemoryCopy(ptr, sk, this._params.Inputs_bytes, this._params.Inputs_bytes);
			}

			//////////////////////////////////////
			/// 
			sk += this._params.Inputs_bytes;
			this.Hash(sk, 4, pk, this._params.PublicKeys_bytes);
		}

		/* c,r_enc = Hide(r,pk,cache); cache is Hash4(pk) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Hide(byte* c, byte* r_enc, sbyte* r, byte* pk, byte* cache) {
			this.Small_encode(r_enc, r);
			this.ZEncrypt(c, r, pk);
			c += this._params.Ciphertexts_bytes;
			this.HashConfirm(c, r_enc, pk, cache);
		}

		/* c,k = Encap(pk) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Encap(byte* c, byte* k, byte* pk) {
			var r = new sbyte[this._params.p];
			var r_enc = new byte[this._params.Inputs_bytes];
			var cache = new byte[this._params.Hash_bytes];

			fixed(sbyte* rPtr = r) {

				fixed(byte* r_encPtr = r_enc, cachePtr = cache) {

					this.Hash(cachePtr, 4, pk, this._params.PublicKeys_bytes);
					this.Short_random(rPtr);
					this.Hide(c, r_encPtr, rPtr, pk, cachePtr);
					this.HashSession(k, 1, r_encPtr, c);
				}
			}
		}

		/* 0 if matching ciphertext+confirm, else -1 */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int Ciphertexts_diff_mask(byte* c, byte* c2) {
			ushort differentbits = 0;
			int len = this.Ciphertexts_bytesConfirm_bytesLength;

			while(len-- > 0) {
				differentbits |= (ushort) (*c++ ^ *c2++);
			}

			return (1 & ((differentbits - 1) >> 8)) - 1;
		}

		/* k = Decap(c,sk) */
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Decap(byte* k, byte* c, byte* sk) {
			byte* pk = sk + this._params.SecretKeys_bytes;
			byte* rho = pk + this._params.PublicKeys_bytes;
			byte* cache = rho + this._params.Inputs_bytes;

			var r = new sbyte[this._params.p];
			var r_enc = new byte[this._params.Inputs_bytes];
			var cnew = new byte[this.Ciphertexts_bytesConfirm_bytesLength];
			int mask;
			int i;

			fixed(sbyte* rPtr = r) {
				fixed(byte* r_encPtr = r_enc, cnewPtr = cnew) {
					this.ZDecrypt(rPtr, c, sk);
					this.Hide(cnewPtr, r_encPtr, rPtr, pk, cache);
					mask = this.Ciphertexts_diff_mask(c, cnewPtr);

					for(i = 0; i < this._params.Inputs_bytes; ++i) {
						r_encPtr[i] ^= (byte) (mask & (r_encPtr[i] ^ rho[i]));
					}

					this.HashSession(k, 1 + mask, r_encPtr, c);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CryptoKemKeypair(byte* pk, byte* sk) {
			this.KEM_KeyGen(pk, sk);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CryptoKemEnc(byte* c, byte* k, byte* pk) {
			this.Encap(c, k, pk);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CryptoKemDec(byte* k, byte* c, byte* sk) {
			this.Decap(k, c, sk);
		}

	#endregion

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.hasher.Dispose();
				
				this.randomBuffer.Dispose();

				this.fsHandle.Value.Dispose();
				this.fsBuffer.Dispose();
				
				this.gsHandle.Value.Dispose();
				this.gsBuffer.Dispose();
				
				this.rsHandle.Value.Dispose();
				this.rsBuffer.Dispose();
				
				this.vsHandle.Value.Dispose();
				this.vsBuffer.Dispose();
				
				this.fg2Handle.Value.Dispose();
				this.fg2Buffer.Dispose();
				
				this.LHandle.Value.Dispose();
				this.LBuffer.Dispose();
				
				this.MHandle.Value.Dispose();
				this.MBuffer.Dispose();
				
				this.RHandle.Value.Dispose();
				this.RBuffer.Dispose();
				
				this.xHHandle.Value.Dispose();
				this.xHBuffer.Dispose();
				
				this.xHSHandle.Value.Dispose();
				this.xHSBuffer.Dispose();
			}

			this.IsDisposed = true;
		}

		~NtruPrimeEngine() {
			this.Dispose(false);
		}

	#endregion

	}

}