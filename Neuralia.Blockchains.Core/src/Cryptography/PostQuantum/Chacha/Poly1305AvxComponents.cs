#if NETCOREAPP3_1
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Neuralia.Blockchains.Core.General;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	internal static unsafe class Poly1305AvxComponents {
		
		/// <summary>
		/// concentration of Avx Operations to maximize optimizations
		/// </summary>
		static Poly1305AvxComponents() {
			
			if(Avx2.IsSupported) {
				uint[] mask = {1, 0, 1, 0, 1, 0, 1, 0};

				fixed(uint* ptr = mask.AsSpan()) {
					one = Avx.LoadVector256(ptr);
				}

				byte[] mask1 = {0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3};

				fixed(byte* ptr = mask1.AsSpan()) {
					Mask1 = Avx.LoadVector256(ptr);
				}

				byte[] mask2 = {4, 5, 6, 7, 4, 5, 6, 7, 4, 5, 6, 7, 4, 5, 6, 7, 4, 5, 6, 7, 4, 5, 6, 7, 4, 5, 6, 7, 4, 5, 6, 7};

				fixed(byte* ptr = mask2.AsSpan()) {
					Mask2 = Avx.LoadVector256(ptr);
				}

				byte[] mask3 = {8, 9, 10, 11, 8, 9, 10, 11, 8, 9, 10, 11, 8, 9, 10, 11, 8, 9, 10, 11, 8, 9, 10, 11, 8, 9, 10, 11, 8, 9, 10, 11};

				fixed(byte* ptr = mask3.AsSpan()) {
					Mask3 = Avx.LoadVector256(ptr);
				}

				byte[] mask4 = {12, 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15};

				fixed(byte* ptr = mask4.AsSpan()) {
					Mask4 = Avx.LoadVector256(ptr);
				}
			}
		}
		
		internal class AvxState {
			
			public ClosureWrapper<Vector256<uint>> rb0Vector;
			public ClosureWrapper<Vector256<uint>> rb1Vector;
			public ClosureWrapper<Vector256<uint>> rb2Vector;
			public ClosureWrapper<Vector256<uint>> rb3Vector;
			public ClosureWrapper<Vector256<uint>> rb4Vector;
		
			public ClosureWrapper<Vector256<uint>> d4Vector;
		}
		public static readonly ClosureWrapper<Vector256<uint>> zero = Vector256<uint>.Zero;
		public static readonly ClosureWrapper<Vector256<uint>> one;
		
		
		private static readonly Vector256<byte> Mask1;
		private static readonly Vector256<byte> Mask2;
		private static readonly Vector256<byte> Mask3;
		private static readonly Vector256<byte> Mask4;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetD4Zero(ref uint d4, AvxState avxstate) {
			d4 = 0;

			avxstate.d4Vector = zero;
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetD4One(ref uint d4, AvxState avxstate) {
			d4 = 1;

			avxstate.d4Vector = one;
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ProcessBlock(uint* dataPtr, uint* h, AvxState avxstate, ref uint h4, uint d4, uint r0, uint* ptr) {

		
			// here start with h + data (h + d). we dont do any carry propagation here
			// then the core, we have (h + d) * r. we dont do any carry propagation here
			// first part

			// load 4 uints
			Vector128<uint> loadedData128 = Sse2.LoadVector128(dataPtr);
			Vector256<uint> loadedData = loadedData128.ToVector256();

			// double them to fill the 256bit buffer
			Vector256<byte> fullData = Avx2.Permute2x128(loadedData, loadedData, 0).AsByte();

			// load 4 uints
			Vector128<uint> loadedH128 = Sse2.LoadVector128(h);
			Vector256<uint> loadedH = loadedH128.ToVector256();

			// double them to fill the 256bit buffer
			Vector256<byte> fullH = Avx2.Permute2x128(loadedH, loadedH, 0).AsByte();

			// get the first entry and copy it to 4 positions
			Vector256<uint> hVector = Avx2.Shuffle(fullH, Mask1).As<byte, uint>();
			Vector256<uint> dVector = Avx2.Shuffle(fullData, Mask1).As<byte, uint>();

			Vector256<ulong> temp1 = Avx2.Multiply(hVector, avxstate.rb0Vector.Value);
			Vector256<ulong> temp2 = Avx2.Multiply(dVector, avxstate.rb0Vector.Value);
			Vector256<ulong> itemp1 = Avx2.Add(temp1, temp2);

			// second part

			// get the second entry
			hVector = Avx2.Shuffle(fullH, Mask2).As<byte, uint>();
			dVector = Avx2.Shuffle(fullData, Mask2).As<byte, uint>();

			temp1 = Avx2.Multiply(hVector, avxstate.rb1Vector.Value);
			temp2 = Avx2.Multiply(dVector, avxstate.rb1Vector.Value);
			Vector256<ulong> itemp2 = Avx2.Add(temp1, temp2);

			// add it for the total
			itemp1 = Avx2.Add(itemp1, itemp2);

			// third part

			hVector = Avx2.Shuffle(fullH, Mask3).As<byte, uint>();
			dVector = Avx2.Shuffle(fullData, Mask3).As<byte, uint>();

			temp1 = Avx2.Multiply(hVector, avxstate.rb2Vector.Value);
			temp2 = Avx2.Multiply(dVector, avxstate.rb2Vector.Value);
			itemp2 = Avx2.Add(temp1, temp2);

			// add it for the total
			itemp1 = Avx2.Add(itemp1, itemp2);

			// fourth part

			hVector = Avx2.Shuffle(fullH, Mask4).As<byte, uint>();
			dVector = Avx2.Shuffle(fullData, Mask4).As<byte, uint>();

			temp1 = Avx2.Multiply(hVector, avxstate.rb3Vector.Value);
			temp2 = Avx2.Multiply(dVector, avxstate.rb3Vector.Value);
			itemp2 = Avx2.Add(temp1, temp2);

			// add it for the total
			itemp1 = Avx2.Add(itemp1, itemp2);

			// fifth part

			// this one we must fill manually
			ptr[Poly1305Avx.H_INDEX_0] = h4;
			ptr[Poly1305Avx.H_INDEX_1] = h4;

			Vector128<uint> hVector128 = Sse2.LoadVector128(ptr);
			hVector = hVector128.ToVector256();

			// copy to both sides
			hVector = Avx2.Permute2x128(hVector, hVector, 0);

			temp1 = Avx2.Multiply(hVector, avxstate.rb4Vector.Value);
			temp2 = Avx2.Multiply(avxstate.d4Vector.Value, avxstate.rb4Vector.Value);
			itemp2 = Avx2.Add(temp1, temp2);

			// add it for the final total
			Vector256<ulong> ig = Avx2.Add(itemp1, itemp2);

			ulong i0 = ig.GetElement(0);
			ulong i1 = ig.GetElement(1);
			ulong i2 = ig.GetElement(2);
			ulong i3 = ig.GetElement(3);

			// now do i4
			uint i4 = (h4 + d4) * (r0 & 3);

			// now we do a partial reduction using a modulo of 2^130 - 5
			var f5 = (uint) (i4 + (i3 >> 32));
			ulong f0 = ((f5 >> 2) * 5) + (i0 & 0xffffffff);
			ulong f1 = (f0 >> 32) + (i1 & 0xffffffff) + (i0 >> 32);
			ulong f2 = (f1 >> 32) + (i2 & 0xffffffff) + (i1 >> 32);
			ulong f3 = (f2 >> 32) + (i3 & 0xffffffff) + (i2 >> 32);
			ulong f4 = (f3 >> 32) + (f5 & 3);

			h[0] = (uint) f0;
			h[1] = (uint) f1;
			h[2] = (uint) f2;
			h[3] = (uint) f3;
			h4 = (uint) f4;
		}
	}
}
#endif