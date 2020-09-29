using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.Hash;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Org.BouncyCastle.Crypto;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS {

	/// <summary>
	///     THE WOTS+ class
	/// </summary>
	/// <remarks>this was built according to the XMSS RFC https://tools.ietf.org/html/rfc8391</remarks>
	public unsafe class WotsPlus : IDisposableExtended {

		public const byte WINTERNITZ_PARAMETER = 16;

		private readonly int digestLength;
		private readonly int logWinternitz;

		private readonly ByteArray msg;
		private readonly int* msgPtr;
		private readonly ImmutableArray<int> range;
		private readonly ThreadContext[] threadContexts;
		private readonly int threadCounts;

		private readonly ThreadState threadState = new ThreadState();

		private readonly XMSSExecutionContext xmssExecutionContext;

		private MemoryHandle msgMemoryHandle;

		private Action<int> processChainParallel;
		private Action<int> processChainParallelGenerate;

		public WotsPlus(Enums.ThreadMode threadMode, XMSSExecutionContext xmssExecutionContext) {
			this.WinternitzParameter = WINTERNITZ_PARAMETER;
			this.WinternitzParameterZeroBased = WINTERNITZ_PARAMETER - 1;
			this.logWinternitz = XMSSCommonUtils.Log2(this.WinternitzParameter);
			this.xmssExecutionContext = xmssExecutionContext;

			this.digestLength = this.xmssExecutionContext.DigestSize;
			this.Len1 = (int) Math.Ceiling((double) (8 * this.digestLength) / this.logWinternitz);
			this.Len2 = (int) Math.Floor((double) XMSSCommonUtils.Log2(this.Len1 * this.WinternitzParameterZeroBased) / this.logWinternitz) + 1;
			this.Len = this.Len1 + this.Len2;

			this.threadCounts = XMSSCommonUtils.GetThreadCount(threadMode);
			int threadSlicesCount = this.threadCounts;

			this.threadContexts = new ThreadContext[threadSlicesCount];

			for(int i = 0; i < threadSlicesCount; i++) {
				this.threadContexts[i] = new ThreadContext(i, this.Len, threadSlicesCount, this.xmssExecutionContext);
			}

			this.range = Enumerable.Range(0, threadSlicesCount).ToImmutableArray();

			this.msg = ByteArray.Create<int>(this.Len);

			this.msgMemoryHandle = this.msg.Memory.Pin();
			this.msgPtr = (int*) this.msgMemoryHandle.Pointer;
		}

		public int WinternitzParameter { get; }
		public int WinternitzParameterZeroBased { get; }

		public int Len { get; }

		private int Len1 { get; }

		private int Len2 { get; }

		/// <summary>
		///     F function
		/// </summary>
		/// <param name="key"></param>
		/// <param name="buffer"></param>
		/// <returns></returns>
		/// <exception cref="NullReferenceException"></exception>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="Exception"></exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ByteArray Hash(ByteArray key, ByteArray buffer) {
			if(buffer == null) {
				ThrowException();
			}

			if(key == null) {
				ThrowException1();
			}

			if(buffer.Length != key.Length) {
				ThrowException2(key.Length, buffer.Length);
			}

			if(key.Length != this.digestLength) {
				ThrowException3(key.Length);
			}

			return XMSSCommonUtils.HashEntry(XMSSCommonUtils.HashCodes.F, key, buffer, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);

		#region ThrowExceptions

			void ThrowException() {

				throw new NullReferenceException("buffer should not be null");
			}

			void ThrowException1() {

				throw new NullReferenceException("key should not be null");
			}

			void ThrowException2(int keyLength, int bufferLength) {

				throw new ArgumentException($"buffer length {bufferLength} should be equal to Key length {keyLength}");
			}

			void ThrowException3(int keyLength) {

				throw new ArgumentException($"key length {keyLength} should be equal to WOTSPlusProvider length capacity {this.digestLength}");
			}

		#endregion

		}

		private ByteArray GeneratePrivateKey(ByteArray privateSeed, int nonce2, OtsHashAddress adrs) {

			int previousKeyAndMask = adrs.KeyAndMask;

			adrs.KeyAndMask = nonce2;

			ByteArray result = XMSSCommonUtils.PRF(privateSeed, adrs, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);

			adrs.KeyAndMask = previousKeyAndMask;

			return result;
		}

		public ByteArray[] GeneratePseudorandomPrivateKeys(ByteArray secretSeed, int nonce2, OtsHashAddress adrs) {

			ByteArray[] sk = new ByteArray[this.Len];

			OtsHashAddress tmpAdrs = this.xmssExecutionContext.OtsHashAddressPool.GetObject();
			tmpAdrs.Initialize(adrs);

			Parallel.For(0, this.Len, i => {
				tmpAdrs.HashAddress = i;

				// generation using pseudorandom
				sk[i] = this.GeneratePrivateKey(secretSeed, nonce2, tmpAdrs);
			});

			this.xmssExecutionContext.OtsHashAddressPool.PutObject(tmpAdrs);

			return sk;
		}

		public ByteArray[] GeneratePublicKeyFromSignature(ByteArray message, ByteArray[] sig, ByteArray publicSeed, OtsHashAddress adrs) {

			using(ByteArray wArray = this.BaseW(message, this.Len1)) {
				wArray.CopyTo(this.msg);
			}

			int checksum = this.WinternitzParameterZeroBased * this.Len1;

			for(int i = 0; i < this.Len1; i += 32) {

			#region unroll32

				checksum -= *(this.msgPtr + (i + 0));
				checksum -= *(this.msgPtr + (i + 1));
				checksum -= *(this.msgPtr + (i + 2));
				checksum -= *(this.msgPtr + (i + 3));
				checksum -= *(this.msgPtr + (i + 4));
				checksum -= *(this.msgPtr + (i + 5));
				checksum -= *(this.msgPtr + (i + 6));
				checksum -= *(this.msgPtr + (i + 7));
				checksum -= *(this.msgPtr + (i + 8));
				checksum -= *(this.msgPtr + (i + 9));
				checksum -= *(this.msgPtr + (i + 10));
				checksum -= *(this.msgPtr + (i + 11));
				checksum -= *(this.msgPtr + (i + 12));
				checksum -= *(this.msgPtr + (i + 13));
				checksum -= *(this.msgPtr + (i + 14));
				checksum -= *(this.msgPtr + (i + 15));
				checksum -= *(this.msgPtr + (i + 16));
				checksum -= *(this.msgPtr + (i + 17));
				checksum -= *(this.msgPtr + (i + 18));
				checksum -= *(this.msgPtr + (i + 19));
				checksum -= *(this.msgPtr + (i + 20));
				checksum -= *(this.msgPtr + (i + 21));
				checksum -= *(this.msgPtr + (i + 22));
				checksum -= *(this.msgPtr + (i + 23));
				checksum -= *(this.msgPtr + (i + 24));
				checksum -= *(this.msgPtr + (i + 25));
				checksum -= *(this.msgPtr + (i + 26));
				checksum -= *(this.msgPtr + (i + 27));
				checksum -= *(this.msgPtr + (i + 28));
				checksum -= *(this.msgPtr + (i + 29));
				checksum -= *(this.msgPtr + (i + 30));
				checksum -= *(this.msgPtr + (i + 31));

			#endregion

			}

			checksum <<= 8 - ((this.Len2 * this.logWinternitz) % 8);
			int len2Bytes = (int) Math.Ceiling((double) (this.Len2 * this.logWinternitz) / 8);

			using(ByteArray bytes = XMSSCommonUtils.ToBytes(checksum, len2Bytes)) {
				using(ByteArray wArray = this.BaseW(bytes, this.Len2)) {
					wArray.CopyTo(this.msg.Span.Slice(this.Len1 * sizeof(int), this.Len2 * sizeof(int)));
				}
			}

			ByteArray[] tmpPk = new ByteArray[this.Len];

			this.threadState.PublicKey = tmpPk;
			this.threadState.Signature = sig;
			this.threadState.PublicSeed = publicSeed;
			this.threadState.OtsHashAddress = adrs;

			if(this.processChainParallel == null) {
				this.processChainParallel = i => {

					ThreadContext threadContext = this.threadContexts[i];
					int jstart = threadContext.Start;

					threadContext.Initialize(this.threadState.OtsHashAddress);
					threadContext.Initialize(this.threadState.Signature[jstart]);

					for(int j = jstart; j < (jstart + threadContext.Count); j++) {
						int msgValue = *(this.msgPtr + j);
						threadContext.TmpAdrs1.ChainAddress = j;
						threadContext.Initialize2();

						this.threadState.Signature[j].CopyTo(threadContext.StartHash);
						this.threadState.PublicKey[j] = this.ChainParallel(threadContext.StartHash, msgValue, this.WinternitzParameterZeroBased - msgValue, this.threadState.PublicSeed, threadContext);
					}
				};
			}

			Parallel.ForEach(this.range, new ParallelOptions {MaxDegreeOfParallelism = this.threadCounts}, this.processChainParallel);

			this.threadState.PublicKey = null;
			this.threadState.Signature = null;
			this.threadState.PublicSeed = null;
			this.threadState.OtsHashAddress = null;

			return tmpPk;
		}

		public ByteArray[] GeneratePublicKey(ByteArray privateSeed, ByteArray publicSeed, int nonce2, XMSSEngine.ThreadContext threadContext) {

			int originalAddress = threadContext.OtsHashAddress.ChainAddress;

			ByteArray[] pk = new ByteArray[this.Len];

			for(int i = 0; i < this.Len; i++) {
				threadContext.OtsHashAddress.ChainAddress = i;

				using ByteArray wotsPrivateKey = this.GeneratePrivateKey(privateSeed, nonce2, threadContext.OtsHashAddress);

				// this should not be parallel since it is part of a threadpool higher up
				pk[i] = this.Chain(wotsPrivateKey, 0, this.WinternitzParameterZeroBased, publicSeed, threadContext);
			}

			// restore the original
			threadContext.OtsHashAddress.ChainAddress = originalAddress;

			return pk;
		}

		public ByteArray[] GenerateSignature(ByteArray message, ByteArray privateSeed, ByteArray publicSeed, int nonce2, OtsHashAddress adrs) {

			using(ByteArray wArray = this.BaseW(message, this.Len1)) {
				wArray.CopyTo(this.msg);
			}

			int checksum = this.WinternitzParameterZeroBased * this.Len1;

			for(int i = 0; i < this.Len1; i += 32) {

			#region unroll32

				checksum -= *(this.msgPtr + (i + 0));
				checksum -= *(this.msgPtr + (i + 1));
				checksum -= *(this.msgPtr + (i + 2));
				checksum -= *(this.msgPtr + (i + 3));
				checksum -= *(this.msgPtr + (i + 4));
				checksum -= *(this.msgPtr + (i + 5));
				checksum -= *(this.msgPtr + (i + 6));
				checksum -= *(this.msgPtr + (i + 7));
				checksum -= *(this.msgPtr + (i + 8));
				checksum -= *(this.msgPtr + (i + 9));
				checksum -= *(this.msgPtr + (i + 10));
				checksum -= *(this.msgPtr + (i + 11));
				checksum -= *(this.msgPtr + (i + 12));
				checksum -= *(this.msgPtr + (i + 13));
				checksum -= *(this.msgPtr + (i + 14));
				checksum -= *(this.msgPtr + (i + 15));
				checksum -= *(this.msgPtr + (i + 16));
				checksum -= *(this.msgPtr + (i + 17));
				checksum -= *(this.msgPtr + (i + 18));
				checksum -= *(this.msgPtr + (i + 19));
				checksum -= *(this.msgPtr + (i + 20));
				checksum -= *(this.msgPtr + (i + 21));
				checksum -= *(this.msgPtr + (i + 22));
				checksum -= *(this.msgPtr + (i + 23));
				checksum -= *(this.msgPtr + (i + 24));
				checksum -= *(this.msgPtr + (i + 25));
				checksum -= *(this.msgPtr + (i + 26));
				checksum -= *(this.msgPtr + (i + 27));
				checksum -= *(this.msgPtr + (i + 28));
				checksum -= *(this.msgPtr + (i + 29));
				checksum -= *(this.msgPtr + (i + 30));
				checksum -= *(this.msgPtr + (i + 31));

			#endregion

			}

			checksum <<= 8 - ((this.Len2 * this.logWinternitz) % 8);
			int len2Bytes = (int) Math.Ceiling((double) (this.Len2 * this.logWinternitz) / 8);

			using(ByteArray bytes = XMSSCommonUtils.ToBytes(checksum, len2Bytes)) {
				using(ByteArray wArray = this.BaseW(bytes, this.Len2)) {
					wArray.CopyTo(this.msg.Span.Slice(this.Len1 * sizeof(int), this.Len2 * sizeof(int)));
				}
			}

			ByteArray[] sig = new ByteArray[this.Len];

			this.threadState.Signature = sig;
			this.threadState.PrivateSeed = privateSeed;
			this.threadState.PublicSeed = publicSeed;
			this.threadState.Nonce2 = nonce2;
			this.threadState.OtsHashAddress = adrs;

			if(this.processChainParallelGenerate == null) {
				this.processChainParallelGenerate = i => {

					ThreadContext threadContext = this.threadContexts[i];
					int jstart = threadContext.Start;

					for(int j = jstart; j < (jstart + threadContext.Count); j++) {
						int msgValue = *(this.msgPtr + j);

						threadContext.Initialize(this.threadState.OtsHashAddress);
						threadContext.TmpAdrs1.ChainAddress = j;

						ByteArray wotsPrivateKey = this.GeneratePrivateKey(this.threadState.PrivateSeed, this.threadState.Nonce2, threadContext.TmpAdrs1);

						threadContext.Initialize(wotsPrivateKey);

						threadContext.Initialize2();

						this.threadState.Signature[j] = this.ChainParallel(threadContext.StartHash, 0, msgValue, this.threadState.PublicSeed, threadContext);
					}
				};
			}

			Parallel.ForEach(this.range, new ParallelOptions {MaxDegreeOfParallelism = this.threadCounts}, this.processChainParallelGenerate);

			this.threadState.Signature = null;
			this.threadState.PrivateSeed = null;
			this.threadState.PublicSeed = null;
			this.threadState.Nonce2 = 0;
			this.threadState.OtsHashAddress = null;

			return sig;
		}

		/// <summary>
		///     A special implementation for very fast multi threading
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="index"></param>
		/// <param name="steps"></param>
		/// <param name="publicSeed"></param>
		/// <param name="threadContext"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ByteArray ChainParallel(ByteArray hash, int index, int steps, ByteArray publicSeed, ThreadContext threadContext) {

			ByteArray tmp = hash.Clone();

			if(steps == 0) {
				return tmp;
			}

			for(int stepIndex = 0; stepIndex < steps; stepIndex++) {

				threadContext.TmpAdrs2.HashAddress = index + stepIndex;
				threadContext.TmpAdrs2.KeyAndMask = 0;
				ByteArray key = XMSSCommonUtils.PRF(publicSeed, threadContext.TmpAdrs2, threadContext);
				threadContext.TmpAdrs2.KeyAndMask = 1;
				ByteArray bitmask = XMSSCommonUtils.PRF(publicSeed, threadContext.TmpAdrs2, threadContext);

				XMSSCommonUtils.Xor(tmp, tmp, bitmask);

				ByteArray prevTmp = tmp;
				tmp = XMSSCommonUtils.F(key, tmp, threadContext);

				prevTmp.Return();
				key.Return();
				bitmask.Return();
			}

			return tmp;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ByteArray Chain(ByteArray hash, int index, int steps, ByteArray publicSeed, XMSSEngine.ThreadContext threadContext) {

			if(steps == 0) {

				return hash?.Clone();
			}

			if(hash == null) {
				ThrowException();
			}

			if((index + steps) > this.WinternitzParameterZeroBased) {
				ThrowException2();
			}

			ByteArray tmp = hash?.Clone();

			int previousHashAddress = threadContext.OtsHashAddress.HashAddress;
			int previousKeyAndMask = threadContext.OtsHashAddress.KeyAndMask;
			for(int stepIndex = 0; stepIndex < steps; stepIndex++) {

				threadContext.OtsHashAddress.HashAddress = index + stepIndex;
				threadContext.OtsHashAddress.KeyAndMask = 0;
				using ByteArray key = XMSSCommonUtils.PRF(publicSeed, threadContext.OtsHashAddress, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);
				threadContext.OtsHashAddress.KeyAndMask = 1;
				using ByteArray bitmask = XMSSCommonUtils.PRF(publicSeed, threadContext.OtsHashAddress, this.xmssExecutionContext, XMSSCommonUtils.HashTypes.Regular);

				XMSSCommonUtils.Xor(tmp, tmp, bitmask);
				
				using ByteArray prevTmp = tmp;
				tmp = this.Hash(key, tmp);
			}

			threadContext.OtsHashAddress.HashAddress = previousHashAddress;
			threadContext.OtsHashAddress.KeyAndMask = previousKeyAndMask;

			return tmp;

			//This is required for inlining
			void ThrowException() {

				throw new NullReferenceException("hash cannot be null");
			}

			void ThrowException2() {

				throw new ArgumentException("The maximum chain length cannot be greater than (w-1)");
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ByteArray BaseW(ByteArray buffer, int outputLen) {

			int maxOutputLen = (buffer.Length << 3) / this.logWinternitz;

			void ThrowLenTooBig() {
				throw new Exception($"Output Length {outputLen} is too big. It should be less than or equal to {maxOutputLen}.");
			}

			if(outputLen > maxOutputLen) {

				ThrowLenTooBig();
			}

			int inVal = 0;
			int outVal = 0;
			uint total = 0;
			int bits = 0;
			int len = buffer.Length;

			ByteArray warray = ByteArray.Create<int>(outputLen);

			fixed(byte* baseWb = warray.Span) {

				int* baseW = (int*) baseWb;

				for(int i = 0; i < outputLen; i++) {
					if(bits == 0) {
						total = buffer[inVal];
						inVal++;
						bits += 8;
					}

					bits -= this.logWinternitz;
					*(baseW + outVal) = (int) (total >> bits) & this.WinternitzParameterZeroBased;
					outVal++;
				}
			}

			return warray;
		}

		private class ThreadState {
			public int Nonce2;
			public OtsHashAddress OtsHashAddress;

			public ByteArray PrivateSeed;

			//note: do not return this memory, it is only borrowed
			public ByteArray[] PublicKey;
			public ByteArray PublicSeed;
			public ByteArray[] Signature;
		}

		public class ThreadContext : IDisposableExtended {
			public readonly int Count;
			public readonly IHashDigest Digest;

			private readonly ByteArray IndexBuffer;

			public readonly int Start;
			public readonly ByteArray StartHash;
			public readonly OtsHashAddress TmpAdrs1;
			public readonly OtsHashAddress TmpAdrs2;
			private int Index;

			public ThreadContext(int index, int len, int threadCount, XMSSExecutionContext xmssExecutionContext) {
				this.Index = index;

				this.Count = len / threadCount;

				this.Start = index * this.Count;

				if(index == (threadCount - 1)) {
					this.Count += len - (threadCount * this.Count);
				}

				this.XmssExecutionContext = xmssExecutionContext;
				this.TmpAdrs1 = this.XmssExecutionContext.OtsHashAddressPool.GetObject();
				this.TmpAdrs2 = this.XmssExecutionContext.OtsHashAddressPool.GetObject();
				this.StartHash = ByteArray.Create(this.XmssExecutionContext.DigestSize);
				this.IndexBuffer = ByteArray.Create(this.XmssExecutionContext.DigestSize);
				this.Digest = xmssExecutionContext.DigestPool.GetObject();
			}

			public XMSSExecutionContext XmssExecutionContext { get; }

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Initialize(OtsHashAddress adrs) {
				this.TmpAdrs1.Initialize(adrs);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Initialize(ByteArray startHash) {

				this.StartHash.CopyFrom(startHash);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Initialize2() {
				this.TmpAdrs2.Initialize(this.TmpAdrs1);
			}

		#region disposable

			public bool IsDisposed { get; private set; }

			public void Dispose() {
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected virtual void Dispose(bool disposing) {

				if(disposing && !this.IsDisposed) {

					this.XmssExecutionContext.OtsHashAddressPool.PutObject(this.TmpAdrs1);
					this.XmssExecutionContext.OtsHashAddressPool.PutObject(this.TmpAdrs2);
					this.XmssExecutionContext.DigestPool.PutObject(this.Digest);
					
					this.IndexBuffer.Dispose();
					this.StartHash.Dispose();

				}

				this.IsDisposed = true;
			}

			~ThreadContext() {
				this.Dispose(false);
			}

		#endregion

		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				foreach(ThreadContext entry in this.threadContexts) {
					entry.Dispose();
				}

				try {
					this.msgMemoryHandle.Dispose();
				} catch {
				}

				try {
					this.msg.Dispose();
				} catch {
				}
			}

			this.IsDisposed = true;
		}

		~WotsPlus() {
			this.Dispose(false);
		}

	#endregion

	}
}