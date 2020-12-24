using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.THS.V1.Crypto;
using Neuralia.Blockchains.Core.Cryptography.THS.V1.Hash;
using Neuralia.Blockchains.Core.Cryptography.THS.V1.Prng;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Data.Arrays.Large;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1 {
	public class THSEngine : IDisposableExtended {

		public enum THSMemorySizes : int {
			THS_64_KB = 16,
			THS_128_KB = 15,
			THS_256_KB = 18,
			THS_512_KB = 19,
			THS_1_MB = 20,
			THS_2_MB = 21,
			THS_4_MB = 22,
			THS_8_MB = 23,
			THS_16_MB = 24,
			THS_32_MB = 25,
			THS_64_MB = 26,
			THS_128_MB = 27,
			THS_256_MB = 28,
			THS_512_MB = 29,
			THS_1_GB = 30,
			THS_2_GB = 31,
			THS_4_GB = 32,
			THS_8_MGB = 33,
			THS_16_MGB = 34
		}
		private const int HASH_SIZE = 64;
		private readonly int blockCount;
		private readonly int ComparisionSize;
		
		private SafeArrayHandle hashingCryptoWorkspaceBuffer;
		private SafeArrayHandle hashingWorkspaceBuffer;

		private Memory<byte> hashingWorkspaceSpan;
		private Memory<byte> hashingCryptoWorkspaceSpan;
		
		private readonly THSMemorySizes MainBufferDataSize; //2^30 = 1GB. 2^20 = 1MB
		private readonly long MainBufferDataSizeX2;
		private readonly Enums.THSMemoryTypes memoryType;
		private readonly int PageMemorySize;
		private readonly THSMemorySizes PageSize; // 2^16 = 64k // must be less than MainBufferDataSize. no smaller than 6

		private readonly SafeArrayHandle intBuffer;
		private readonly Memory<byte> intBufferSpan;

		private readonly SafeArrayHandle pageWorkspaceBuffer;
		private readonly Memory<byte> pageWorkspaceBufferSpan;
		private readonly ClosureWrapper<MemoryHandle> pageWorkspaceHandle;
		private readonly unsafe byte* pageWorkspace;

		private readonly THSCryptoSet thsCryptoSet;
		private readonly THSHashSet thsHashSet;
		private readonly THSPrngSet thsPrngSet;

		private readonly THSRulesSet rulesSet;
		private readonly THSRulesSetDescriptor rulesSetDescriptor;

		private bool firstRun = true;
		private bool initialized = false;
		private IVeryLargeByteArray scratchpad;

		public THSEngine(THSRulesSet rulesSet, THSRulesSetDescriptor rulesSetDescriptor, Enums.THSMemoryTypes memoryType) {
			this.memoryType = memoryType;

			this.rulesSet = rulesSet;
			this.rulesSetDescriptor = rulesSetDescriptor;
			this.thsHashSet = rulesSet.GenerateHashSet();
			this.thsCryptoSet = rulesSet.GenerateCryptoSet();
			this.thsPrngSet = rulesSet.GeneratePrngSet();

			this.PageSize = this.rulesSet.PageSize;
			this.MainBufferDataSize = this.rulesSet.MainBufferDataSize; //2^30 = 1GB. 2^20 = 1MB

			this.MainBufferDataSizeX2 = 1L << (int)this.MainBufferDataSize; //2^30 = 1GB
			this.PageMemorySize = 1 << (int)this.PageSize; //2^12 = 4096 bytes

			this.ComparisionSize = 1 << (this.MainBufferDataSize - this.PageSize); //2^(30-16) = 256K;

			this.blockCount = this.PageMemorySize / THSPrngBase.BLOCK_SIZE_BYTES;

			if(this.MainBufferDataSizeX2 < this.PageMemorySize) {
				throw new ApplicationException($"Main buffer memory size too small. Currently {this.MainBufferDataSizeX2} bytes, but page memory size is {this.PageMemorySize} bytes.");
			}

			// setup the memory buffers
			this.pageWorkspaceBuffer = SafeArrayHandle.Create(this.PageMemorySize);
			this.pageWorkspaceHandle = this.pageWorkspaceBuffer.Memory.Pin();

			unsafe {
				this.pageWorkspace = (byte*) this.pageWorkspaceHandle.Value.Pointer;
			}

			this.hashingWorkspaceBuffer = SafeArrayHandle.Create(HASH_SIZE);
			this.hashingCryptoWorkspaceBuffer = SafeArrayHandle.Create(this.hashingWorkspaceBuffer.Length);

			this.hashingWorkspaceSpan = this.hashingWorkspaceBuffer.Memory;
			this.hashingCryptoWorkspaceSpan = this.hashingCryptoWorkspaceBuffer.Memory;
			this.pageWorkspaceBufferSpan = this.pageWorkspaceBuffer.Memory;

			if(this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.ScramblePages)) {
				this.intBuffer = SafeArrayHandle.Create(sizeof(ulong));
				this.intBufferSpan = this.intBuffer.Memory;
			}
		}
		
		public async Task Initialize(){
			if(!this.initialized) {
				
				await this.CreateScratchpad().ConfigureAwait(false);
				this.initialized = true;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private BigInteger BuildRootHash(SafeArrayHandle root, int round) {
			using SafeArrayHandle hashBuffer = SafeArrayHandle.Create(root.Length + sizeof(int));
			root.CopyTo(hashBuffer);

			using SafeArrayHandle paddingBuffer = SafeArrayHandle.Create(sizeof(int));
			TypeSerializer.Serialize(round, paddingBuffer.Span);

			paddingBuffer.CopyTo(hashBuffer, 0, root.Length, sizeof(int));

			using SafeArrayHandle rootHash = this.thsHashSet.Hash(hashBuffer);

			return HashDifficultyUtils.GetBigInteger(rootHash);
		}

		public async Task<THSSolutionSet> PerformTHS(SafeArrayHandle root, CancellationToken? cancellationToken, Func<long, long, long, long, long, long, long, List<(int solution, long nonce)>, Task> starting = null, Func<long, int, long, List<(int solution, long nonce)>, long, long, double, Task> iteration = null, Func<int, long, long, int, Task> roundCallback = null, THSProcessState thsState = null) {
			
			THSSolutionSet solutionSet = new THSSolutionSet();

			long hashTargetDifficulty = this.rulesSetDescriptor.HashTargetDifficulty;
			BigInteger hashTarget = HashDifficultyUtils.GetHash512TargetByIncrementalDifficulty(hashTargetDifficulty);
			NLog.Default.Debug("Difficulty: {0}", hashTargetDifficulty);
			NLog.Default.Debug("target: {0}", hashTarget);

			int startingRound = 1;
			long totalNonce = 0;
			long startingNonce = 0;

			if(thsState != null) {
				startingRound = thsState.Round;
				totalNonce = thsState.TotalNonce;
				startingNonce = thsState.Nonce;
				foreach(var solution in thsState.Solutions) {
					solutionSet.AddSolution(solution.Solution, solution.Nonce);
				}
			}
			int round = startingRound;

			using xxHasher32 hasher = new xxHasher32();

			// start with the estimated amount of total seconds as per the descriptor

			long targetTotalDuration = (long)this.rulesSetDescriptor.TargetTimespan.TotalSeconds;
			long nonceTarget = this.rulesSetDescriptor.NonceTarget;
			long benchmarkEstimatedIterationTime = (long)this.rulesSetDescriptor.EstimatedIterationTime.TotalSeconds;
			long estimatedIterationTime = benchmarkEstimatedIterationTime;
			long estimatedRemainingTime = estimatedIterationTime * (nonceTarget - totalNonce);
			double benchmarkSpeedRatio = 1;
			
			if(starting != null) {
				await starting(this.rulesSetDescriptor.HashTargetDifficulty, targetTotalDuration, estimatedIterationTime, estimatedRemainingTime, startingNonce, totalNonce, startingRound, solutionSet.Solutions).ConfigureAwait(false);
			}
			while(true) {

				
				long nonce = 0;

				if(round == startingRound) {
					nonce = startingNonce;
				}
				
				BigInteger hash = this.BuildRootHash(root, round);

				int solution = 0;

				while(true) {

					cancellationToken?.ThrowIfCancellationRequested();
					nonce++;
					totalNonce++;
					this.ResetSets();

					NLog.Default.Debug($"Performing proof of work for Nonce {nonce} and Round {round}.");

					// alert we are running an iteration
					if(iteration != null) {
						await iteration(nonce, round, totalNonce, solutionSet.Solutions, estimatedIterationTime, estimatedRemainingTime, benchmarkSpeedRatio).ConfigureAwait(false);
					}

					DateTime start = DateTime.Now;
					
					BigInteger result = await this.PerformIteration(hash, nonce).ConfigureAwait(false);

					// update the estimated time
					estimatedIterationTime = (long) (DateTime.Now - start).TotalSeconds;
					estimatedRemainingTime = estimatedIterationTime * (nonceTarget - totalNonce);
					benchmarkSpeedRatio = (double)estimatedIterationTime / benchmarkEstimatedIterationTime;

					if(double.IsNaN(benchmarkSpeedRatio)) {
						benchmarkSpeedRatio = 0; 
					}
					if(result < hashTarget) {
						NLog.Default.Debug("Found pre hash result: {0}", result);
						solution = hasher.Hash(result.ToByteArray());

						break;
					}
				}

				solutionSet.AddSolution(solution, nonce);

				if(roundCallback != null) {
					await roundCallback(round, totalNonce, nonce, solution).ConfigureAwait(false);
				}

				if(totalNonce < nonceTarget) {
					// no choice, we were too low, we increment the padding

					round++;

					continue;
				}

				NLog.Default.Debug("Found a solution with value {0}!", solution);

				return solutionSet;

			}
		}

		/// <summary>
		///     here we verify the THS by running it for the passed parameters
		/// </summary>
		/// <param name="nonce"></param>
		/// <param name="hashTargetDifficulty"></param>
		/// <param name="solutions"></param>
		/// <returns></returns>
		public async Task<bool> Verify(SafeArrayHandle root, THSSolutionSet solutionSet) {

			if((root == null) || root.IsZero || solutionSet.IsEmpty) {
				return false;
			}

			long hashTargetDifficulty = this.rulesSetDescriptor.HashTargetDifficulty;
			BigInteger hashTarget = HashDifficultyUtils.GetHash512TargetByIncrementalDifficulty(hashTargetDifficulty);
			NLog.Default.Debug("target: {0}", hashTarget);
			NLog.Default.Debug("Difficulty: {0}", hashTargetDifficulty);

			long totalNonce = 0;
			int round = 1;
			int solutionsCount = solutionSet.Solutions.Count;
			long nonceTarget = this.rulesSetDescriptor.NonceTarget;
			
			foreach((int solution, long nonce) in solutionSet.Solutions) {

				if((nonce == 0) || (solution == 0)) {
					return false;
				}

				BigInteger hash = this.BuildRootHash(root, round);
				totalNonce += nonce;

				if(totalNonce > nonceTarget && round < solutionsCount) {

					// we dont go any more rounds after reaching target
					return false;
				}

				int currentSolution = 0;

				NLog.Default.Debug($"Performing proof of work verification for Nonce {nonce} and Round {round}.");

				this.ResetSets();

				BigInteger result = await this.PerformIteration(hash, nonce).ConfigureAwait(false);

				if(result < hashTarget) {
					using xxHasher32 hasher = new xxHasher32();
					currentSolution = hasher.Hash(result.ToByteArray());
				}

				// lets return if they match
				if(solution != currentSolution) {
					return false;
				}

				round++;
			}

			if(totalNonce < nonceTarget) {

				// we did not meet the minimum
				return false;
			}

			return true;
		}

		private void ResetSets() {
			this.thsHashSet.Reset();
			this.thsCryptoSet.Reset();
			this.thsPrngSet.Reset();
		}

		private async Task<BigInteger> PerformIteration(BigInteger rootHash, long nonce) {

			using SafeArrayHandle iterationHash = this.GetNoncedHash(rootHash, nonce);

			// start by clearing the memory
			if(!this.firstRun) {
				await this.scratchpad.Clear().ConfigureAwait(false);
				this.pageWorkspaceBuffer.Clear();
				this.hashingCryptoWorkspaceBuffer.Clear();
				this.hashingWorkspaceBuffer.Clear();
			}

			this.firstRun = false;
			int cacheChunks = this.PageMemorySize / sizeof(ulong);
			
			// loop through the pages-
			for(ulong page = 0; page < (ulong) this.ComparisionSize; page++) {
				
				ulong pageOffset = page * (ulong) this.PageMemorySize;

				//  prepare the seed hash

				// step 1 Fill from memory zones from a random previous page
				if(page == 0) {
					// first entry is always the iteration hash
					iterationHash.CopyTo(this.hashingWorkspaceBuffer);
				} else {
					// lets get some random memory
					TypeSerializer.Deserialize(this.hashingWorkspaceSpan.Slice(sizeof(ulong), sizeof(ulong)), out ulong previousMarker);
					ulong previousPageOffset = previousMarker % page;
					ulong previousPageBytesOffset = (previousPageOffset * (ulong)this.PageMemorySize);
					
					ulong innerBytesOffset = (previousMarker & (ulong) (this.PageMemorySize - 1));
					innerBytesOffset = Math.Min(innerBytesOffset, (ulong)this.PageMemorySize- HASH_SIZE);
					await this.scratchpad.CopyTo(this.hashingWorkspaceBuffer.Entry, (long)(previousPageBytesOffset + innerBytesOffset), 0, HASH_SIZE).ConfigureAwait(false);

					if(this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.HashPages)) {
						// lets take the final hash (last bytes) and merge it in
						TypeSerializer.Deserialize(this.hashingWorkspaceSpan.Slice(sizeof(ulong)*2, sizeof(ulong)), out previousMarker);
						previousPageOffset = previousMarker % page;
						previousPageBytesOffset = (previousPageOffset * (ulong)this.PageMemorySize);
						
						await this.scratchpad.CopyTo(this.hashingCryptoWorkspaceBuffer.Entry, (long)previousPageBytesOffset+(this.PageMemorySize- HASH_SIZE), 0, HASH_SIZE).ConfigureAwait(false);
						
						MergeIn(this.hashingWorkspaceSpan, this.hashingCryptoWorkspaceSpan, sizeof(ulong)*0, sizeof(ulong));
						MergeIn(this.hashingWorkspaceSpan, this.hashingCryptoWorkspaceSpan, sizeof(ulong)*1, sizeof(ulong));
						MergeIn(this.hashingWorkspaceSpan, this.hashingCryptoWorkspaceSpan, sizeof(ulong)*2, sizeof(ulong));
						MergeIn(this.hashingWorkspaceSpan, this.hashingCryptoWorkspaceSpan, sizeof(ulong)*3, sizeof(ulong));
						MergeIn(this.hashingWorkspaceSpan, this.hashingCryptoWorkspaceSpan, sizeof(ulong)*4, sizeof(ulong));
						MergeIn(this.hashingWorkspaceSpan, this.hashingCryptoWorkspaceSpan, sizeof(ulong)*5, sizeof(ulong));
						MergeIn(this.hashingWorkspaceSpan, this.hashingCryptoWorkspaceSpan, sizeof(ulong)*6, sizeof(ulong));
						MergeIn(this.hashingWorkspaceSpan, this.hashingCryptoWorkspaceSpan, sizeof(ulong)*7, sizeof(ulong));
					}
				}

				// step 2 crypto
				TypeSerializer.Deserialize(this.hashingWorkspaceSpan.Slice(sizeof(ulong) * 2, sizeof(ulong)), out ulong cryptoMarker);

				if((cryptoMarker % (ulong) this.rulesSet.CryptoSuccessRate) == 0) {

					for(int i = 0; i < this.rulesSet.CryptoIterations; i++) {
						this.thsCryptoSet.EncryptStringToBytes(this.hashingWorkspaceBuffer, this.hashingCryptoWorkspaceBuffer);
						// perform a swap
						var temp = this.hashingWorkspaceBuffer;
						this.hashingWorkspaceBuffer = this.hashingCryptoWorkspaceBuffer;
						this.hashingCryptoWorkspaceBuffer = temp;

						var tempSpan = this.hashingWorkspaceSpan;
						this.hashingWorkspaceSpan = this.hashingCryptoWorkspaceSpan;
						this.hashingCryptoWorkspaceSpan = tempSpan;
					}
				}

				// step 2.1 hash
				TypeSerializer.Deserialize(this.hashingWorkspaceSpan.Slice(sizeof(ulong) * 3, sizeof(ulong)), out ulong hashingMarker);

				if((hashingMarker % (ulong) this.rulesSet.HashingSuccessRate) == 0) {
					for(int i = 0; i < this.rulesSet.HashIterations; i++) {
						using SafeArrayHandle resultHash = this.thsHashSet.Hash512(this.hashingWorkspaceBuffer);
						resultHash.CopyTo(this.hashingWorkspaceBuffer);
					}
				}

				// step 4, generate the PRNG and load the page
				unsafe {
					if(this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.FillFast)) {
						this.thsPrngSet.FillFast(this.pageWorkspace, this.blockCount, this.hashingWorkspaceBuffer.Entry);
					} 
					else if(this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.FillFull)) {
						this.thsPrngSet.FillFull(this.pageWorkspace, this.blockCount, this.hashingWorkspaceBuffer.Entry);
					}
					else {
						this.thsPrngSet.FillMedium(this.pageWorkspace, this.blockCount, this.hashingWorkspaceBuffer.Entry);
					}
				}

				// step 5 scramble if applicable
				if(page != 0 && this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.ScramblePages)) {
					// build a pseudorandom location to pick data
					int hashOffset = sizeof(ulong) * 4;
					TypeSerializer.Deserialize(this.hashingWorkspaceSpan.Slice(hashOffset, sizeof(uint)), out uint cache32);
					ulong nextLocationStart = cache32 % page;
					TypeSerializer.Deserialize(this.hashingWorkspaceSpan.Slice(hashOffset + sizeof(uint), sizeof(uint)), out cache32);
					ulong nextLocationMiddle = cache32 % page;
					TypeSerializer.Deserialize(this.hashingWorkspaceSpan.Slice(hashOffset + (sizeof(uint) * 2), sizeof(uint)), out cache32);
					ulong nextLocationEnd = cache32 % page;

					// and the middle random
					TypeSerializer.Deserialize(this.hashingWorkspaceSpan.Slice(hashOffset + (sizeof(uint) * 3), sizeof(uint)), out cache32);
					int middleLocation = (int) (cache32 % cacheChunks);

					// beginning
					await this.Scramble(nextLocationStart, 0, this.intBufferSpan, this.pageWorkspaceBufferSpan).ConfigureAwait(false);

					// middle
					await this.Scramble(nextLocationMiddle, middleLocation, this.intBufferSpan, this.pageWorkspaceBufferSpan).ConfigureAwait(false);

					// end
					await this.Scramble(nextLocationEnd, cacheChunks - 1, this.intBufferSpan, this.pageWorkspaceBufferSpan).ConfigureAwait(false);
				}

				if(this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.HashPages)) {
					// now hash the page
					using SafeArrayHandle pageHash = this.thsHashSet.Hash512(this.pageWorkspaceBuffer);
					
					// and copy it back
					pageHash.CopyTo(this.pageWorkspaceBuffer, 0, this.pageWorkspaceBuffer.Length-HASH_SIZE, HASH_SIZE);
				}

				
				// step 4, save back to the scratchpad if we are not at the end
				if(page < (ulong) (this.ComparisionSize - 1)) {
					
					await this.scratchpad.CopyFrom(this.pageWorkspaceBuffer.Entry, 0, (long) pageOffset, this.PageMemorySize).ConfigureAwait(false);
				}
			}

			// assemble the final hash
			using SafeArrayHandle hashBuffer = SafeArrayHandle.Create(HASH_SIZE*2);
			
			TypeSerializer.Deserialize(this.pageWorkspaceBufferSpan.Slice(sizeof(ulong)*2, sizeof(ulong)), out ulong pageMarker);
			ulong innerPageOffset = (pageMarker & (ulong) (this.PageMemorySize - 1));
			innerPageOffset = Math.Min(innerPageOffset, (ulong)this.PageMemorySize- HASH_SIZE);
			this.pageWorkspaceBuffer.CopyTo(hashBuffer, (int)innerPageOffset, 0, HASH_SIZE);
			this.pageWorkspaceBuffer.CopyTo(hashBuffer, this.pageWorkspaceBuffer.Length-HASH_SIZE, HASH_SIZE, HASH_SIZE);
			
			using SafeArrayHandle hash = this.thsHashSet.Hash512(hashBuffer);

			return HashDifficultyUtils.GetBigInteger(hash);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void MergeIn(Memory<byte> a, Memory<byte> b, int start, int offset) {
			var sliceA = a.Slice(start, offset);
			TypeSerializer.Deserialize(sliceA, out ulong nonceA);
			TypeSerializer.Deserialize(b.Slice(start, offset), out ulong nonceB);
			TypeSerializer.Serialize(nonceA ^ nonceB, sliceA);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private async Task Scramble(ulong nextLocation, int chunk, Memory<byte> bufferSpan, Memory<byte> cacheBufferSpan) {

			int offset = chunk * sizeof(ulong);
			await this.scratchpad.CopyTo(this.intBuffer.Entry, ((long) nextLocation * this.PageMemorySize) + offset, 0, sizeof(ulong)).ConfigureAwait(false);
			TypeSerializer.Deserialize(bufferSpan, out ulong cache64A);

			cacheBufferSpan.Slice(offset, sizeof(ulong)).CopyTo(bufferSpan);
			TypeSerializer.Deserialize(bufferSpan, out ulong cache64B);

			cache64B ^= cache64A;
			TypeSerializer.Serialize(cache64B, bufferSpan);
			bufferSpan.Slice(0, sizeof(ulong)).CopyTo(cacheBufferSpan.Slice(offset, sizeof(ulong)));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private SafeArrayHandle GetNoncedHash(BigInteger hash, long nonce) {

			int byteCount = hash.GetByteCount();
			using ByteArray hashBytes = ByteArray.Create(byteCount);
			hash.TryWriteBytes(hashBytes.Span, out int bytesWritten);

			Span<byte> nonceBytes = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize(nonce, nonceBytes);

			using SafeArrayHandle message = SafeArrayHandle.Create(hashBytes.Length + nonceBytes.Length);

			//arbitrary put together
			message.Entry.CopyFrom(hashBytes);
			message.Entry.CopyFrom(nonceBytes, 0, hashBytes.Length, nonceBytes.Length);

			return this.thsHashSet.Hash(message);
		}

		/// <summary>
		///     create a scratchpad with backup methods
		/// </summary>
		/// <exception cref="IOException"></exception>
		/// <exception cref="OutOfMemoryException"></exception>
		private async Task CreateScratchpad() {
			long size = this.MainBufferDataSizeX2 - (int)this.PageSize;
			async Task CreateHDDScratchpad() {
				try {
					this.scratchpad = new FileMappedLargeByteArray(size, this.memoryType == Enums.THSMemoryTypes.HDD_DB);

					try {
						await this.scratchpad.Initialize().ConfigureAwait(false);
					} catch {
						this.scratchpad?.Dispose();
						this.scratchpad = null;

						throw;
					}
				} catch(IOException e) {
					this.scratchpad = new FileMappedLargeByteArray(size, this.memoryType == Enums.THSMemoryTypes.HDD);
					try {
						await this.scratchpad.Initialize().ConfigureAwait(false);
					} catch {
						this.scratchpad?.Dispose();
						this.scratchpad = null;

						throw;
					}
				}
			}

			async Task CreateRAMScratchpad() {
				this.scratchpad = new VeryLargeByteArray(size);
				try {
					await this.scratchpad.Initialize().ConfigureAwait(false);
				} catch {
					this.scratchpad?.Dispose();
					this.scratchpad = null;

					throw;
				}
			}

			if(this.memoryType == Enums.THSMemoryTypes.RAM) {
				try {
					await CreateRAMScratchpad().ConfigureAwait(false);
				} catch(OutOfMemoryException ex) {

					// try the HDD version as a backup
					try {
						await CreateHDDScratchpad().ConfigureAwait(false);
					} catch(IOException e) {
						throw new IOException("Failed to create a backup HDD scatchpad after failing to create a RAM one.", ex);
					}
				}
			} else {
				try {
					await CreateHDDScratchpad().ConfigureAwait(false);
				} catch(IOException e) {
					// try the RAM version as a backup
					try {
						await CreateRAMScratchpad().ConfigureAwait(false);
					} catch(OutOfMemoryException ex) {

						// try the HDD version as a backup
						throw new OutOfMemoryException("Failed to create a backup RAM scatchpad after failing to create a HDD one.", ex);
					}
				}
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

				this.scratchpad.Dispose();

				this.pageWorkspaceHandle.Value.Dispose();
				this.pageWorkspaceBuffer.Dispose();

				this.hashingWorkspaceBuffer.Dispose();

				this.hashingCryptoWorkspaceBuffer.Dispose();
				
				this.intBuffer?.Dispose();
			}

			this.IsDisposed = true;
		}

		~THSEngine() {
			this.Dispose(false);
		}

	#endregion

	}
}