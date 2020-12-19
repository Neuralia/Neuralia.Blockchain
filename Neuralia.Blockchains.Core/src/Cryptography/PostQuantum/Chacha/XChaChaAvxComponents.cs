#if NETCOREAPP3_1

using System;
using System.Runtime.CompilerServices;
//using System.Runtime.Intrinsics;
//using System.Runtime.Intrinsics.X86;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Tools.General;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	
	/// <summary>
	/// concentration of Avx Operations to maximize optimizations
	/// </summary>
	internal static unsafe class XChaChaAvxComponents {
		
		private static readonly ClosureWrapper<Vector128<byte>> Mask1;
		private static readonly ClosureWrapper<Vector128<byte>> Mask2;
		private static readonly ClosureWrapper<Vector128<byte>> Mask3;
		
		public static bool IsSupported => Sse2.IsSupported || !Ssse3.IsSupported;
		static XChaChaAvxComponents() {

			if(IsSupported) {
				Mask1 = new ClosureWrapper<Vector128<byte>>();
				Mask2 = new ClosureWrapper<Vector128<byte>>();
				Mask3 = new ClosureWrapper<Vector128<byte>>();

				// build the AVX diagonals shuffling masks
				byte[] mask = {4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 3};

				fixed(byte* ptr = mask.AsSpan()) {
					Mask1.Value = Sse2.LoadVector128(ptr);
				}

				mask = new byte[] {8, 9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6, 7};

				fixed(byte* ptr = mask.AsSpan()) {
					Mask2.Value = Sse2.LoadVector128(ptr);
				}

				mask = new byte[] {12, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11};

				fixed(byte* ptr = mask.AsSpan()) {
					Mask3.Value = Sse2.LoadVector128(ptr);
				}
			}
		}
		
		/// <summary>
		///     avx optimized chacha
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PerformChaChaRounds(uint* state, int rounds) {
			Vector128<uint> t0 = Sse2.LoadVector128(state);
			Vector128<uint> t4 = Sse2.LoadVector128(state + 4);
			Vector128<uint> t8 = Sse2.LoadVector128(state + 8);
			Vector128<uint> t12 = Sse2.LoadVector128(state + 12);

			for(int i = rounds >> 2; i != 0; i--) { // 20 rounds, 4 rounds per loop. 
				QuarterRoundAvx(ref t0, ref t4, ref t8, ref t12);
				t4 = Ssse3.Shuffle(t4.AsByte(), Mask1).As<byte, uint>();
				t8 = Ssse3.Shuffle(t8.AsByte(), Mask2).As<byte, uint>();
				t12 = Ssse3.Shuffle(t12.AsByte(), Mask3).As<byte, uint>();
				QuarterRoundAvx(ref t0, ref t4, ref t8, ref t12);
				
				QuarterRoundAvx(ref t0, ref t4, ref t8, ref t12);
				t4 = Ssse3.Shuffle(t4.AsByte(), Mask1).As<byte, uint>();
				t8 = Ssse3.Shuffle(t8.AsByte(), Mask2).As<byte, uint>();
				t12 = Ssse3.Shuffle(t12.AsByte(), Mask3).As<byte, uint>();
				QuarterRoundAvx(ref t0, ref t4, ref t8, ref t12);
			}

			Sse2.Store(state, t0);
			Sse2.Store(state + 4, t4);
			Sse2.Store(state + 8, t8);
			Sse2.Store(state + 12, t12);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void QuarterRoundAvx(ref Vector128<uint> a, ref Vector128<uint> b, ref Vector128<uint> c, ref Vector128<uint> d) {
			a = Sse2.Add(a, b);
			d = RotateLeftAvx(Sse2.Xor(d, a), 16);
			c = Sse2.Add(c, d);
			b = RotateLeftAvx(Sse2.Xor(b, c), 12);
			a = Sse2.Add(a, b);
			d = RotateLeftAvx(Sse2.Xor(d, a), 8);
			c = Sse2.Add(c, d);
			b = RotateLeftAvx(Sse2.Xor(b, c), 7);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Vector128<uint> RotateLeftAvx(Vector128<uint> value, int offset) {
			Vector128<uint> temp1 = Sse2.ShiftLeftLogical(value, (byte) offset);
			Vector128<uint> temp2 = Sse2.ShiftRightLogical(value, (byte) (32 - offset));

			return Sse2.Or(temp1, temp2);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void XorBlock64(byte* input, byte* output, int curBlock,uint* state ) {
			int blockOffset = curBlock * XChaCha.BLOCK_SIZE_IN_BYTES;

			var block = (byte*) state;
			
			// first 32 bits
			Vector256<uint> blockVector = Avx.LoadVector256((uint*) block);
			Vector256<uint> inputVector = Avx.LoadVector256((uint*) (input + blockOffset));
			Vector256<uint> outputVector = Avx2.Xor(inputVector, blockVector);
			Avx.Store((uint*) (output + blockOffset), outputVector);

			// last 32 bits
			blockVector = Avx.LoadVector256((uint*) (block + 32));
			inputVector = Avx.LoadVector256((uint*) (input + blockOffset + 32));
			outputVector = Avx2.Xor(inputVector, blockVector);
			Avx.Store((uint*) (output + blockOffset + 32), outputVector);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void XorBlock32(byte* input, byte* output, int curBlock,uint* state ) {
			int blockOffset = curBlock * XChaCha.BLOCK_SIZE_IN_BYTES;

			var block = (byte*) state;
			
			// first 16 bits
			Vector128<byte> blockVector = Sse2.LoadVector128(block);
			Vector128<byte> inputVector = Sse2.LoadVector128(input + blockOffset);
			Vector128<byte> outputVector = Sse2.Xor(inputVector, blockVector);
			Sse2.Store(output + blockOffset, outputVector);

			// second 16 bits
			blockVector = Sse2.LoadVector128(block + 16);
			inputVector = Sse2.LoadVector128(input + blockOffset + 16);
			outputVector = Sse2.Xor(inputVector, blockVector);
			Sse2.Store(output + blockOffset + 16, outputVector);

			// third 16 bits
			blockVector = Sse2.LoadVector128(block + 32);
			inputVector = Sse2.LoadVector128(input + blockOffset + 32);
			outputVector = Sse2.Xor(inputVector, blockVector);
			Sse2.Store(output + blockOffset + 32, outputVector);

			// fourth 16 bits
			blockVector = Sse2.LoadVector128(block + 48);
			inputVector = Sse2.LoadVector128(input + blockOffset + 48);
			outputVector = Sse2.Xor(inputVector, blockVector);
			Sse2.Store(output + blockOffset + 48, outputVector);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void XorDynamic(byte* input, byte* output, int curBlock, int len,uint* state ) {
			int blockOffset = curBlock * XChaCha.BLOCK_SIZE_IN_BYTES;

			var block = (byte*) state;
			int remaining = len;
			var offset = 0;

			int blocks32 = remaining >> 5;

			if(blocks32 != 0) {
				for(var i = 0; i < blocks32; i++) {
					Vector256<uint> blockVector = Avx.LoadVector256((uint*) block + offset);
					Vector256<uint> inputVector = Avx.LoadVector256((uint*) (input + blockOffset + offset));
					Vector256<uint> outputVector = Avx2.Xor(inputVector, blockVector);
					Avx.Store((uint*) (output + blockOffset + offset), outputVector);
					remaining -= 32;
					offset += 32;
				}
			}

			int blocks16 = remaining >> 4;

			if(blocks16 != 0) {

				for(var i = 0; i < blocks16; i++) {
					Vector128<byte> blockVector = Sse2.LoadVector128(block + offset);
					Vector128<byte> inputVector = Sse2.LoadVector128(input + blockOffset + offset);
					Vector128<byte> outputVector = Sse2.Xor(inputVector, blockVector);
					Sse2.Store(output + blockOffset + offset, outputVector);
					remaining -= 16;
					offset += 16;

				}
			}

			for(var i = 0; i < remaining; i++) {
				output[i + blockOffset + offset] = (byte) (input[i + blockOffset + offset] ^ block[i + offset]);
			}
		}
	}
}
#endif