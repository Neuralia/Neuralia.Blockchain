using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
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
using Neuralia.Blockchains.Tools.Threading;

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

		private readonly THSMemorySizes MainBufferDataSize; //2^30 = 1GB. 2^20 = 1MB
		private readonly long MainBufferDataSizeX2;
		private readonly Enums.THSMemoryTypes memoryType;
		private readonly int PageMemorySize;
		private readonly THSMemorySizes PageSize; // 2^16 = 64k // must be less than MainBufferDataSize. no smaller than 6
		public THSModes Mode { get; private set; }

		private readonly THSRulesSet rulesSet;
		private readonly THSRulesSetDescriptor rulesSetDescriptor;

		private bool initialized = false;

		private int threadCount;
		private THSThreadContext[] threadContexts;

		private class THSThreadContext : IDisposable {
			public int threadId;
			public bool firstRun = true;
			public IVeryLargeByteArray scratchpad;
			public readonly THSCryptoSet thsCryptoSet;
			public readonly THSHashSet thsHashSet;
			public readonly THSPrngSet thsPrngSet;

			public readonly SafeArrayHandle pageWorkspaceBuffer;
			public readonly Memory<byte> pageWorkspaceBufferSpan;
			public readonly ClosureWrapper<MemoryHandle> pageWorkspaceHandle;
			public readonly unsafe byte* pageWorkspace;
			public SafeArrayHandle hashingCryptoWorkspaceBuffer;
			public SafeArrayHandle hashingWorkspaceBuffer;

			public Memory<byte> hashingWorkspaceSpan;
			public Memory<byte> hashingCryptoWorkspaceSpan;

			public readonly SafeArrayHandle intBuffer;
			public readonly Memory<byte> intBufferSpan;

			private long currentNonce;

			public long CurrentNonce {
				get => Interlocked.Read(ref this.currentNonce);
				set => Interlocked.Exchange(ref this.currentNonce, value);
			}

			public THSThreadContext(THSRulesSet rulesSet, int pageMemorySize) {
				this.thsHashSet = rulesSet.GenerateHashSet();
				this.thsCryptoSet = rulesSet.GenerateCryptoSet();
				this.thsPrngSet = rulesSet.GeneratePrngSet();

				// setup the memory buffers
				this.pageWorkspaceBuffer = SafeArrayHandle.Create(pageMemorySize);
				this.pageWorkspaceHandle = this.pageWorkspaceBuffer.Memory.Pin();

				unsafe {
					this.pageWorkspace = (byte*) this.pageWorkspaceHandle.Value.Pointer;
				}

				this.pageWorkspaceBufferSpan = this.pageWorkspaceBuffer.Memory;

				this.hashingWorkspaceBuffer = SafeArrayHandle.Create(HASH_SIZE);
				this.hashingCryptoWorkspaceBuffer = SafeArrayHandle.Create(this.hashingWorkspaceBuffer.Length);

				this.hashingWorkspaceSpan = this.hashingWorkspaceBuffer.Memory;
				this.hashingCryptoWorkspaceSpan = this.hashingCryptoWorkspaceBuffer.Memory;

				if(rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.ScramblePages)) {
					this.intBuffer = SafeArrayHandle.Create(sizeof(ulong));
					this.intBufferSpan = this.intBuffer.Memory;
				}
			}

			public void Clear() {
				this.pageWorkspaceBuffer.Clear();
				this.hashingCryptoWorkspaceBuffer.Clear();
				this.hashingWorkspaceBuffer.Clear();
			}

			public void ResetSets() {
				this.thsHashSet.Reset();
				this.thsCryptoSet.Reset();
				this.thsPrngSet.Reset();
			}

			public void Dispose() {
				this.scratchpad?.Dispose();

				this.pageWorkspaceHandle.Value.Dispose();
				this.pageWorkspaceBuffer.Dispose();

				this.hashingWorkspaceBuffer.Dispose();

				this.hashingCryptoWorkspaceBuffer.Dispose();

				this.intBuffer?.Dispose();
			}
		}

		public THSEngine(THSRulesSet rulesSet, THSRulesSetDescriptor rulesSetDescriptor, Enums.THSMemoryTypes memoryType) {
			this.memoryType = memoryType;

			this.rulesSet = rulesSet;
			this.rulesSetDescriptor = rulesSetDescriptor;

			this.PageSize = this.rulesSet.PageSize;
			this.MainBufferDataSize = this.rulesSet.MainBufferDataSize; //2^30 = 1GB. 2^20 = 1MB

			this.MainBufferDataSizeX2 = 1L << (int) this.MainBufferDataSize; //2^30 = 1GB
			this.PageMemorySize = 1 << (int) this.PageSize; //2^12 = 4096 bytes

			this.ComparisionSize = 1 << (this.MainBufferDataSize - this.PageSize); //2^(30-16) = 256K;

			this.blockCount = this.PageMemorySize / THSPrngBase.BLOCK_SIZE_BYTES;

			if(this.MainBufferDataSizeX2 < this.PageMemorySize) {
				throw new ApplicationException($"Main buffer memory size too small. Currently {this.MainBufferDataSizeX2} bytes, but page memory size is {this.PageMemorySize} bytes.");
			}
		}

		public enum THSModes {
			Generate,
			Verify
		}

		public async Task Initialize(THSModes mode, int? threadCountSet = null) {
			if(!this.initialized) {

				this.Mode = mode;

				this.threadCount = 1;

				if(mode == THSModes.Generate) {

					if(threadCountSet.HasValue) {
						this.threadCount = threadCountSet.Value;
					} else {
						this.threadCount = GlobalSettings.ApplicationSettings.THSThreadCount;
					}
				}

				this.threadCount = Math.Min(this.threadCount, Environment.ProcessorCount);

				this.threadContexts = new THSThreadContext[this.threadCount];

				for(int i = 0; i < this.threadCount; i++) {
					this.threadContexts[i] = new THSThreadContext(this.rulesSet, this.PageMemorySize);
					this.threadContexts[i].scratchpad = await this.CreateScratchpad().ConfigureAwait(false);
				}

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

			using SafeArrayHandle rootHash = HashingUtils.HashSha3_512(hashBuffer);

			return HashDifficultyUtils.GetBigInteger(rootHash);
		}
		
		public async Task<THSSolutionSet> PerformTHS(SafeArrayHandle root, CancellationToken? cancellationToken, Func<long, long, long, long, long, int, long, List<(int solution, long nonce)>, Task> starting = null, Func<long[], int, long, List<(int solution, long nonce)>, long, long, double, Task> iteration = null, Func<int, long, int, Task> roundCallback = null, THSProcessState thsState = null) {

			if(this.Mode == THSModes.Verify) {
				throw new ApplicationException("Invalid THS engine mode");
			}

			THSSolutionSet solutionSet = new THSSolutionSet();

			long hashTargetDifficulty = this.rulesSetDescriptor.HashTargetDifficulty;
			BigInteger hashTarget = HashDifficultyUtils.GetHash512TargetByIncrementalDifficulty(hashTargetDifficulty);
			NLog.Default.Debug("Difficulty: {0}", hashTargetDifficulty);
			NLog.Default.Debug("target: {0}", hashTarget);
			NLog.Default.Debug("number of threads: {0}", this.threadCount);
			NLog.Default.Debug("We are searching for {0} solution rounds", this.rulesSetDescriptor.Rounds);
			
			int startingRound = 1;
			long startingNonce = 0;

			if(thsState != null) {
				startingRound = thsState.Round;
				startingNonce = thsState.Nonce;

				if(thsState.Solutions.Count > this.rulesSetDescriptor.Rounds) {
					throw new ApplicationException("We have too many rounds");
				}
				
				foreach(var solution in thsState.Solutions) {
					solutionSet.AddSolution(solution.Solution, solution.Nonce);
				}
			}

			int round = startingRound;

			// start with the estimated amount of total seconds as per the descriptor

			long targetTotalDuration = (long) this.rulesSetDescriptor.TargetTimespan.TotalSeconds;
			long nonceTarget = this.rulesSetDescriptor.NonceTarget;
			long benchmarkEstimatedIterationTime = (long) this.rulesSetDescriptor.EstimatedIterationTime.TotalSeconds;
			long estimatedIterationTime = benchmarkEstimatedIterationTime;
			long estimatedRemainingTime = estimatedIterationTime * nonceTarget;
			double benchmarkSpeedRatio = 1;
			int zeroSolutions = 0;
			
			if(starting != null) {
				await starting(this.rulesSetDescriptor.HashTargetDifficulty, targetTotalDuration, estimatedIterationTime, estimatedRemainingTime, startingNonce, startingRound, this.rulesSetDescriptor.EstimatedRoundTarget, solutionSet.Solutions).ConfigureAwait(false);
			}
		
			using xxHasher32 hasher = new xxHasher32();

			using AsyncManualResetEventSlim resetEvent = new AsyncManualResetEventSlim(false);
			
			while(true) {

				long nonce = 0;

				if(round == startingRound) {
					nonce = startingNonce;
				}
				
				BigInteger hash = this.BuildRootHash(root, round);
				
				object locker = new object();
				object solutionLocker = new object();
				long roundSolutionFound = 0;
				long roundNonceFound = 0;

				using CancellationTokenSource tokenSource = new CancellationTokenSource();
				
				Task[] tasks = new Task[this.threadCount];

				for(int i = 0; i < this.threadCount; i++) {

					int index = i;

					tasks[i] = Task.Run(async () => {
						var localThreadContext = this.threadContexts[index];
						localThreadContext.threadId = Environment.CurrentManagedThreadId;
					
						while(Interlocked.Read(ref roundSolutionFound) == 0) {
							cancellationToken?.ThrowIfCancellationRequested();
							tokenSource.Token.ThrowIfCancellationRequested();

							long threadNonce = 0;

							lock(locker) {
								nonce++;
								threadNonce = nonce;
							}

							localThreadContext.CurrentNonce = threadNonce;
							
							resetEvent.Set();

							localThreadContext.ResetSets();

							NLog.Default.Debug($"Performing time hard signature for Nonce {threadNonce} and Round {round}. Thread Id {localThreadContext.threadId}");
						
							DateTime start = DateTime.Now;

							BigInteger result = await this.PerformIteration(hash, threadNonce, localThreadContext).ConfigureAwait(false);

							// update the estimated time
							long localEstimatedIterationTime = (long) (DateTime.Now - start).TotalSeconds;
							Interlocked.Exchange(ref estimatedIterationTime, localEstimatedIterationTime);

							decimal roundTime = localEstimatedIterationTime * rulesSetDescriptor.EstimatedRoundTarget;
							int currentRound = round;
							long remainingTime = (long)((roundTime * rulesSetDescriptor.Rounds) - ((roundTime * (currentRound-1)) - (threadNonce * localEstimatedIterationTime)) / this.threadCount);

							if(remainingTime < 0) {
								remainingTime = 0;
							}
							Interlocked.Exchange(ref estimatedRemainingTime, remainingTime);
							double localBenchmarkSpeedRatio = ((double) localEstimatedIterationTime / benchmarkEstimatedIterationTime) / this.threadCount;

							if(double.IsNaN(localBenchmarkSpeedRatio)) {
								localBenchmarkSpeedRatio = 0;
							}
							
							Interlocked.Exchange(ref benchmarkSpeedRatio, localBenchmarkSpeedRatio);

							if(result < hashTarget) {
								lock(solutionLocker) {
									if(roundSolutionFound == 0) {
										int solution = hasher.Hash(result.ToByteArray());
									
										NLog.Default.Debug($"Found solution {solution} with nonce {threadNonce}  for round {round}. Thread Id {localThreadContext.threadId}");

										Interlocked.Exchange(ref roundSolutionFound, solution);
										roundNonceFound = threadNonce;
									}
								}
							
								break;
							}
						}
					
						resetEvent.Set();
						
						return 1;
					}, tokenSource.Token);
				}

				long[] lastNonces = new long[this.threadCount];
				long[] currentNonces = new long[this.threadCount];
				while(true) {
					
					if(await resetEvent.WaitAsync(TimeSpan.FromSeconds(Math.Max(estimatedIterationTime >> 1, 3)), tokenSource.Token).ConfigureAwait(false)) {
						resetEvent.Reset();
					}

					if(tasks.Any(t => t.IsFaulted)) {
						tokenSource.Cancel();

						throw new AggregateException(tasks.Where(t => t.IsFaulted).Select(e => e.Exception));
					}
					
					// alert we are running an iteration
					if(iteration != null) {

						for(int i = 0; i < this.threadCount; i++) {
							currentNonces[i] = this.threadContexts[i].CurrentNonce;
						}

						if(!lastNonces.SequenceEqual(currentNonces)) {
							var orderedNonces = currentNonces.OrderBy(e => e).ToArray();
							long smallestTotalNonce = orderedNonces[0];

							
							await iteration(orderedNonces, round, smallestTotalNonce, solutionSet.Solutions, Interlocked.Read(ref estimatedIterationTime), Interlocked.Read(ref estimatedRemainingTime), Interlocked.CompareExchange(ref benchmarkSpeedRatio, 0,0)).ConfigureAwait(false);
							currentNonces.AsSpan().CopyTo(lastNonces);
						}
					}

					if(tasks.All(t => t.IsCompleted)) {
						break;
					}
				}

				if(roundSolutionFound == 0 || roundNonceFound == 0) {
					// seems this was a bug, let's retry the round
					zeroSolutions++;

					if(zeroSolutions == 3) {
						throw new ApplicationException("We had zero solutions multiple times");
					}
					continue;
				}

				zeroSolutions = 0;
				solutionSet.AddSolution((int)roundSolutionFound, roundNonceFound);

				if(roundCallback != null) {
					await roundCallback(round, roundNonceFound, (int)roundSolutionFound).ConfigureAwait(false);
				}

				if(solutionSet.Solutions.Count != this.rulesSetDescriptor.Rounds) {
					round++;

					continue;
				}

				NLog.Default.Debug($"THS successfully completed! Found {solutionSet.Solutions.Count} solutions.");

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
			if(this.Mode == THSModes.Generate) {
				throw new ApplicationException("Invalid THS engine mode");
			}

			if((root == null) || root.IsZero || solutionSet.IsEmpty || solutionSet.Solutions.Count != this.rulesSetDescriptor.Rounds || !solutionSet.IsValid) {
				return false;
			}

			long hashTargetDifficulty = this.rulesSetDescriptor.HashTargetDifficulty;
			BigInteger hashTarget = HashDifficultyUtils.GetHash512TargetByIncrementalDifficulty(hashTargetDifficulty);
			NLog.Default.Debug("target: {0}", hashTarget);
			NLog.Default.Debug("Difficulty: {0}", hashTargetDifficulty);
			NLog.Default.Debug("Testing: {0} rounds", this.rulesSetDescriptor.Rounds);
			
			int round = 1;
			int solutionsCount = solutionSet.Solutions.Count;
			long nonceTarget = this.rulesSetDescriptor.NonceTarget;

			foreach((int solution, long nonce) in solutionSet.Solutions) {

				if((nonce == 0) || (solution == 0)) {
					return false;
				}

				BigInteger hash = this.BuildRootHash(root, round);

				int currentSolution = 0;

				NLog.Default.Debug($"Performing time hard signature verification for Nonce {nonce} and Round {round}.");

				this.threadContexts[0].ResetSets();

				BigInteger result = await this.PerformIteration(hash, nonce, this.threadContexts[0]).ConfigureAwait(false);

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

			return true;
		}

		private async Task<BigInteger> PerformIteration(BigInteger rootHash, long nonce, THSThreadContext currentThreadContext) {

			using SafeArrayHandle iterationHash = this.GetNoncedHash(rootHash, nonce, currentThreadContext);

			// start by clearing the memory
			if(!currentThreadContext.firstRun) {
				await currentThreadContext.scratchpad.Clear().ConfigureAwait(false);
				currentThreadContext.pageWorkspaceBuffer.Clear();
				currentThreadContext.hashingCryptoWorkspaceBuffer.Clear();
				currentThreadContext.hashingWorkspaceBuffer.Clear();
			}
			
			currentThreadContext.firstRun = false;
			int cacheChunks = this.PageMemorySize / sizeof(ulong);

			// loop through the pages-
			for(ulong page = 0; page < (ulong) this.ComparisionSize; page++) {

				ulong pageOffset = page * (ulong) this.PageMemorySize;

				//  prepare the seed hash

				// step 1 Fill from memory zones from a random previous page
				if(page == 0) {
					// first entry is always the iteration hash
					iterationHash.CopyTo(currentThreadContext.hashingWorkspaceBuffer);
				} else {
					// lets get some random memory
					TypeSerializer.Deserialize(currentThreadContext.hashingWorkspaceSpan.Slice(sizeof(ulong), sizeof(ulong)), out ulong previousMarker);
					ulong previousPageOffset = previousMarker % page;
					ulong previousPageBytesOffset = (previousPageOffset * (ulong) this.PageMemorySize);

					ulong innerBytesOffset = (previousMarker & (ulong) (this.PageMemorySize - 1));
					innerBytesOffset = Math.Min(innerBytesOffset, (ulong) this.PageMemorySize - HASH_SIZE);
					await currentThreadContext.scratchpad.CopyTo(currentThreadContext.hashingWorkspaceBuffer.Entry, (long) (previousPageBytesOffset + innerBytesOffset), 0, HASH_SIZE).ConfigureAwait(false);

					if(this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.HashPages)) {
						// lets take the final hash (last bytes) and merge it in
						TypeSerializer.Deserialize(currentThreadContext.hashingWorkspaceSpan.Slice(sizeof(ulong) * 2, sizeof(ulong)), out previousMarker);
						previousPageOffset = previousMarker % page;
						previousPageBytesOffset = (previousPageOffset * (ulong) this.PageMemorySize);

						await currentThreadContext.scratchpad.CopyTo(currentThreadContext.hashingCryptoWorkspaceBuffer.Entry, (long) previousPageBytesOffset + (this.PageMemorySize - HASH_SIZE), 0, HASH_SIZE).ConfigureAwait(false);

						MergeIn(currentThreadContext.hashingWorkspaceSpan, currentThreadContext.hashingCryptoWorkspaceSpan, sizeof(ulong) * 0, sizeof(ulong));
						MergeIn(currentThreadContext.hashingWorkspaceSpan, currentThreadContext.hashingCryptoWorkspaceSpan, sizeof(ulong) * 1, sizeof(ulong));
						MergeIn(currentThreadContext.hashingWorkspaceSpan, currentThreadContext.hashingCryptoWorkspaceSpan, sizeof(ulong) * 2, sizeof(ulong));
						MergeIn(currentThreadContext.hashingWorkspaceSpan, currentThreadContext.hashingCryptoWorkspaceSpan, sizeof(ulong) * 3, sizeof(ulong));
						MergeIn(currentThreadContext.hashingWorkspaceSpan, currentThreadContext.hashingCryptoWorkspaceSpan, sizeof(ulong) * 4, sizeof(ulong));
						MergeIn(currentThreadContext.hashingWorkspaceSpan, currentThreadContext.hashingCryptoWorkspaceSpan, sizeof(ulong) * 5, sizeof(ulong));
						MergeIn(currentThreadContext.hashingWorkspaceSpan, currentThreadContext.hashingCryptoWorkspaceSpan, sizeof(ulong) * 6, sizeof(ulong));
						MergeIn(currentThreadContext.hashingWorkspaceSpan, currentThreadContext.hashingCryptoWorkspaceSpan, sizeof(ulong) * 7, sizeof(ulong));
					}
				}

				// step 2 crypto
				TypeSerializer.Deserialize(currentThreadContext.hashingWorkspaceSpan.Slice(sizeof(ulong) * 2, sizeof(ulong)), out ulong cryptoMarker);

				if((cryptoMarker % (ulong) this.rulesSet.CryptoSuccessRate) == 0) {

					for(int i = 0; i < this.rulesSet.CryptoIterations; i++) {
						currentThreadContext.thsCryptoSet.EncryptStringToBytes(currentThreadContext.hashingWorkspaceBuffer, currentThreadContext.hashingCryptoWorkspaceBuffer);

						// perform a swap
						var temp = currentThreadContext.hashingWorkspaceBuffer;
						currentThreadContext.hashingWorkspaceBuffer = currentThreadContext.hashingCryptoWorkspaceBuffer;
						currentThreadContext.hashingCryptoWorkspaceBuffer = temp;

						var tempSpan = currentThreadContext.hashingWorkspaceSpan;
						currentThreadContext.hashingWorkspaceSpan = currentThreadContext.hashingCryptoWorkspaceSpan;
						currentThreadContext.hashingCryptoWorkspaceSpan = tempSpan;
					}
				}

				// step 2.1 hash
				TypeSerializer.Deserialize(currentThreadContext.hashingWorkspaceSpan.Slice(sizeof(ulong) * 3, sizeof(ulong)), out ulong hashingMarker);

				if((hashingMarker % (ulong) this.rulesSet.HashingSuccessRate) == 0) {
					for(int i = 0; i < this.rulesSet.HashIterations; i++) {
						using SafeArrayHandle resultHash = currentThreadContext.thsHashSet.Hash512(currentThreadContext.hashingWorkspaceBuffer);
						resultHash.CopyTo(currentThreadContext.hashingWorkspaceBuffer);
					}
				}

				// step 4, generate the PRNG and load the page
				unsafe {
					if(this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.FillFast)) {
						currentThreadContext.thsPrngSet.FillFast(currentThreadContext.pageWorkspace, this.blockCount, currentThreadContext.hashingWorkspaceBuffer.Entry);
					} else if(this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.FillFull)) {
						currentThreadContext.thsPrngSet.FillFull(currentThreadContext.pageWorkspace, this.blockCount, currentThreadContext.hashingWorkspaceBuffer.Entry);
					} else {
						currentThreadContext.thsPrngSet.FillMedium(currentThreadContext.pageWorkspace, this.blockCount, currentThreadContext.hashingWorkspaceBuffer.Entry);
					}
				}

				// step 5 scramble if applicable
				if(page != 0 && this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.ScramblePages)) {
					// build a pseudorandom location to pick data
					int hashOffset = sizeof(ulong) * 4;
					TypeSerializer.Deserialize(currentThreadContext.hashingWorkspaceSpan.Slice(hashOffset, sizeof(uint)), out uint cache32);
					ulong nextLocationStart = cache32 % page;
					TypeSerializer.Deserialize(currentThreadContext.hashingWorkspaceSpan.Slice(hashOffset + sizeof(uint), sizeof(uint)), out cache32);
					ulong nextLocationMiddle = cache32 % page;
					TypeSerializer.Deserialize(currentThreadContext.hashingWorkspaceSpan.Slice(hashOffset + (sizeof(uint) * 2), sizeof(uint)), out cache32);
					ulong nextLocationEnd = cache32 % page;

					// and the middle random
					TypeSerializer.Deserialize(currentThreadContext.hashingWorkspaceSpan.Slice(hashOffset + (sizeof(uint) * 3), sizeof(uint)), out cache32);
					int middleLocation = (int) (cache32 % cacheChunks);

					// beginning
					await this.Scramble(nextLocationStart, 0, currentThreadContext.intBufferSpan, currentThreadContext.pageWorkspaceBufferSpan, currentThreadContext).ConfigureAwait(false);

					// middle
					await this.Scramble(nextLocationMiddle, middleLocation, currentThreadContext.intBufferSpan, currentThreadContext.pageWorkspaceBufferSpan, currentThreadContext).ConfigureAwait(false);

					// end
					await this.Scramble(nextLocationEnd, cacheChunks - 1, currentThreadContext.intBufferSpan, currentThreadContext.pageWorkspaceBufferSpan, currentThreadContext).ConfigureAwait(false);
				}

				if(this.rulesSet.Features.HasFlag(THSRulesSet.THSFeatures.HashPages)) {
					// now hash the page
					using SafeArrayHandle pageHash = currentThreadContext.thsHashSet.Hash512(currentThreadContext.pageWorkspaceBuffer);

					// and copy it back
					pageHash.CopyTo(currentThreadContext.pageWorkspaceBuffer, 0, currentThreadContext.pageWorkspaceBuffer.Length - HASH_SIZE, HASH_SIZE);
				}

				// step 4, save back to the scratchpad if we are not at the end
				if(page < (ulong) (this.ComparisionSize - 1)) {

					await currentThreadContext.scratchpad.CopyFrom(currentThreadContext.pageWorkspaceBuffer.Entry, 0, (long) pageOffset, this.PageMemorySize).ConfigureAwait(false);
				}
			}

			// assemble the final hash
			using SafeArrayHandle hashBuffer = SafeArrayHandle.Create(HASH_SIZE * 2);

			TypeSerializer.Deserialize(currentThreadContext.pageWorkspaceBufferSpan.Slice(sizeof(ulong) * 2, sizeof(ulong)), out ulong pageMarker);
			ulong innerPageOffset = (pageMarker & (ulong) (this.PageMemorySize - 1));
			innerPageOffset = Math.Min(innerPageOffset, (ulong) this.PageMemorySize - HASH_SIZE);
			currentThreadContext.pageWorkspaceBuffer.CopyTo(hashBuffer, (int) innerPageOffset, 0, HASH_SIZE);
			currentThreadContext.pageWorkspaceBuffer.CopyTo(hashBuffer, currentThreadContext.pageWorkspaceBuffer.Length - HASH_SIZE, HASH_SIZE, HASH_SIZE);

			using SafeArrayHandle hash = currentThreadContext.thsHashSet.Hash512(hashBuffer);

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
		private async Task Scramble(ulong nextLocation, int chunk, Memory<byte> bufferSpan, Memory<byte> cacheBufferSpan, THSThreadContext currentThreadContext) {

			int offset = chunk * sizeof(ulong);
			await currentThreadContext.scratchpad.CopyTo(currentThreadContext.intBuffer.Entry, ((long) nextLocation * this.PageMemorySize) + offset, 0, sizeof(ulong)).ConfigureAwait(false);
			TypeSerializer.Deserialize(bufferSpan, out ulong cache64A);

			cacheBufferSpan.Slice(offset, sizeof(ulong)).CopyTo(bufferSpan);
			TypeSerializer.Deserialize(bufferSpan, out ulong cache64B);

			cache64B ^= cache64A;
			TypeSerializer.Serialize(cache64B, bufferSpan);
			bufferSpan.Slice(0, sizeof(ulong)).CopyTo(cacheBufferSpan.Slice(offset, sizeof(ulong)));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private SafeArrayHandle GetNoncedHash(BigInteger hash, long nonce, THSThreadContext currentThreadContext) {

			int byteCount = hash.GetByteCount();
			using ByteArray hashBytes = ByteArray.Create(byteCount);
			hash.TryWriteBytes(hashBytes.Span, out int bytesWritten);

			Span<byte> nonceBytes = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize(nonce, nonceBytes);

			using SafeArrayHandle message = SafeArrayHandle.Create(hashBytes.Length + nonceBytes.Length);

			//arbitrary put together
			message.Entry.CopyFrom(hashBytes);
			message.Entry.CopyFrom(nonceBytes, 0, hashBytes.Length, nonceBytes.Length);

			return currentThreadContext.thsHashSet.Hash(message);
		}

		/// <summary>
		///     create a scratchpad with backup methods
		/// </summary>
		/// <exception cref="IOException"></exception>
		/// <exception cref="OutOfMemoryException"></exception>
		private async Task<IVeryLargeByteArray> CreateScratchpad() {
			long size = this.MainBufferDataSizeX2 - (int) this.PageSize;
			IVeryLargeByteArray scratchpad = null;

			async Task CreateHDDScratchpad() {
				try {
					scratchpad = new FileMappedLargeByteArray(size, this.memoryType == Enums.THSMemoryTypes.HDD_DB);

					try {
						await scratchpad.Initialize().ConfigureAwait(false);
					} catch {
						scratchpad?.Dispose();
						scratchpad = null;

						throw;
					}
				} catch(IOException e) {
					scratchpad = new FileMappedLargeByteArray(size, this.memoryType == Enums.THSMemoryTypes.HDD);

					try {
						await scratchpad.Initialize().ConfigureAwait(false);
					} catch {
						scratchpad?.Dispose();
						scratchpad = null;

						throw;
					}
				}
			}

			async Task CreateRAMScratchpad() {
				scratchpad = new VeryLargeByteArray(size);

				try {
					await scratchpad.Initialize().ConfigureAwait(false);
				} catch {
					scratchpad?.Dispose();
					scratchpad = null;

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

			return scratchpad;
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {

				for(int i = 0; i < this.threadCount; i++) {
					this.threadContexts[i].Dispose();
				}
			}

			this.IsDisposed = true;
		}

		~THSEngine() {
			this.Dispose(false);
		}

	#endregion

	}
}