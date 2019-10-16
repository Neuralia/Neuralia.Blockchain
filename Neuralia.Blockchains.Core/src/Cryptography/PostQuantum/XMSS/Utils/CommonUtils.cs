using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.BouncyCastle.extra.pqc.math.ntru.util;
using Org.BouncyCastle.Crypto;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils {
	public class CommonUtils {

		public enum HashCodes : byte {
			F = 0,
			H = 1,
			HMsg = 2,
			Prf = 3
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Log2(int n) {
			return (int) Math.Log(n, 2);
		}

		/// <summary>
		///     Xor two arrays, return the result in the first array
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void Xor(ByteArray result, ByteArray first, ByteArray second) {
			int len = first.Length / sizeof(long);

			fixed(byte* cFirst = first.Span) {
				fixed(byte* cSecond = second.Span) {
					fixed(byte* cResult = result.Span) {
						
						long* cFirstL = (long*)cFirst;
						long* cSecondL = (long*)cSecond;
						long* cResultL = (long*)cResult;
						
						for(int i = 0; i < len; i += 4) {
							*(cResultL+(i + 0)) = *(cFirstL+(i + 0)) ^ *(cSecondL+(i + 0));
							*(cResultL+(i + 1)) = *(cFirstL+(i + 1)) ^ *(cSecondL+(i + 1));
							*(cResultL+(i + 2)) = *(cFirstL+(i + 2)) ^ *(cSecondL+(i + 2));
							*(cResultL+(i + 3)) = *(cFirstL+(i + 3)) ^ *(cSecondL+(i + 3));
						}
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray PRF(ByteArray key, int index, XMSSExecutionContext xmssExecutionContext) {

			using(ByteArray indexBytes = ToBytes(index, 32)) {

				return PRF(key, indexBytes, xmssExecutionContext);

			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray PRF(ByteArray key, CommonAddress adrs, XMSSExecutionContext xmssExecutionContext) {

			// do note return this array, it is only lent for performance
			return PRF(key, adrs.ToByteArray(), xmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray PRF(ByteArray key, ByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			return HashEntry(HashCodes.Prf, key, buffer, xmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray PRF(ByteArray key, CommonAddress adrs, WotsPlusEngine.ThreadContext threadContext) {
			return HashEntry(HashCodes.Prf, threadContext.digest, key, adrs.ToByteArray(), threadContext.XmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray F(ByteArray key, ByteArray buffer, WotsPlusEngine.ThreadContext threadContext) {

			return HashEntry(HashCodes.F, threadContext.digest, key, buffer, threadContext.XmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray HashEntry(HashCodes hashCode, ByteArray key, ByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			IDigest digest = xmssExecutionContext.DigestPool.GetObject();
			ByteArray hash = HashEntry(hashCode, digest, key, buffer, xmssExecutionContext);

			xmssExecutionContext.DigestPool.PutObject(digest);

			return hash;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray HashEntry(HashCodes hashCode, IDigest digest, ByteArray key, ByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			ByteArray hash = null;

			if(digest is ShaDigestBase digestBase) {

				using(ByteArray index = ToBytes((int) hashCode, xmssExecutionContext.DigestSize)) {

					// soince we know the final size, lets preset the size of the buffer
					digestBase.ResetFixed(buffer.Length + key.Length + index.Length);

					digest.BlockUpdate(index.Bytes, index.Offset, index.Length);
					digest.BlockUpdate(key.Bytes, key.Offset, key.Length);
					digest.BlockUpdate(buffer.Bytes, buffer.Offset, buffer.Length);

					digestBase.DoFinalReturn(out hash);
				}
			}

			return hash;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray Hash(ByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			IDigest digest = xmssExecutionContext.DigestPool.GetObject();
			ByteArray hash = null;

			if(digest is ShaDigestBase digestBase) {
				// soince we know the final size, lets preset the size of the buffer
				digestBase.ResetFixed(buffer.Length);

				digest.BlockUpdate(buffer.Bytes, buffer.Offset, buffer.Length);

				digestBase.DoFinalReturn(out  hash);
			}

			xmssExecutionContext.DigestPool.PutObject(digest);

			return hash;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray Hash(ByteArray buffer, ByteArray buffer2, XMSSExecutionContext xmssExecutionContext) {

			IDigest digest = xmssExecutionContext.DigestPool.GetObject();
			ByteArray hash = null;

			if(digest is ShaDigestBase digestBase) {
				// soince we know the final size, lets preset the size of the buffer
				digestBase.ResetFixed(buffer.Length + buffer2.Length);

				digest.BlockUpdate(buffer.Bytes, buffer.Offset, buffer.Length);
				digest.BlockUpdate(buffer2.Bytes, buffer2.Offset, buffer2.Length);

				digestBase.DoFinalReturn(out hash);
			}

			xmssExecutionContext.DigestPool.PutObject(digest);

			return hash;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte BigEndianByte(int value1, int value2) {
			return (byte) ((value2 & 0xF) | ((value1 & 0xF) << 4));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte BigEndianByte2(int value1, int value2, int value3, int value4) {
			return (byte) ((value4 & 0x3) | ((value3 & 0x3) << 2) | ((value2 & 0x3) << 4) | ((value1 & 0x3) << 6));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray ToBytes(long value, int sizeInByte) {
			ByteArray result = ByteArray.Create(sizeInByte);

			return ToBytes(value, result);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray ToBytes(long value, ByteArray result) {

			int length = Math.Min(sizeof(long), result.Length);
			result.Span.Slice(0, length).Clear();

			Span<byte> buffer = stackalloc byte[sizeof(long)];
			buffer[7] = (byte) (value & 0xFF);
			buffer[6] = (byte) ((value >> (1 * 8)) & 0xFF);
			buffer[5] = (byte) ((value >> (2 * 8)) & 0xFF);
			buffer[4] = (byte) ((value >> (3 * 8)) & 0xFF);
			buffer[3] = (byte) ((value >> (4 * 8)) & 0xFF);
			buffer[2] = (byte) ((value >> (5 * 8)) & 0xFF);
			buffer[1] = (byte) ((value >> (6 * 8)) & 0xFF);
			buffer[0] = (byte) ((value >> (7 * 8)) & 0xFF);

			buffer.Slice(sizeof(long) - length, length).CopyTo(result.Span);

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray Concatenate(ByteArray a, ByteArray b) {
			if(a == null) {
				return b.Clone();
			}

			if(b == null) {
				return a.Clone();
			}

			ByteArray joined = ByteArray.Create(a.Length + b.Length);

			a.CopyTo(joined, 0, 0, a.Length);
			b.CopyTo(joined, 0, a.Length, b.Length);

			return joined;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray Concatenate(ByteArray a, ByteArray b, ByteArray c) {
			if((a != null) && (b != null) && (c != null)) {
				ByteArray rv = ByteArray.Create(a.Length + b.Length + c.Length);

				a.CopyTo(rv, 0, 0, a.Length);
				b.CopyTo(rv, 0, a.Length, b.Length);
				c.CopyTo(rv, 0, a.Length + b.Length, c.Length);

				return rv;
			}

			if(a == null) {
				return Concatenate(b, c);
			}

			if(b == null) {
				return Concatenate(a, c);
			}

			return Concatenate(a, b);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <remarks>must be a multiple of 4</remarks>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe bool EqualsConstantTime(ByteArray a, ByteArray b) {
			int len = a.Length;

			if(len != b.Length) {
				return false;
			}

			len >>= 3;

			if((len & 0x3) != 0) {
				void ThrowMustBeMultiple4Exception() {
					throw new ArgumentException("Arrays size must be a multiple of 4.");
				}
				ThrowMustBeMultiple4Exception();
			}

			long difference = 0;
			
			fixed(byte* cFirst = a.Span) {
				fixed(byte* cSecond = b.Span) {
					
					var cFirstL = (long*)cFirst;
					var cSecondL = (long*)cSecond;
					
					for(; len != 0; len -= 4) {
						difference |= *(cFirstL+(len - 1)) ^ *(cSecondL+(len - 1));
						difference |= *(cFirstL+(len - 2)) ^ *(cSecondL+(len - 2));
						difference |= *(cFirstL+(len - 3)) ^ *(cSecondL+(len - 3));
						difference |= *(cFirstL+(len - 4)) ^ *(cSecondL+(len - 4));
					}
				}
			}
			return difference == 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetThreadCount(Enums.ThreadMode threadMode) {

			if(threadMode == Enums.ThreadMode.Single) {
				return 1;
			}

			int numThreads = Environment.ProcessorCount;

			if(threadMode == Enums.ThreadMode.Quarter) {
				// we dont want to use all the cores on the machine, so we use 25%, its enough. minimum is 1
				return (int) Math.Max(Math.Ceiling(numThreads * 0.25), 1);
			}

			if(threadMode == Enums.ThreadMode.Half) {
				// we dont want to use all the cores on the machine, so we use 50%, its enough. minimum is 1
				return (int) Math.Max(Math.Ceiling(numThreads * 0.50), 1);
			}

			if(threadMode == Enums.ThreadMode.ThreeQuarter) {
				// we dont want to use all the cores on the machine, so we use 75%, its enough. minimum is 1
				return (int) Math.Max(Math.Ceiling(numThreads * 0.75), 1);
			}

			// anything else, go full strength
			return numThreads;
		}

		/// <summary>
		///     Generate the seeds. this is important that it be VERY secure!!
		/// </summary>
		/// <param name="XMSSMTPrivateKey"></param>
		/// <param name="xmssExecutionContext"></param>
		/// <returns></returns>
		public static (ByteArray publicSeed, ByteArray secretSeed, ByteArray secretSeedPrf) GenerateSeeds(XMSSExecutionContext xmssExecutionContext) {
#if DETERMINISTIC_DEBUG
			// do not change this order, to match Bouncy's code

			ArrayWrapperBase secretSeed = ByteArray.Create(xmssExecutionContext.DigestSize);
			xmssExecutionContext.Random.NextBytes(secretSeed.Bytes, secretSeed.Offset, secretSeed.Length);

			ArrayWrapperBase secretSeedPrf = ByteArray.Create(xmssExecutionContext.DigestSize);
			xmssExecutionContext.Random.NextBytes(secretSeedPrf.Bytes, secretSeedPrf.Offset, secretSeedPrf.Length);

			ArrayWrapperBase publicSeed = ByteArray.Create(xmssExecutionContext.DigestSize);
			xmssExecutionContext.Random.NextBytes(publicSeed.Bytes, publicSeed.Offset, publicSeed.Length);
#else

			// it is VERY important
			ByteArray[] pool = new ByteArray[50];

			for(int i = 0; i < pool.Length; i++) {

				ByteArray buffer = ByteArray.Create(xmssExecutionContext.DigestSize);
				xmssExecutionContext.Random.NextBytes(buffer.Bytes, buffer.Offset, buffer.Length);
				pool[i] = buffer;
			}

			var entries = pool.ToList();

			entries.Shuffle(xmssExecutionContext.Random);

			ByteArray publicSeed = entries[0].Clone();
			ByteArray secretSeedPrf = entries[1].Clone();
			ByteArray secretSeed = entries[2].Clone();

			DoubleArrayHelper.Return(pool);
#endif
			return (publicSeed, secretSeed, secretSeedPrf);
		}
	}
}