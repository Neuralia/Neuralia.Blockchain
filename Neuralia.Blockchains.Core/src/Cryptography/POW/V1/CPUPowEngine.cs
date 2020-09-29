using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Core.Cryptography.POW.V1.Crypto;
using Neuralia.Blockchains.Core.Cryptography.POW.V1.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays.Large;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Neuralia.Blockchains.Core.Cryptography.POW.V1 {
	public class CPUPowEngine {

		private const int HASH_512_BYTES = 64;

		private const long CHUNK_SIZE = 1L << 6; //2^6 = 64 bytes

		private readonly int CacheMemorySize; //2^12 = 4096 bytes

		private readonly int ComparisionSize; //2^(30-12) = 256K
		
		private readonly int FillerHashIterations = 10;
		private readonly int CryptoIterations = 15;

		private readonly bool enableMacroDiagnostics;
		private readonly bool enableMicroDiagnostics;

		private readonly int L2CacheTarget = 16; // 2^12 = 64k // must be less than MAIN_BUFFER_DATA_SIZE. no smaller than 6

		private readonly int MainBufferDataSize = 31; //2^30 = 1GB. 2^20 = 1MB

		private readonly long MainBufferDataSizeX2;
		private readonly POWCryptoSet powCryptoSet;

		private readonly POWHashSet powHashSet;
		private readonly CPUPOWRulesSet rulesSet;
		private readonly bool memoryMapped;
		private bool firstRun = true;
		private readonly long hashTargetDifficulty = HashDifficultyUtils.Default512Difficulty;
		
		public CPUPowEngine(CPUPOWRulesSet rulesSet, bool memoryMapped = false, bool enableMacroDiagnostics = false, bool enableMicroDiagnostics = false) {
			this.enableMacroDiagnostics = enableMacroDiagnostics;
			this.enableMicroDiagnostics = enableMicroDiagnostics;
			this.memoryMapped = memoryMapped;

			this.rulesSet = rulesSet;
			this.powHashSet = rulesSet.GenerateHashSet();
			this.powCryptoSet = rulesSet.GenerateCryptoSet();

			this.MainBufferDataSize = this.rulesSet.MainBufferDataSize; //2^30 = 1GB. 2^20 = 1MB
			this.MainBufferDataSizeX2 = 1L << this.MainBufferDataSize; //2^30 = 1GB
			this.L2CacheTarget = this.rulesSet.L2CacheTarget;
			this.ComparisionSize = 1 << (this.MainBufferDataSize - this.L2CacheTarget); //2^(30-16) = 256K;
			this.CryptoIterations = this.rulesSet.CryptoIterations;
			this.CacheMemorySize = 1 << this.L2CacheTarget; //2^12 = 4096 bytes
			this.FillerHashIterations = this.rulesSet.FillerHashIterations;
			this.hashTargetDifficulty = this.rulesSet.HashTargetDifficulty;
		}

		public async Task<(int solution, long nonce)> PerformPow(SafeArrayHandle root, Func<long, long, Task> iteration = null, long startingNonce = 1) {

			this.powHashSet.Reset();
			this.powCryptoSet.Reset();

			using SafeArrayHandle rootHash = this.powHashSet.Hash(root);

			BigInteger hash = HashDifficultyUtils.GetBigInteger(rootHash);

			BigInteger hashTarget = HashDifficultyUtils.GetHash512TargetByIncrementalDifficulty(this.hashTargetDifficulty);
			NLog.Default.Debug("Difficulty: {0}", this.hashTargetDifficulty);
			NLog.Default.Debug("target: {0}", hashTarget);

			IVeryLargeByteArray scratchpad = null;

			try {
				if(this.memoryMapped) {
					scratchpad = new FileMappedLargeByteArray(this.MainBufferDataSizeX2, true);
				} else {
					scratchpad = new VeryLargeByteArray(this.MainBufferDataSizeX2);
				}

				long nonce = startingNonce;

				var solution = 0;

				Stopwatch outerStopwatch = null;
				Stopwatch innerStopwatch = null;

				if(this.enableMacroDiagnostics) {
					outerStopwatch = new Stopwatch();
					outerStopwatch.Start();
				}

				if(this.enableMicroDiagnostics) {
					innerStopwatch = new Stopwatch();
				}

				using var hasher = new xxHasher32();

				while(true) {

					this.powHashSet.Reset();
					this.powCryptoSet.Reset();

					NLog.Default.Debug("Performing proof of work for Nonce {0}", nonce);

					if(this.enableMicroDiagnostics) {
						innerStopwatch?.Reset();
						innerStopwatch?.Start();
					}

					// alert we are running an iteration
					if(iteration != null) {
						await iteration(nonce, hashTargetDifficulty).ConfigureAwait(false);
					}

					BigInteger result = await this.FindBestPatternHash(hash, scratchpad, nonce).ConfigureAwait(false);

					if(this.enableMicroDiagnostics) {
						innerStopwatch?.Stop();
						NLog.Default.Debug("Single nonce search took {0}", innerStopwatch?.Elapsed);
					}

					if(result < hashTarget) {
						NLog.Default.Debug("Found pre hash result: {0}", result);
						solution = hasher.Hash(result.ToByteArray());

						break;
					}

					nonce++;
				}

				if(this.enableMacroDiagnostics) {
					outerStopwatch.Stop();
					NLog.Default.Debug("Entire proof of work took {0}", outerStopwatch.Elapsed);
				}

				NLog.Default.Debug("Found a solution with value {0}!", solution);

				return (solution, nonce);
			} finally {
				scratchpad?.Dispose();
			}
		}

		/// <summary>
		///     here we verify the POW by running it for the passed parameters
		/// </summary>
		/// <param name="nonce"></param>
		/// <param name="hashTargetDifficulty"></param>
		/// <param name="solutions"></param>
		/// <returns></returns>
		public async Task<bool> Verify(SafeArrayHandle root, long nonce, int solution) {

			this.powHashSet.Reset();
			this.powCryptoSet.Reset();

			using SafeArrayHandle rootHash = this.powHashSet.Hash(root);

			BigInteger hashTarget = HashDifficultyUtils.GetHash512TargetByIncrementalDifficulty(this.hashTargetDifficulty);
			NLog.Default.Debug("target: {0}", hashTarget);
			NLog.Default.Debug("Difficulty: {0}", this.hashTargetDifficulty);

			IVeryLargeByteArray scratchpad = null;

			try {
				if(this.memoryMapped) {
					scratchpad = new FileMappedLargeByteArray(this.MainBufferDataSizeX2, true);
				} else {
					scratchpad = new VeryLargeByteArray(this.MainBufferDataSizeX2);
				}

				var currentSolution = 0;

				NLog.Default.Debug("Performing proof of work verification for Nonce {0}", nonce);

				this.powHashSet.Reset();
				this.powCryptoSet.Reset();

				BigInteger hash = HashDifficultyUtils.GetBigInteger(rootHash);
				BigInteger result = await this.FindBestPatternHash(hash, scratchpad, nonce).ConfigureAwait(false);

				if(result < hashTarget) {
					using var hasher = new xxHasher32();
					currentSolution = hasher.Hash(result.ToByteArray());
				}

				// lets return if they match
				return solution == currentSolution;
			} finally {
				scratchpad?.Dispose();
			}
		}

		private async Task<BigInteger> FindBestPatternHash(BigInteger hash, IVeryLargeByteArray scratchpad, long nonce) {

			// hash the transaction header
			using SafeArrayHandle dataHash = this.GetNoncedHash(hash, nonce);
			NLog.Default.Debug("current hash {0}", dataHash.Entry.ToBase58());

			using SafeArrayHandle searchResults = await this.pattern_search(nonce, dataHash, scratchpad).ConfigureAwait(false);

			return HashDifficultyUtils.GetBigInteger(searchResults);
		}

		/// <summary>
		///     run a pattern search, which is designed to run on a single CPU core.
		/// </summary>
		/// <param name="nonce"></param>
		/// <param name="hash"></param>
		/// <param name="scratchpad"></param>
		/// <returns></returns>
		private async Task<SafeArrayHandle> pattern_search(long nonce, SafeArrayHandle hash, IVeryLargeByteArray scratchpad) {

			if(!this.firstRun) {
				await scratchpad.Clear().ConfigureAwait(false);
			}

			this.firstRun = false;

			using var hashWorkSpace = ByteArray.Create(HASH_512_BYTES * 3);

			using var buffer = SafeArrayHandle.Create(sizeof(long));

			TypeSerializer.Serialize(nonce, buffer.Span);
			using SafeArrayHandle nonceHash = this.powHashSet.Hash(buffer);

			hashWorkSpace.CopyFrom(hash.Entry);
			hashWorkSpace.CopyFrom(nonceHash.Entry, HASH_512_BYTES);

			Console.WriteLine("Sha512 filler");

			// prepare tasks
			await this.Sha512Filler(nonce, scratchpad, hashWorkSpace).ConfigureAwait(false);
			Console.WriteLine("Crypto");

			return await this.CryptoSearch(scratchpad).ConfigureAwait(false);
		}

		private async Task<SafeArrayHandle> CryptoSearch(IVeryLargeByteArray scratchpad) {

			using var cache = SafeArrayHandle.Create(this.CacheMemorySize);
			Memory<byte> cacheSpan = cache.Memory;

			using var data = SafeArrayHandle.Create(sizeof(uint) * 4);
			Memory<byte> dataSpan = data.Memory;

			using var buffer = SafeArrayHandle.Create(HASH_512_BYTES * 3);

			var result = SafeArrayHandle.Create(HASH_512_BYTES);

			int cacheChunks = this.CacheMemorySize / sizeof(ulong);

			// copy the last hash of all
			await scratchpad.CopyTo(buffer.Entry, scratchpad.Length - HASH_512_BYTES, HASH_512_BYTES * 2, HASH_512_BYTES).ConfigureAwait(false);

			for(long k = 0; k < this.ComparisionSize; k++) {

				long kOffset = k * this.CacheMemorySize;

				// take the cached slice
				await scratchpad.CopyTo(cache.Entry, kOffset, 0, cache.Length).ConfigureAwait(false);

				// prevent parallelization. lets add from the previous
				int hashOffset = this.CacheMemorySize - HASH_512_BYTES;

				// and the current one
				cache.Entry.CopyTo(buffer.Entry, hashOffset, 0, HASH_512_BYTES);

				if(k != 0) {
					// and the last one
					await scratchpad.CopyTo(buffer.Entry, (kOffset - this.CacheMemorySize) + hashOffset, HASH_512_BYTES, HASH_512_BYTES).ConfigureAwait(false);
				}

				using SafeArrayHandle hash = this.powHashSet.Hash(buffer);

				cache.Entry.CopyFrom(hash.Entry, 0, hashOffset, hash.Length);

				for(var j = 0; j < this.CryptoIterations; j++) {

					// perform some scrambling
					await scratchpad.CopyTo(data.Entry, (kOffset + this.CacheMemorySize) - ((j + 1) * data.Length), 0, data.Length).ConfigureAwait(false);

					// build a pseudorandom location to pick data
					TypeSerializer.Deserialize(dataSpan.Slice(0, sizeof(uint)), out uint cache32);
					long nextLocationStart = cache32 % this.ComparisionSize;
					TypeSerializer.Deserialize(dataSpan.Slice(sizeof(uint), sizeof(uint)), out cache32);
					long nextLocationMiddle = cache32 % this.ComparisionSize;
					TypeSerializer.Deserialize(dataSpan.Slice(sizeof(uint) * 2, sizeof(uint)), out cache32);
					long nextLocationEnd = cache32 % this.ComparisionSize;

					// and the middle random
					TypeSerializer.Deserialize(dataSpan.Slice(sizeof(uint) * 3, sizeof(uint)), out cache32);
					var middleLocation = (int) (cache32 % cacheChunks);

					async Task Scramble(long nextLocation, int chunk, Memory<byte> bufferSpan, Memory<byte> cacheBufferSpan) {

						int offset = chunk * sizeof(ulong);
						await scratchpad.CopyTo(data.Entry, (nextLocation * this.CacheMemorySize) + offset, 0, sizeof(ulong)).ConfigureAwait(false);
						TypeSerializer.Deserialize(bufferSpan, out ulong cache64A);

						cacheBufferSpan.Slice(offset, sizeof(ulong)).CopyTo(bufferSpan);
						TypeSerializer.Deserialize(bufferSpan, out ulong cache64B);

						cache64B ^= cache64A;
						TypeSerializer.Serialize(cache64B, bufferSpan);
						bufferSpan.Slice(0, sizeof(ulong)).CopyTo(cacheBufferSpan.Slice(offset, sizeof(ulong)));
					}

					// beginning
					await Scramble(nextLocationStart, 0, dataSpan, cacheSpan).ConfigureAwait(false);

					// middle
					await Scramble(nextLocationMiddle, middleLocation, dataSpan, cacheSpan).ConfigureAwait(false);

					// end
					await Scramble(nextLocationEnd, cacheChunks - 1, dataSpan, cacheSpan).ConfigureAwait(false);
					
					this.powCryptoSet.EncryptStringToBytes(cache, cache);
				}

				result.Entry.CopyTo(cache.Entry, 0, cache.Length - HASH_512_BYTES, HASH_512_BYTES);
				result.Dispose();
				result = this.powHashSet.Hash512(cache);
			}

			return result;
		}

		private async Task Sha512Filler(long nonce, IVeryLargeByteArray scratchpad, ByteArray hashWorkSpace) {

			using var cache = SafeArrayHandle.Create(this.CacheMemorySize);
			Memory<byte> cacheSpan = cache.Memory;
			
			using var buffer = SafeArrayHandle.Create(HASH_512_BYTES);
			Memory<byte> bufferSpan = buffer.Memory;
			using var localHashWorkSpace = SafeArrayHandle.Create(hashWorkSpace.Length);
			localHashWorkSpace.Entry.CopyFrom(hashWorkSpace);

			TypeSerializer.Serialize(nonce, bufferSpan);

			int chunks = this.CacheMemorySize / this.FillerHashIterations;
			for(long k = 0; k < this.ComparisionSize; k++) {

				TypeSerializer.Serialize(k, bufferSpan.Slice(sizeof(long), sizeof(long)));

				if(k != 0) {
					await scratchpad.CopyTo(buffer.Entry, (k - 1) * CHUNK_SIZE, sizeof(long) + sizeof(long), sizeof(long)).ConfigureAwait(false);
				}
				
				SafeArrayHandle resultingHash = null;
				long location = 0;
				for(int j = 0; j < this.FillerHashIterations; j++) {
					
					TypeSerializer.Serialize(j, bufferSpan.Slice(sizeof(long) + sizeof(long)+sizeof(long), sizeof(int)));
					// to prevent paralellisation, we ensure we take a bit of the previous run
					
					if(resultingHash != null) {
						// copy the previous hash
						resultingHash.Entry.CopyTo(buffer.Entry, 0, sizeof(long) + sizeof(long)+sizeof(long) + sizeof(int), sizeof(long));
					}
					localHashWorkSpace.Entry.CopyFrom(buffer.Entry, HASH_512_BYTES * 2);

					resultingHash = this.powHashSet.Hash(localHashWorkSpace);

					TypeSerializer.Deserialize(cacheSpan.Slice((int)(location * chunks), sizeof(uint)), out uint cache32);
					location = cache32 % this.FillerHashIterations;
					resultingHash.Entry.CopyTo(cache.Entry, 0, (int)(location * chunks), resultingHash.Length);
				}
				
				// this will fill with pseudorandom bytes quickly
				this.powCryptoSet.EncryptStringToBytes(cache, cache);

				await scratchpad.CopyFrom(cache.Entry, 0, k * this.CacheMemorySize, this.CacheMemorySize).ConfigureAwait(false);
			}
		}

		private SafeArrayHandle GetNoncedHash(BigInteger hash, long nonce) {

			int byteCount = hash.GetByteCount();
			using var hashbytes = ByteArray.Create(byteCount);
			hash.TryWriteBytes(hashbytes.Span, out int bytesWritten);

			Span<byte> noncebytes = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize(nonce, noncebytes);

			using var message = SafeArrayHandle.Create(hashbytes.Length + noncebytes.Length);

			//arbitrary put together
			message.Entry.CopyFrom(hashbytes);
			message.Entry.CopyFrom(noncebytes, 0, hashbytes.Length, noncebytes.Length);

			SafeArrayHandle result = this.powHashSet.Hash(message);

			return result;
		}
	}

}