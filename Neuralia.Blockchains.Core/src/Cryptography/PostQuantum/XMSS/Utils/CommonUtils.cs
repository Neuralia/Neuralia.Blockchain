using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;
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
		public static void Xor(IByteArray result, IByteArray first, IByteArray second) {
			int len = first.Length / sizeof(long);

			var cFirst = first.CastedArray<long>();
			var cSecond = second.CastedArray<long>();
			var cResult = result.CastedArray<long>();

			for(int i = 0; i < len; i += 4) {

				cResult[i + 0] = cFirst[i + 0] ^ cSecond[i + 0];
				cResult[i + 1] = cFirst[i + 1] ^ cSecond[i + 1];
				cResult[i + 2] = cFirst[i + 2] ^ cSecond[i + 2];
				cResult[i + 3] = cFirst[i + 3] ^ cSecond[i + 3];
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray PRF(IByteArray key, int index, XMSSExecutionContext xmssExecutionContext) {

			IByteArray indexBytes = ToBytes(index, 32);

			IByteArray result = PRF(key, indexBytes, xmssExecutionContext);

			indexBytes.Return();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray PRF(IByteArray key, CommonAddress adrs, XMSSExecutionContext xmssExecutionContext) {

			// do note return this array, it is only lent for performance
			return PRF(key, adrs.ToByteArray(), xmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray PRF(IByteArray key, IByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			return HashEntry(HashCodes.Prf, key, buffer, xmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray PRF(IByteArray key, CommonAddress adrs, WotsPlusEngine.ThreadContext threadContext) {
			return HashEntry(HashCodes.Prf, threadContext.digest, key, adrs.ToByteArray(), threadContext.XmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray F(IByteArray key, IByteArray buffer, WotsPlusEngine.ThreadContext threadContext) {

			return HashEntry(HashCodes.F, threadContext.digest, key, buffer, threadContext.XmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray HashEntry(HashCodes hashCode, IByteArray key, IByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			IDigest digest = xmssExecutionContext.DigestPool.GetObject();
			IByteArray hash = HashEntry(hashCode, digest, key, buffer, xmssExecutionContext);

			xmssExecutionContext.DigestPool.PutObject(digest);

			return hash;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray HashEntry(HashCodes hashCode, IDigest digest, IByteArray key, IByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			IByteArray hash = null;

			if(digest is ShaDigestBase digestBase) {

				IByteArray index = ToBytes((int) hashCode, xmssExecutionContext.DigestSize);

				// soince we know the final size, lets preset the size of the buffer
				digestBase.ResetFixed(buffer.Length + key.Length + index.Length);

				digest.BlockUpdate(index.Bytes, index.Offset, index.Length);
				digest.BlockUpdate(key.Bytes, key.Offset, key.Length);
				digest.BlockUpdate(buffer.Bytes, buffer.Offset, buffer.Length);

				digestBase.DoFinalReturn(out hash);

				index.Return();
			}

			return hash;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray Hash(IByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			IDigest digest = xmssExecutionContext.DigestPool.GetObject();
			IByteArray hash = null;

			if(digest is ShaDigestBase digestBase) {
				// soince we know the final size, lets preset the size of the buffer
				digestBase.ResetFixed(buffer.Length);

				digest.BlockUpdate(buffer.Bytes, buffer.Offset, buffer.Length);

				digestBase.DoFinalReturn(out hash);
			}

			xmssExecutionContext.DigestPool.PutObject(digest);

			return hash;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray Hash(IByteArray buffer, IByteArray buffer2, XMSSExecutionContext xmssExecutionContext) {

			IDigest digest = xmssExecutionContext.DigestPool.GetObject();
			IByteArray hash = null;

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
		public static IByteArray ToBytes(long value, int sizeInByte) {
			IByteArray result = MemoryAllocators.Instance.cryptoAllocator.Take(sizeInByte);

			return ToBytes(value, result);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray ToBytes(long value, IByteArray result) {

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
		public static IByteArray Concatenate(IByteArray a, IByteArray b) {
			if(a == null) {
				return b.Clone();
			}

			if(b == null) {
				return a.Clone();
			}

			IByteArray joined = MemoryAllocators.Instance.cryptoAllocator.Take(a.Length + b.Length);

			a.CopyTo(joined, 0, 0, a.Length);
			b.CopyTo(joined, 0, a.Length, b.Length);

			return joined;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IByteArray Concatenate(IByteArray a, IByteArray b, IByteArray c) {
			if((a != null) && (b != null) && (c != null)) {
				IByteArray rv = MemoryAllocators.Instance.cryptoAllocator.Take(a.Length + b.Length + c.Length);

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool EqualsConstantTime(IByteArray a, IByteArray b) {
			int len = a.Length;

			if(len != b.Length) {
				return false;
			}

			len /= sizeof(long);

			var cFirst = a.CastedArray<long>();
			var cSecond = b.CastedArray<long>();

			long difference = 0;

			for(; len != 0; len -= 4) {
				difference |= cFirst[len - 1] ^ cSecond[len - 1];
				difference |= cFirst[len - 2] ^ cSecond[len - 2];
				difference |= cFirst[len - 3] ^ cSecond[len - 3];
				difference |= cFirst[len - 4] ^ cSecond[len - 4];
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
		public static (IByteArray publicSeed, IByteArray secretSeed, IByteArray secretSeedPrf) GenerateSeeds(XMSSExecutionContext xmssExecutionContext) {
#if DETERMINISTIC_DEBUG
			// do not change this order, to match Bouncy's code

			IByteArray secretSeed = MemoryAllocators.Instance.cryptoAllocator.Take(xmssExecutionContext.DigestSize);
			xmssExecutionContext.Random.NextBytes(secretSeed.Bytes, secretSeed.Offset, secretSeed.Length);

			IByteArray secretSeedPrf = MemoryAllocators.Instance.cryptoAllocator.Take(xmssExecutionContext.DigestSize);
			xmssExecutionContext.Random.NextBytes(secretSeedPrf.Bytes, secretSeedPrf.Offset, secretSeedPrf.Length);

			IByteArray publicSeed = MemoryAllocators.Instance.cryptoAllocator.Take(xmssExecutionContext.DigestSize);
			xmssExecutionContext.Random.NextBytes(publicSeed.Bytes, publicSeed.Offset, publicSeed.Length);
#else

			// it is VERY important
			MemoryBlockDoubleArray pool = MemoryAllocators.Instance.doubleArrayCryptoAllocator.Take(50);

			for(int i = 0; i < pool.Length; i++) {

				IByteArray buffer = MemoryAllocators.Instance.cryptoAllocator.Take(xmssExecutionContext.DigestSize);
				xmssExecutionContext.Random.NextBytes(buffer.Bytes, buffer.Offset, buffer.Length);
				pool[i] = buffer;
			}

			var entries = pool.ToList();

			entries.Shuffle(xmssExecutionContext.Random);

			IByteArray publicSeed = entries[0].Clone();
			IByteArray secretSeedPrf = entries[1].Clone();
			IByteArray secretSeed = entries[2].Clone();

			pool.Return();
#endif
			return (publicSeed, secretSeed, secretSeedPrf);
		}
	}
}