using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography {
	public class AesSearchPow : IDisposable2 {

		private const int AES_BLOCK_SIZE = 16;

		private const int AES_KEY_SIZE = 32;

		private const int MAIN_BUFFER_DATA_SIZE = 20; //2^30 = 1GB. 2^20 = 1MB

		private const int L2_CACHE_TARGET = 12; // 2^12 = 4096 bytes // must be less than MAIN_BUFFER_DATA_SIZE. no smaller than 6

		private const int MAIN_BUFFER_DATA_CHUNK_SIZE = 6; //2^6 = 64 bytes //must be same as SHA512_DIGEST_LENGTH 64

		private const int AES_ITERATIONS = 15;

		private const int CHUNKS = 1 << (MAIN_BUFFER_DATA_SIZE - MAIN_BUFFER_DATA_CHUNK_SIZE); //2^(30-6) = 16 mil

		private const int CHUNK_SIZE = 1 << MAIN_BUFFER_DATA_CHUNK_SIZE; //2^6 = 64 bytes
		private const int CACHE_MEMORY_SIZE = 1 << L2_CACHE_TARGET; //2^12 = 4096 bytes

		private const int MAIN_BUFFER_DATA_SIZEX2 = 1 << MAIN_BUFFER_DATA_SIZE; //2^30 = 1GB

		private const int COMPARISON_SIZE = 1 << (MAIN_BUFFER_DATA_SIZE - L2_CACHE_TARGET); //2^(30-12) = 256K

		private const int NUM_SOLUTIONS_TO_RETURN = 3;

		private readonly bool enableMacroDiagnostics;
		private readonly bool enableMicroDiagnostics;

		public AesSearchPow() {
			this.enableMacroDiagnostics = true;
			this.enableMicroDiagnostics = true;
		}

		public AesSearchPow(bool enableMacroDiagnostics, bool enableMicroDiagnostics) {
			this.enableMacroDiagnostics = enableMacroDiagnostics;
			this.enableMicroDiagnostics = enableMicroDiagnostics;
		}

		private int GetNumThreads(Enums.ThreadMode threadMode) {
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

		public (List<int> solutions, int nonce) PerformPow(IByteArray root, int hashTargetDifficulty, Action<int, int> iteration = null) {

			IByteArray rootHash = this.Sha3(root);

			BigInteger hash = new BigInteger(rootHash.ToExactByteArrayCopy().Concat(new byte[] {0}).ToArray());

			BigInteger hashTarget = HashDifficultyUtils.GetHash256TargetByIncrementalDifficulty(hashTargetDifficulty);
			Log.Verbose("Difficulty: {0}", hashTargetDifficulty);
			Log.Verbose("target: {0}", hashTarget);

			int nThreads = this.GetNumThreads(Enums.ThreadMode.Quarter);
			IByteArray scratchpad = MemoryAllocators.Instance.cryptoAllocator.Take(MAIN_BUFFER_DATA_SIZEX2);

			int nonce = 1;

			var solutions = new List<int>();

			Stopwatch outerStopwatch = null;

			Stopwatch innerStopwatch = null;

			if(this.enableMacroDiagnostics) {
				outerStopwatch = new Stopwatch();
				outerStopwatch.Start();
			}

			if(this.enableMicroDiagnostics) {
				innerStopwatch = new Stopwatch();
			}

			xxHasher32 hasher = new xxHasher32();

			while(true) {
				Log.Verbose("Performing proof of work for Nonce {0}", nonce);

				if(this.enableMicroDiagnostics) {
					innerStopwatch?.Reset();
					innerStopwatch?.Start();
				}

				// alert we are running an iteration
				iteration?.Invoke(nonce, hashTargetDifficulty);

				var results = this.FindBestPatternHash(out int collisions, hash, scratchpad, nThreads, nonce);

				if(this.enableMicroDiagnostics) {
					innerStopwatch?.Stop();
					Log.Verbose("Single nonce search took {0}", innerStopwatch?.Elapsed);
				}

				int count = 0;

				foreach(BigInteger result in results) {
					if(result < hashTarget) {
						Log.Verbose("Found pre hash result: {0}", result);
						solutions.Add(hasher.Hash(result.ToByteArray()));
						count++;

						if(count == NUM_SOLUTIONS_TO_RETURN) {
							break;
						}
					}
				}

				if(solutions.Count > 0) {
					break;
				}

				nonce += 1;
			}

			scratchpad.Return();

			if(this.enableMacroDiagnostics) {
				outerStopwatch.Stop();
				Log.Verbose("Entire proof of work took {0}", outerStopwatch.Elapsed);
			}

			Log.Verbose("Found {0} solutions!", solutions.Count);

			return (solutions.Take(NUM_SOLUTIONS_TO_RETURN).ToList(), nonce);
		}

		/// <summary>
		///     here we verify the POW by running it for the passed parameters
		/// </summary>
		/// <param name="nonce"></param>
		/// <param name="hashTargetDifficulty"></param>
		/// <param name="solutions"></param>
		/// <returns></returns>
		public bool Verify(IByteArray root, int nonce, int hashTargetDifficulty, List<int> solutions, Enums.ThreadMode threadMode) {

			IByteArray rootHash = this.Sha3(root);

			BigInteger hash = new BigInteger(rootHash.ToExactByteArray().Concat(new byte[] {0}).ToArray());

			rootHash.Return();

			BigInteger hashTarget = HashDifficultyUtils.GetHash256TargetByIncrementalDifficulty(hashTargetDifficulty);
			Log.Verbose("target: {0}", hashTarget);

			Log.Verbose("Difficulty: {0}", hashTargetDifficulty);

			int nThreads = this.GetNumThreads(threadMode);

			IByteArray scratchpad = MemoryAllocators.Instance.cryptoAllocator.Take(MAIN_BUFFER_DATA_SIZEX2);

			var currentSolutions = new List<int>();

			Log.Verbose("Performing proof of work verification for Nonce {0}", nonce);

			var results = this.FindBestPatternHash(out int collisions, hash, scratchpad, nThreads, nonce);

			xxHasher32 hasher = new xxHasher32();

			int count = 0;

			foreach(BigInteger result in results) {
				if(result < hashTarget) {

					currentSolutions.Add(hasher.Hash(result.ToByteArray()));
					count++;

					if(count == NUM_SOLUTIONS_TO_RETURN) {
						break;
					}
				}
			}

			scratchpad.Return();

			currentSolutions = currentSolutions.Take(NUM_SOLUTIONS_TO_RETURN).ToList();

			if(solutions.Count != currentSolutions.Count) {

				return false;
			}

			// lets return if they match
			return solutions.SequenceEqual(currentSolutions);
		}

		private List<BigInteger> FindBestPatternHash(out int collisions, BigInteger hash, IByteArray scratchpad, int nThreads, int nonce) {
			var results = new List<BigInteger>();

			collisions = 0;

			// hash the transaction header
			IByteArray dataHash = this.GetHash(hash, nonce);
			Log.Verbose("current hash {0}", dataHash.ToBase58());

			var searchResults = this.pattern_search(nonce, dataHash, scratchpad, nThreads);

			dataHash.Return();

			collisions = searchResults.Count();

			IByteArray result = MemoryAllocators.Instance.cryptoAllocator.Take(sizeof(long) + sizeof(uint));

			Span<byte> startLocBytes = stackalloc byte[sizeof(long)];
			Span<byte> finalCalculationBytes = stackalloc byte[sizeof(uint)];

			for(int i = 0; i < searchResults.Count(); i++) {
				long startLocation = searchResults[i].Item1;
				uint finalCalculation = searchResults[i].Item2;

				//arbitrary put together

				TypeSerializer.Serialize(startLocation, startLocBytes);
				result.CopyFrom(startLocBytes);

				TypeSerializer.Serialize(finalCalculation, finalCalculationBytes);
				result.CopyFrom(finalCalculationBytes, 0, sizeof(long), sizeof(uint));

				IByteArray hashres = this.Sha3_256(result);

				results.Add(new BigInteger(hashres.ToExactByteArray().Concat(new byte[] {0}).ToArray())); // add a 0 to make sure the results are positive

				hashres.Return();
			}

			result.Return();

			return results;
		}

		private List<(long, uint)> pattern_search(int nonce, IByteArray hash, IByteArray mainBuffer, int totalThreads) {
			// create many threads
			var results = new List<(long, uint)>();

			var tasks = new Task[totalThreads];

			mainBuffer.Clear();

			IByteArray hashWorkSpace = MemoryAllocators.Instance.cryptoAllocator.Take(64 * 3);

			IByteArray buffer = MemoryAllocators.Instance.cryptoAllocator.Take(sizeof(long));

			TypeSerializer.Serialize(nonce, buffer.Span);
			IByteArray nonceHash = this.Sha3(buffer);
			buffer.Return();

			hashWorkSpace.CopyFrom(hash);
			hashWorkSpace.CopyFrom(nonceHash, 64);

			nonceHash.Return();

			// prepare tasks
			for(int i = 0; i < totalThreads; i++) {
				int index = i;

				tasks[i] = new Task(() => {
					this.Sha512Filler(nonce, mainBuffer, index, totalThreads, hashWorkSpace);
				});
			}

			//start them all
			for(int i = 0; i < totalThreads; i++) {
				tasks[i].Start();
			}

			//wait for it all to finish
			Task.WaitAll(tasks.ToArray());

			hashWorkSpace.Return();

			var aesTasks = new Task<List<(long, uint)>>[totalThreads];

			for(int i = 0; i < totalThreads; i++) {
				int index = i;

				aesTasks[i] = new Task<List<(long, uint)>>(() => this.AesSearch(mainBuffer, index, totalThreads));
			}

			for(int i = 0; i < totalThreads; i++) {
				aesTasks[i].Start();
			}

			Task.WaitAll(aesTasks.Cast<Task>().ToArray());

			foreach(var task in aesTasks) {
				results.AddRange(task.Result);
			}

			return results;
		}

		private List<(long, uint)> AesSearch(IByteArray mainBuffer, int threadNumber, int totalThreads) {
			var results = new List<(long, uint)>();

			IByteArray cache = MemoryAllocators.Instance.cryptoAllocator.Take(CACHE_MEMORY_SIZE);
			IByteArray encrypted = MemoryAllocators.Instance.cryptoAllocator.Take(CACHE_MEMORY_SIZE);

			IByteArray data64 = MemoryAllocators.Instance.cryptoAllocator.Take(8);

			IByteArray key = MemoryAllocators.Instance.cryptoAllocator.Take(AES_KEY_SIZE);
			IByteArray iv = MemoryAllocators.Instance.cryptoAllocator.Take(AES_BLOCK_SIZE);

			long searchNumber = COMPARISON_SIZE / totalThreads;
			long startLoc = threadNumber * searchNumber;

			long remainder = 0;

			if((threadNumber + 1) == totalThreads) {
				remainder = CHUNKS % totalThreads;
			}

			for(long k = startLoc; k < (startLoc + searchNumber + remainder); k++) {
				cache.CopyFrom(mainBuffer, (int) (k * CACHE_MEMORY_SIZE), 0, CACHE_MEMORY_SIZE);

				uint cache32 = 0;

				for(int j = 0; j < AES_ITERATIONS; j++) {
					// use the last 4 bytes as an int
					TypeSerializer.Deserialize(cache, (int) ((CACHE_MEMORY_SIZE / sizeof(int)) - 1L), out cache32);
					long nextLocation = cache32 % COMPARISON_SIZE;

					for(int i = 0; i < (CACHE_MEMORY_SIZE / sizeof(long)); i++) {

						int offset = i * sizeof(ulong);

						data64.CopyFrom(mainBuffer, (int) (nextLocation * CACHE_MEMORY_SIZE) + offset, data64.Length);
						TypeSerializer.Deserialize(data64, 0, out ulong cache64A);

						data64.CopyFrom(mainBuffer, offset, data64.Length);
						TypeSerializer.Deserialize(data64, 0, out ulong cache64B);

						cache64B ^= cache64A;
						TypeSerializer.Serialize(cache64B, data64.Span);

						mainBuffer.CopyFrom(data64, offset);
					}

					key.CopyFrom(cache, CACHE_MEMORY_SIZE - AES_KEY_SIZE, 0, key.Length);
					iv.CopyFrom(cache, CACHE_MEMORY_SIZE - AES_BLOCK_SIZE, 0, iv.Length);

					this.AesEncryptStringToBytes(cache, encrypted, key, iv);

					cache.CopyFrom(encrypted, 0, 0, encrypted.Length);
				}

				TypeSerializer.Deserialize(cache, (CACHE_MEMORY_SIZE / sizeof(int)) - 1, out cache32);

				if((cache32 % COMPARISON_SIZE) < (COMPARISON_SIZE / sizeof(int))) {
					// check solution
					TypeSerializer.Deserialize(cache, (CACHE_MEMORY_SIZE / sizeof(int)) - 2, out cache32);

					results.Add((k, cache32)); // set proof of calculation
				}
			}

			cache.Return();
			encrypted.Return();
			data64.Return();
			key.Return();
			iv.Return();

			return results;
		}

		private void AesEncryptStringToBytes(IByteArray message, IByteArray encrypted, IByteArray key, IByteArray iv) {
			using(Aes aesAlg = Aes.Create()) {
				aesAlg.Key = key.ToExactByteArrayCopy();
				aesAlg.IV = iv.ToExactByteArrayCopy();

				aesAlg.Mode = CipherMode.CBC;

				using(ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV)) {

					// Create the streams used for encryption. 
					using(MemoryStream msEncrypt = new MemoryStream()) {
						using(CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write)) {
							csEncrypt.Write(message.Bytes, message.Offset, message.Length);
							msEncrypt.Position = 0;
							msEncrypt.Read(encrypted.Bytes, encrypted.Offset, encrypted.Length);
						}
					}
				}
			}
		}

		private void Sha512Filler(int nonce, IByteArray mainBuffer, int threadNumber, int totalThreads, IByteArray hashWorkSpace) {
			long chunksToProcess = CHUNKS / (uint) totalThreads;
			long startChunk = threadNumber * chunksToProcess;

			IByteArray localHashWorkSpace = MemoryAllocators.Instance.cryptoAllocator.Take(hashWorkSpace.Length);
			localHashWorkSpace.CopyFrom(hashWorkSpace);

			IByteArray data = mainBuffer;

			long remainder = 0;

			if((threadNumber + 1) == totalThreads) {
				remainder = CHUNKS % totalThreads;
			}

			using(Sha3ExternalDigest sha3 = new Sha3ExternalDigest(512)) {

				IByteArray buffer = MemoryAllocators.Instance.cryptoAllocator.Take(sizeof(long));

				for(long i = startChunk; i < (startChunk + chunksToProcess + remainder); i++) {

					TypeSerializer.Serialize(i, buffer.Span);
					IByteArray chunkHash = this.Sha3(sha3, buffer);
					localHashWorkSpace.CopyFrom(chunkHash, 64 * 2);
					chunkHash.Return();

					IByteArray resultingHash = this.Sha3(localHashWorkSpace);

					data.CopyFrom(resultingHash, 0, (int) (i * CHUNK_SIZE), resultingHash.Length);

					resultingHash.Return();
				}

				buffer.Return();
			}

			localHashWorkSpace.Return();
		}

		private IByteArray Sha512(IByteArray message) {

			using(SHA512 sha512 = new SHA512Managed()) {
				var hash = sha512.ComputeHash(message.ToExactByteArray());

				return ByteArray.CreateFrom(hash);
			}
		}

		private IByteArray Sha512(SHA512 sha512, IByteArray message) {

			var hash = sha512.ComputeHash(message.ToExactByteArray());

			return ByteArray.CreateFrom(hash);

		}

		private IByteArray Sha3_256(IByteArray message) {

			using(Sha3ExternalDigest digester = new Sha3ExternalDigest(256)) {
				digester.BlockUpdate(message, 0, message.Length);
				digester.DoFinalReturn(out IByteArray block);

				return block;
			}
		}

		private IByteArray Sha3(IByteArray message) {

			using(Sha3ExternalDigest digester = new Sha3ExternalDigest(512)) {
				digester.BlockUpdate(message, 0, message.Length);
				digester.DoFinalReturn(out IByteArray block);

				return block;
			}
		}

		private IByteArray Sha3(Sha3ExternalDigest digester, IByteArray message) {

			digester.BlockUpdate(message, 0, message.Length);
			digester.DoFinalReturn(out IByteArray block);

			return block;
		}

		private IByteArray GetHash(BigInteger hash, int nonce) {

#if (NETSTANDARD2_0)
			Span<byte> bytes = hash.ToByteArray();
			IByteArray hashbytes = MemoryAllocators.Instance.cryptoAllocator.Take(bytes.Length);
			bytes.CopyTo(hashbytes.Span);
#elif (NETCOREAPP2_2)
			int        byteCount = hash.GetByteCount();
			IByteArray hashbytes = MemoryAllocators.Instance.cryptoAllocator.Take(byteCount);
			hash.TryWriteBytes(hashbytes.Span, out int bytesWritten);

#else
	throw new NotImplementedException();
#endif

			Span<byte> noncebytes = stackalloc byte[sizeof(int)];
			TypeSerializer.Serialize(nonce, noncebytes);

			IByteArray message = MemoryAllocators.Instance.cryptoAllocator.Take(hashbytes.Length + noncebytes.Length);

			//arbitrary put together
			message.CopyFrom(hashbytes);
			message.CopyFrom(noncebytes, 0, hashbytes.Length, noncebytes.Length);

			IByteArray result = this.Sha3(message);

			message.Return();
			hashbytes.Return();

			return result;
		}

	#region dispose

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {
				try {
					try {

					} catch(Exception ex) {

					}
				} finally {
					this.IsDisposed = true;
				}
			}
		}

		~AesSearchPow() {
			this.Dispose(false);
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}

}