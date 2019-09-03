using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Addresses;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;
using Org.BouncyCastle.Crypto;

#if CONCURRENCY_ANALYSER
#endif

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS {
	/// <summary>
	///     THE WOTS+ class
	/// </summary>
	/// <remarks>this was built according to the XMSS RFC https://tools.ietf.org/html/rfc8391</remarks>
	public class WotsPlusEngine {

		private readonly int digestLength;
		private readonly int logWinternitz;

		private readonly IByteArray msgBase;
		private readonly ImmutableArray<int> range;
		private readonly ThreadContext[] threadContexts;
		private readonly int threadCounts;

		private readonly Enums.ThreadMode threadMode;

		private readonly ThreadState threadState = new ThreadState();
		private readonly int winternitz;
		private readonly int winternitz1;

		private readonly XMSSExecutionContext xmssExecutionContext;

		private Action<int> processChainParallel;
		private Action<int> processChainParallelGenerate;

		public WotsPlusEngine(XMSSOperationModes wotsOperationMode, Enums.ThreadMode threadMode, XMSSExecutionContext xmssExecutionContext, WinternitzParameter wParam = WinternitzParameter.Param16) {

			this.winternitz = (int) wParam;
			this.winternitz1 = this.winternitz - 1;
			this.logWinternitz = CommonUtils.Log2(this.winternitz);
			this.xmssExecutionContext = xmssExecutionContext;

			this.digestLength = this.xmssExecutionContext.DigestSize;
			this.Len1 = (int) Math.Ceiling((double) (8 * this.digestLength) / this.logWinternitz);
			this.Len2 = (int) Math.Floor((double) CommonUtils.Log2(this.Len1 * this.winternitz1) / this.logWinternitz) + 1;
			this.Len = this.Len1 + this.Len2;

			this.threadMode = threadMode;

			this.threadCounts = CommonUtils.GetThreadCount(this.threadMode);
			int threadSlicesCount = this.threadCounts;

			this.threadContexts = new ThreadContext[threadSlicesCount];

			for(int i = 0; i < threadSlicesCount; i++) {
				this.threadContexts[i] = new ThreadContext(i, this.Len, threadSlicesCount, this.xmssExecutionContext);
			}

			this.range = Enumerable.Range(0, threadSlicesCount).ToImmutableArray();

			this.msgBase = MemoryAllocators.Instance.cryptoAllocator.Take<int>(this.Len);

		}

		public int Len { get; }

		public int Len1 { get; }

		public int Len2 { get; }

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
		public IByteArray Hash(IByteArray key, IByteArray buffer) {
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

			IByteArray hashEntry = CommonUtils.HashEntry(CommonUtils.HashCodes.F, key, buffer, this.xmssExecutionContext);

			return hashEntry;

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

		private IByteArray GeneratePrivateKey(IByteArray privateSeed, int nonce2, OtsHashAddress adrs) {

			int previousKeyAndMask = adrs.KeyAndMask;

			adrs.KeyAndMask = nonce2;

			IByteArray result = CommonUtils.PRF(privateSeed, adrs, this.xmssExecutionContext);

			adrs.KeyAndMask = previousKeyAndMask;

			return result;
		}

		public MemoryBlockDoubleArray GeneratePseudorandomPrivateKeys(IByteArray secretSeed, int nonce2, OtsHashAddress adrs) {

			MemoryBlockDoubleArray sk = MemoryAllocators.Instance.doubleArrayCryptoAllocator.Take(this.Len);

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

		public MemoryBlockDoubleArray GeneratePublicKeyFromSignature(IByteArray message, MemoryBlockDoubleArray sig, IByteArray publicSeed, OtsHashAddress adrs) {
			var msg = this.msgBase.CastedArray<int>();

			IByteArray baseWArray = this.BaseW(message, this.Len1);
			var baseW = baseWArray.CastedArray<int>();

			baseW.CopyTo(msg);
			baseWArray.Return();

			int checksum = this.winternitz1 * this.Len1;

			for(int i = 0; i < this.Len1; i += 32) {

			#region unroll32

				checksum -= msg[i + 0];
				checksum -= msg[i + 1];
				checksum -= msg[i + 2];
				checksum -= msg[i + 3];
				checksum -= msg[i + 4];
				checksum -= msg[i + 5];
				checksum -= msg[i + 6];
				checksum -= msg[i + 7];
				checksum -= msg[i + 8];
				checksum -= msg[i + 9];
				checksum -= msg[i + 10];
				checksum -= msg[i + 11];
				checksum -= msg[i + 12];
				checksum -= msg[i + 13];
				checksum -= msg[i + 14];
				checksum -= msg[i + 15];
				checksum -= msg[i + 16];
				checksum -= msg[i + 17];
				checksum -= msg[i + 18];
				checksum -= msg[i + 19];
				checksum -= msg[i + 20];
				checksum -= msg[i + 21];
				checksum -= msg[i + 22];
				checksum -= msg[i + 23];
				checksum -= msg[i + 24];
				checksum -= msg[i + 25];
				checksum -= msg[i + 26];
				checksum -= msg[i + 27];
				checksum -= msg[i + 28];
				checksum -= msg[i + 29];
				checksum -= msg[i + 30];
				checksum -= msg[i + 31];

			#endregion

			}

			checksum <<= 8 - ((this.Len2 * this.logWinternitz) % 8);
			int len2Bytes = (int) Math.Ceiling((double) (this.Len2 * this.logWinternitz) / 8);

			IByteArray bytes = CommonUtils.ToBytes(checksum, len2Bytes);
			baseWArray = this.BaseW(bytes, this.Len2);
			bytes.Return();

			baseW = baseWArray.CastedArray<int>();

			baseW.CopyTo(msg.Slice(this.Len1, this.Len2));
			baseWArray.Return();

			MemoryBlockDoubleArray tmpPk = MemoryAllocators.Instance.doubleArrayCryptoAllocator.Take(this.Len);

			this.threadState.publicKey = tmpPk;
			this.threadState.signature = sig;
			this.threadState.publicSeed = publicSeed;
			this.threadState.otsHashAddress = adrs;

			if(this.processChainParallel == null) {
				this.processChainParallel = i => {

					ThreadContext threadContext = this.threadContexts[i];
					int jstart = threadContext.start;

					threadContext.Initialize(this.threadState.otsHashAddress);
					threadContext.Initialize(this.threadState.signature[jstart]);

					for(int j = jstart; j < (jstart + threadContext.count); j++) {
						int msgValue = this.msgBase.ReadCasted<int>(j);
						threadContext.tmpAdrs1.ChainAddress = j;
						threadContext.Initialize2();

						this.threadState.signature[j].CopyTo(threadContext.startHash);
						this.threadState.publicKey[j] = this.ChainParallel(threadContext.startHash, msgValue, this.winternitz1 - msgValue, this.threadState.publicSeed, threadContext);
					}
				};
			}

			Parallel.ForEach(this.range, new ParallelOptions {MaxDegreeOfParallelism = this.threadCounts}, this.processChainParallel);

			this.threadState.publicKey = null;
			this.threadState.signature = null;
			this.threadState.publicSeed = null;
			this.threadState.otsHashAddress = null;

			return tmpPk;
		}

		public MemoryBlockDoubleArray GeneratePublicKey(IByteArray privateSeed, IByteArray publicSeed, int nonce2, XMSSEngine.ThreadContext threadContext) {

			int originalAddress = threadContext.OtsHashAddress.ChainAddress;

			MemoryBlockDoubleArray pk = MemoryAllocators.Instance.doubleArrayCryptoAllocator.Take(this.Len);

			for(int i = 0; i < this.Len; i++) {
				threadContext.OtsHashAddress.ChainAddress = i;

				IByteArray wotsPrivateKey = this.GeneratePrivateKey(privateSeed, nonce2, threadContext.OtsHashAddress);

				// this should not be parallel since it is part of a threadpool higher up
				pk[i] = this.Chain(wotsPrivateKey, 0, this.winternitz1, publicSeed, threadContext);
				wotsPrivateKey.Return();
			}

			// restore the original
			threadContext.OtsHashAddress.ChainAddress = originalAddress;

			return pk;
		}

		public MemoryBlockDoubleArray GenerateSignature(IByteArray message, IByteArray privateSeed, IByteArray publicSeed, int nonce2, OtsHashAddress adrs) {

			var msg = this.msgBase.CastedArray<int>();

			IByteArray baseWArray = this.BaseW(message, this.Len1);
			var baseW = baseWArray.CastedArray<int>();

			baseW.CopyTo(msg);

			baseWArray.Return();

			int checksum = this.winternitz1 * this.Len1;

			for(int i = 0; i < this.Len1; i += 32) {

			#region unroll32

				checksum -= msg[i + 0];
				checksum -= msg[i + 1];
				checksum -= msg[i + 2];
				checksum -= msg[i + 3];
				checksum -= msg[i + 4];
				checksum -= msg[i + 5];
				checksum -= msg[i + 6];
				checksum -= msg[i + 7];
				checksum -= msg[i + 8];
				checksum -= msg[i + 9];
				checksum -= msg[i + 10];
				checksum -= msg[i + 11];
				checksum -= msg[i + 12];
				checksum -= msg[i + 13];
				checksum -= msg[i + 14];
				checksum -= msg[i + 15];
				checksum -= msg[i + 16];
				checksum -= msg[i + 17];
				checksum -= msg[i + 18];
				checksum -= msg[i + 19];
				checksum -= msg[i + 20];
				checksum -= msg[i + 21];
				checksum -= msg[i + 22];
				checksum -= msg[i + 23];
				checksum -= msg[i + 24];
				checksum -= msg[i + 25];
				checksum -= msg[i + 26];
				checksum -= msg[i + 27];
				checksum -= msg[i + 28];
				checksum -= msg[i + 29];
				checksum -= msg[i + 30];
				checksum -= msg[i + 31];

			#endregion

			}

			checksum <<= 8 - ((this.Len2 * this.logWinternitz) % 8);
			int len2Bytes = (int) Math.Ceiling((double) (this.Len2 * this.logWinternitz) / 8);

			IByteArray bytes = CommonUtils.ToBytes(checksum, len2Bytes);
			baseWArray = this.BaseW(bytes, this.Len2);
			bytes.Return();
			baseW = baseWArray.CastedArray<int>();

			baseW.CopyTo(msg.Slice(this.Len1, this.Len2));

			baseWArray.Return();

			MemoryBlockDoubleArray sig = MemoryAllocators.Instance.doubleArrayCryptoAllocator.Take(this.Len);

			this.threadState.signature = sig;
			this.threadState.privateSeed = privateSeed;
			this.threadState.publicSeed = publicSeed;
			this.threadState.nonce2 = nonce2;
			this.threadState.otsHashAddress = adrs;

			if(this.processChainParallelGenerate == null) {
				this.processChainParallelGenerate = i => {

					ThreadContext threadContext = this.threadContexts[i];
					int jstart = threadContext.start;

					for(int j = jstart; j < (jstart + threadContext.count); j++) {
						int msgValue = this.msgBase.ReadCasted<int>(j);

						threadContext.Initialize(this.threadState.otsHashAddress);
						threadContext.tmpAdrs1.ChainAddress = j;

						IByteArray wotsPrivateKey = this.GeneratePrivateKey(this.threadState.privateSeed, this.threadState.nonce2, threadContext.tmpAdrs1);

						threadContext.Initialize(wotsPrivateKey);

						threadContext.Initialize2();

						this.threadState.signature[j] = this.ChainParallel(threadContext.startHash, 0, msgValue, this.threadState.publicSeed, threadContext);
					}
				};
			}

			Parallel.ForEach(this.range, new ParallelOptions {MaxDegreeOfParallelism = this.threadCounts}, this.processChainParallelGenerate);

			this.threadState.signature = null;
			this.threadState.privateSeed = null;
			this.threadState.publicSeed = null;
			this.threadState.nonce2 = 0;
			this.threadState.otsHashAddress = null;

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
		public IByteArray ChainParallel(IByteArray hash, int index, int steps, IByteArray publicSeed, ThreadContext threadContext) {
			if(steps == 0) {
				return hash.Clone();
			}

			IByteArray tmp = hash;

			for(int stepIndex = 0; stepIndex < steps; stepIndex++) {

				threadContext.tmpAdrs2.HashAddress = index + stepIndex;
				threadContext.tmpAdrs2.KeyAndMask = 0;
				IByteArray key = CommonUtils.PRF(publicSeed, threadContext.tmpAdrs2, threadContext);
				threadContext.tmpAdrs2.KeyAndMask = 1;
				IByteArray bitmask = CommonUtils.PRF(publicSeed, threadContext.tmpAdrs2, threadContext);

				CommonUtils.Xor(tmp, tmp, bitmask);

				tmp = CommonUtils.F(key, tmp, threadContext);

				key.Return();
				bitmask.Return();
			}

			return tmp;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IByteArray Chain(IByteArray hash, int index, int steps, IByteArray publicSeed, XMSSEngine.ThreadContext threadContext) {

			if(steps == 0) {

				return hash?.Clone();
			}

			if(hash == null) {
				ThrowException();
			}

			if((index + steps) > this.winternitz1) {
				ThrowException2();
			}

			IByteArray tmp = hash?.Clone();

			int previousHashAddress = threadContext.OtsHashAddress.HashAddress;
			int previousKeyAndMask = threadContext.OtsHashAddress.KeyAndMask;

			for(int stepIndex = 0; stepIndex < steps; stepIndex++) {

				threadContext.OtsHashAddress.HashAddress = index + stepIndex;
				threadContext.OtsHashAddress.KeyAndMask = 0;
				IByteArray key = CommonUtils.PRF(publicSeed, threadContext.OtsHashAddress, this.xmssExecutionContext);
				threadContext.OtsHashAddress.KeyAndMask = 1;
				IByteArray bitmask = CommonUtils.PRF(publicSeed, threadContext.OtsHashAddress, this.xmssExecutionContext);

				CommonUtils.Xor(tmp, tmp, bitmask);

				tmp = this.Hash(key, tmp);

				key.Return();
				bitmask.Return();
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
		public IByteArray BaseW(IByteArray buffer, int outputLen) {

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

			IByteArray baseWarray = MemoryAllocators.Instance.cryptoAllocator.Take<int>(outputLen);
			var baseW = baseWarray.CastedArray<int>();

			for(int i = 0; i < outputLen; i++) {
				if(bits == 0) {
					total = buffer[inVal];
					inVal++;
					bits += 8;
				}

				bits -= this.logWinternitz;
				baseW[outVal] = (int) (total >> bits) & this.winternitz1;
				outVal++;
			}

			return baseWarray;
		}

		private class ThreadState {
			public int nonce2;
			public OtsHashAddress otsHashAddress;

			public IByteArray privateSeed;

			//note: do not return this memory, it is only borrowed
			public MemoryBlockDoubleArray publicKey;
			public IByteArray publicSeed;
			public MemoryBlockDoubleArray signature;
		}

		public class ThreadContext : IDisposable2 {
			public int count;
			public IDigest digest;
			public int index;

			public IByteArray indexBuffer;

			public int start;
			public IByteArray startHash;
			public OtsHashAddress tmpAdrs1;
			public OtsHashAddress tmpAdrs2;
			public XMSSExecutionContext XmssExecutionContext;

			public ThreadContext(int index, int len, int threadCount, XMSSExecutionContext xmssExecutionContext) {
				this.index = index;

				this.count = len / threadCount;

				this.start = index * this.count;

				if(index == (threadCount - 1)) {
					this.count += len - (threadCount * this.count);
				}

				this.XmssExecutionContext = xmssExecutionContext;
				this.tmpAdrs1 = this.XmssExecutionContext.OtsHashAddressPool.GetObject();
				this.tmpAdrs2 = this.XmssExecutionContext.OtsHashAddressPool.GetObject();
				this.startHash = MemoryAllocators.Instance.cryptoAllocator.Take(this.XmssExecutionContext.DigestSize);
				this.indexBuffer = MemoryAllocators.Instance.cryptoAllocator.Take(this.XmssExecutionContext.DigestSize);
				this.digest = xmssExecutionContext.DigestPool.GetObject();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Initialize(OtsHashAddress adrs) {
				this.tmpAdrs1.Initialize(adrs);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Initialize(IByteArray startHash) {

				this.startHash.CopyFrom(startHash);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Initialize2() {
				this.tmpAdrs2.Initialize(this.tmpAdrs1);
			}

		#region disposable

			public bool IsDisposed { get; private set; }

			public void Dispose() {
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected virtual void Dispose(bool disposing) {

				if(disposing && !this.IsDisposed) {

					this.tmpAdrs1.Dispose();
					this.tmpAdrs2.Dispose();

					this.indexBuffer.Dispose();
					this.startHash.Dispose();

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

				this.msgBase?.Dispose();
			}

			this.IsDisposed = true;
		}

		~WotsPlusEngine() {
			this.Dispose(false);
		}

	#endregion

	}
}