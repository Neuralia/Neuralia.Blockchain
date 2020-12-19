using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Data.Pools;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils {
	public static class XMSSCommonUtils {

		public enum HashTypes {
			Regular,
			Backup
		}
		
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

						long* cFirstL = (long*) cFirst;
						long* cSecondL = (long*) cSecond;
						long* cResultL = (long*) cResult;

						for(int i = 0; i < len; i += 4) {
							*(cResultL + (i + 0)) = *(cFirstL + (i + 0)) ^ *(cSecondL + (i + 0));
							*(cResultL + (i + 1)) = *(cFirstL + (i + 1)) ^ *(cSecondL + (i + 1));
							*(cResultL + (i + 2)) = *(cFirstL + (i + 2)) ^ *(cSecondL + (i + 2));
							*(cResultL + (i + 3)) = *(cFirstL + (i + 3)) ^ *(cSecondL + (i + 3));
						}
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray PRF(ByteArray key, int index, XMSSExecutionContext xmssExecutionContext, HashTypes hashType) {

			using ByteArray indexBytes = ToBytes(index, 32);
			return PRF(key, indexBytes, xmssExecutionContext, hashType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray PRF(ByteArray key, CommonAddress adrs, XMSSExecutionContext xmssExecutionContext, HashTypes hashType) {

			// do note dispose this array, it is only lent for performance
			var adrBytes = adrs.ToByteArray();
			return PRF(key, adrBytes, xmssExecutionContext, hashType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray PRF(ByteArray key, ByteArray buffer, XMSSExecutionContext xmssExecutionContext, HashTypes hashType) {

			return HashEntry(HashCodes.Prf, key, buffer, xmssExecutionContext, hashType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray PRF(ByteArray key, CommonAddress adrs, WotsPlus.ThreadContext threadContext) {
			// do note dispose this array, it is only lent for performance
			var adrBytes = adrs.ToByteArray();
			return HashEntry(HashCodes.Prf, threadContext.Digest, key, adrBytes, threadContext.XmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray F(ByteArray key, ByteArray buffer, WotsPlus.ThreadContext threadContext) {

			return HashEntry(HashCodes.F, threadContext.Digest, key, buffer, threadContext.XmssExecutionContext);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray HashEntry(HashCodes hashCode, ByteArray key, ByteArray buffer, XMSSExecutionContext xmssExecutionContext, HashTypes hashType) {

			IHashDigest digest = null;
			ObjectPool<IHashDigest> returnPool = null;
			
			try {
				if(hashType == HashTypes.Regular) {
					returnPool = xmssExecutionContext.DigestPool;
				} else {
					returnPool = xmssExecutionContext.BackupDigestPool;
				}
				
				digest = returnPool.GetObject();
				return HashEntry(hashCode, digest, key, buffer, xmssExecutionContext);
			} finally {
				returnPool?.PutObject(digest);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray HashEntry(HashCodes hashCode, IHashDigest digest, ByteArray key, ByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			ByteArray hash = null;

			if(digest is ShaDigestBase digestBase) {

				using ByteArray index = ToBytes((int) hashCode, digest.GetDigestSize());

				// since we know the final size, lets preset the size of the buffer
				digestBase.ResetFixed(buffer.Length + key.Length + index.Length);

				digest.BlockUpdate(index.Bytes, index.Offset, index.Length);
				digest.BlockUpdate(key.Bytes, key.Offset, key.Length);
				digest.BlockUpdate(buffer.Bytes, buffer.Offset, buffer.Length);

				digestBase.DoFinalReturn(out hash);
			}
			else 
				throw new ArgumentException(nameof(digest));
			
			return hash;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray Hash(ByteArray buffer, XMSSExecutionContext xmssExecutionContext) {

			IHashDigest digest = xmssExecutionContext.DigestPool.GetObject();
			ByteArray hash = null;

			if(digest is ShaDigestBase digestBase) {
				// soince we know the final size, lets preset the size of the buffer
				digestBase.ResetFixed(buffer.Length);

				digest.BlockUpdate(buffer.Bytes, buffer.Offset, buffer.Length);

				digestBase.DoFinalReturn(out hash);
			}
			else 
				throw new ArgumentException(nameof(digest));

			xmssExecutionContext.DigestPool.PutObject(digest);

			return hash;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteArray Hash(ByteArray buffer, ByteArray buffer2, XMSSExecutionContext xmssExecutionContext) {

			IHashDigest digest = xmssExecutionContext.DigestPool.GetObject();
			ByteArray hash = null;

			if(digest is ShaDigestBase digestBase) {
				// soince we know the final size, lets preset the size of the buffer
				digestBase.ResetFixed(buffer.Length + buffer2.Length);

				digest.BlockUpdate(buffer.Bytes, buffer.Offset, buffer.Length);
				digest.BlockUpdate(buffer2.Bytes, buffer2.Offset, buffer2.Length);

				digestBase.DoFinalReturn(out hash);
			}
			else 
				throw new ArgumentException(nameof(digest));

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

			var resSpan = result.Span;
			int length = Math.Min(sizeof(long), result.Length);
			resSpan.Slice(0, length).Clear();

			Span<byte> buffer = stackalloc byte[sizeof(long)];
			buffer[7] = (byte) (value & 0xFF);
			buffer[6] = (byte) ((value >> (1 * 8)) & 0xFF);
			buffer[5] = (byte) ((value >> (2 * 8)) & 0xFF);
			buffer[4] = (byte) ((value >> (3 * 8)) & 0xFF);
			buffer[3] = (byte) ((value >> (4 * 8)) & 0xFF);
			buffer[2] = (byte) ((value >> (5 * 8)) & 0xFF);
			buffer[1] = (byte) ((value >> (6 * 8)) & 0xFF);
			buffer[0] = (byte) ((value >> (7 * 8)) & 0xFF);

			buffer.Slice(sizeof(long) - length, length).CopyTo(resSpan);

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
		public static (ByteArray publicSeed, ByteArray secretSeed, ByteArray secretSeedPrf) GenerateSeeds(int? seedSize, XMSSExecutionContext xmssExecutionContext) {

			// it is VERY important
			ByteArray[] pool = new ByteArray[20];

			if(!seedSize.HasValue) {
				seedSize = xmssExecutionContext.DigestSize;
			}

			for(int i = 0; i < pool.Length; i++) {

				ByteArray buffer = ByteArray.Create(seedSize.Value);
				GlobalRandom.GetNextBytes(buffer.Bytes, buffer.Offset, buffer.Length);
				pool[i] = buffer;
			}

			List<ByteArray> entries = pool.Shuffle().ToList();
			
			ByteArray secretSeedPrf = entries[0].Clone();
			ByteArray secretSeed = entries[1].Clone();

			DoubleArrayHelper.Return(pool);
			
			for(int i = 0; i < pool.Length; i++) {

				ByteArray buffer = ByteArray.Create(xmssExecutionContext.DigestSize);
				GlobalRandom.GetNextBytes(buffer.Bytes, buffer.Offset, buffer.Length);
				pool[i] = buffer;
			}

			entries = pool.Shuffle().ToList();
			
			ByteArray publicSeed = entries[0].Clone();

			return (publicSeed, secretSeed, secretSeedPrf);
		}
	}
}