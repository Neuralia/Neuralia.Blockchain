#if NETCOREAPP3_1
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	public unsafe class XChaChaAvx : IXChaCha {
		public const int CHACHA_DEFAULT_ROUNDS = 20;
		private const int NONCE_SIZE_IN_BYTES = 24;
		private const int BLOCK_SIZE_IN_INTS = 16;
		private const int KEY_SIZE_IN_INTS = 8;

		public const int NonceSizeInBytes = NONCE_SIZE_IN_BYTES;
		public const int KeySizeInBytes = KEY_SIZE_IN_INTS * sizeof(int);
		public static int BlockSizeInBytes = BLOCK_SIZE_IN_INTS * sizeof(int);

		public static uint[] SIGMA = {0x61707865, 0x3320646E, 0x79622D32, 0x6B206574}; //Encoding.ASCII.GetBytes("expand 32-byte k");

		private readonly ByteArray initialStateBuffer;
		
		private int rounds;
		private readonly uint* state;

		private readonly ByteArray stateBuffer;
		private readonly ClosureWrapper<MemoryHandle> stateHandle;

		public static bool IsSupported => XChaChaAvxComponents.IsSupported;

		public XChaChaAvx(int rounds = CHACHA_DEFAULT_ROUNDS) {

			if(!IsSupported) {
				throw new NotSupportedException();
			}
			
			if((rounds & 3) != 0) {
				// we unroll the loops by 4, so it must be multiple of 4
				throw new ArgumentException($"The rounds value {rounds} parameter must be a multiple of 4");
			}
			this.rounds = rounds;
			this.stateBuffer = ByteArray.Create<uint>(BLOCK_SIZE_IN_INTS);
			this.stateHandle = this.stateBuffer.Memory.Pin();
			this.state = (uint*) this.stateHandle.Value.Pointer;

			this.initialStateBuffer = ByteArray.Create<uint>(BLOCK_SIZE_IN_INTS);
		}

		public void SetRounds(int rounds) {
			this.rounds = rounds;
		}
		
		/// <summary>
		///     avx optimized chacha
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void PerformChaChaRounds() {
			XChaChaAvxComponents.PerformChaChaRounds(this.state, this.rounds);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SetSigma() {
			this.state[0] = SIGMA[0];
			this.state[1] = SIGMA[1];
			this.state[2] = SIGMA[2];
			this.state[3] = SIGMA[3];
		}

		/// <summary>
		///     prepare the starting state
		/// </summary>
		/// <param name="nonce"></param>
		/// <param name="key"></param>
		private void PrepareInitialState(ByteArray nonce, ByteArray key) {
			// See https://tools.ietf.org/html/draft-arciszewski-xchacha-01#section-2.2.

			// The first four words (0-3) are constants: 0x61707865, 0x3320646e, 0x79622d32, 0x6b206574.
			this.SetSigma();

			// The next eight words (4-11) are taken from the 256-bit key in little-endian order, in 4-byte chunks; and the first 16 bytes of the 24-byte nonce to obtain the subkey.

			// Set 256-bit Key
			key.CopyTo(this.stateBuffer, 0, 16, 32);

			// Set 128-bit Nonce
			nonce.CopyTo(this.stateBuffer, 0, 12 * sizeof(uint), sizeof(uint) * 4);

			// Block function
			this.PerformChaChaRounds();

			this.stateBuffer.CopyTo(this.stateBuffer, 12*sizeof(uint), 4*sizeof(uint), 4*sizeof(uint));

			this.state[12] = 0;

			// Words 13-15 are a nonce, which must not be repeated for the same key.
			// The 13th word is the first 32 bits of the input nonce taken as a little-endian integer, while the 15th word is the last 32 bits.
			nonce.CopyTo(this.stateBuffer, 0, 13 * sizeof(uint), sizeof(uint) * 3);

			this.stateBuffer.CopyTo(this.initialStateBuffer);
		}

		private void SetInitialState(int counter) {

			if(counter == 0) {
				return;
			}

			// copy to the state
			this.initialStateBuffer.CopyTo(this.stateBuffer);

			// Word 12 is a block counter. Since each block is 64-byte, a 32-bit word is enough for 256 gigabytes of data. Ref: https://tools.ietf.org/html/rfc8439#section-2.3.
			this.state[12] = (uint) counter;
		}

		private void ProcessKeyStreamBlock(int counter) {

			// Set the initial state based on https://tools.ietf.org/html/rfc8439#section-2.3

			this.SetInitialState(counter);

			// Create a copy of the state and then run 20 rounds on it,
			// alternating between "column rounds" and "diagonal rounds"; each round consisting of four quarter-rounds.

			this.PerformChaChaRounds();
		}

		private void Process(ByteArray nonce, ByteArray key, ByteArray input, ByteArray output, int? length = null) {
			if(input == null) {
				throw new ArgumentNullException(nameof(input));
			}

			if(output == null) {
				throw new ArgumentNullException(nameof(output));
			}

			int len = length.HasValue?length.Value:output.Length;
			if(output.Length < len) {
				throw new ArgumentException("The output buffer is not large enough.");
			}

			if(nonce == null) {
				throw new ArgumentNullException(nameof(nonce));
			}

			if(nonce.IsEmpty || (nonce.Length != NONCE_SIZE_IN_BYTES)) {
				throw new ArgumentException($"Nonce current length {nonce.Length}, it has to be {NONCE_SIZE_IN_BYTES}");
			}

			if(key.IsEmpty || (key.Length != KeySizeInBytes)) {
				throw new ArgumentException($"Key current length {key.Length}, it has to be {KeySizeInBytes}");
			}
			
			int numBlocks = len >> 6;
			int remainders = len & 0x3F;

			this.PrepareInitialState(nonce, key);

			fixed(byte* inputPtr = input.Span) {
				fixed(byte* outputPtr = output.Span) {
					for(var i = 0; i < numBlocks; i++) {
						this.ProcessKeyStreamBlock(i);
						this.Xor(inputPtr, outputPtr, i);
					}

					if(remainders != 0) {
						this.ProcessKeyStreamBlock(numBlocks);
						this.Xor(inputPtr, outputPtr, remainders, numBlocks); // last block
					}
				}
			}
		}

		/// <summary>
		///     64 bytes (full block) xor
		/// </summary>
		/// <param name="output"></param>
		/// <param name="input"></param>
		/// <param name="curBlock"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Xor(byte* input, byte* output, int curBlock) {
			
			if(Avx2.IsSupported) {
				XChaChaAvxComponents.XorBlock64(input, output, curBlock, this.state);
			} else if(Sse2.IsSupported) {
				XChaChaAvxComponents.XorBlock32(input, output, curBlock, this.state);
			} else {
				
				int blockOffset = curBlock * XChaCha.BLOCK_SIZE_IN_BYTES;

				var block = (byte*) this.state;
				for(var i = 0; i < XChaCha.BLOCK_SIZE_IN_BYTES; i += 8) {
					output[i + 0 + blockOffset] = (byte) (input[i + 0 + blockOffset] ^ block[i + 0]);
					output[i + 1 + blockOffset] = (byte) (input[i + 1 + blockOffset] ^ block[i + 1]);
					output[i + 2 + blockOffset] = (byte) (input[i + 2 + blockOffset] ^ block[i + 2]);
					output[i + 3 + blockOffset] = (byte) (input[i + 3 + blockOffset] ^ block[i + 3]);
					output[i + 4 + blockOffset] = (byte) (input[i + 4 + blockOffset] ^ block[i + 4]);
					output[i + 5 + blockOffset] = (byte) (input[i + 5 + blockOffset] ^ block[i + 5]);
					output[i + 6 + blockOffset] = (byte) (input[i + 6 + blockOffset] ^ block[i + 6]);
					output[i + 7 + blockOffset] = (byte) (input[i + 7 + blockOffset] ^ block[i + 7]);
				}
			}
		}

		/// <summary>
		///     dynamic sized xor
		/// </summary>
		/// <param name="output"></param>
		/// <param name="input"></param>
		/// <param name="len"></param>
		/// <param name="curBlock"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Xor(byte* input, byte* output, int len, int curBlock) {
			XChaChaAvxComponents.XorDynamic(input, output, curBlock, len, this.state);
		}

		public void Encrypt(SafeArrayHandle plaintext, SafeArrayHandle nonce, SafeArrayHandle key, SafeArrayHandle ciphertext, int? length = null) {
			this.Process(nonce.Entry, key.Entry, plaintext.Entry, ciphertext.Entry, length);
		}

		public void Decrypt(SafeArrayHandle ciphertext, SafeArrayHandle nonce, SafeArrayHandle key, SafeArrayHandle plaintext, int? length = null) {
			this.Process(nonce.Entry, key.Entry, ciphertext.Entry, plaintext.Entry, length);
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.stateHandle.Value.Dispose();
				this.stateBuffer.Dispose();

				this.initialStateBuffer.Dispose();
			}

			this.IsDisposed = true;
		}

		~XChaChaAvx() {
			this.Dispose(false);
		}

	#endregion

	}
}
#endif