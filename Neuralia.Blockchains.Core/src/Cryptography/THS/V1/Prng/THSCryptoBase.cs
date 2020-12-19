using System;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Prng {
	public abstract class THSPrngBase : ITHSPrng {

		public const int BLOCK_SIZE_BYTES = 1 << 10;
		public const int BLOCK_SIZE_LONG = BLOCK_SIZE_BYTES >> 3;

		public abstract void InitializeSeed(ByteArray buffer);

		public unsafe void FillFull(byte* buffer, int blocksCount) {
			ulong* ptr = (ulong*)(buffer+(this.GetEntry() & 0xF));
			for(int i = 0; i < blocksCount; i++) {

				for(int k = 0; k < 2; k++) {
					int j = 0;
					ptr[j] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
					ptr[j++] = this.GetEntry();
				}

				ptr += BLOCK_SIZE_LONG;
			}
		}

		public unsafe void FillMedium(byte* buffer, int blocksCount) {

			var entry1 = this.GetEntry();
			var entry2 = this.GetEntry();
			var entry3 = this.GetEntry();
			var entry4 = this.GetEntry();
			var entry5 = this.GetEntry();
			
			var entry6 = entry1 ^ entry2;
			var entry7 = entry1 ^ entry3;
			var entry8 = entry4 ^ entry5;
			var entry9 = ~entry3 ^ entry4;
			var entry10 = ~entry2 & ~entry5;
			
			// take a small offset (0 to 8) and then set longs one over two
			ulong* ptr = (ulong*)(buffer+(entry1 & 0xF));
			int addition = (int)(entry2 & 0x1);
			for(int i = 0; i < blocksCount; i++) {
				
				ptr[0+addition] = entry1;
				ptr[2+addition] = entry2;
				ptr[4+addition] = entry3;
				ptr[6+addition] = entry4;
				ptr[8+addition] = entry5;
				
				ptr[10+addition] = entry6;
				ptr[12+addition] = entry7;
				ptr[14+addition] = entry8;
				ptr[16+addition] = entry9;
				ptr[18+addition] = entry10;
				
				ptr[20+addition] = entry10;
				ptr[22+addition] = entry9;
				ptr[24+addition] = entry8;
				ptr[26+addition] = entry7;
				ptr[28+addition] = entry6;
				
				ptr[30+addition] = entry5;
				ptr[32+addition] = entry4;
				ptr[34+addition] = entry3;
				ptr[36+addition] = entry2;
				ptr[48+addition] = entry2;
				
				ptr[50+addition] = entry3;
				ptr[52+addition] = entry4;
				ptr[54+addition] = entry5;
				ptr[56+addition] = entry6;
				ptr[58+addition] = entry7;
				
				ptr[60+addition] = entry8;
				ptr[62+addition] = entry9;
				ptr[64+addition] = entry2;
				ptr[66+addition] = entry10;
				ptr[68+addition] = entry1;
				
				ptr[70+addition] = entry2;
				ptr[72+addition] = entry10;
				ptr[74+addition] = entry9;
				ptr[76+addition] = entry1;
				ptr[78+addition] = entry4;
				
				ptr[80+addition] = entry7;
				ptr[82+addition] = entry5;
				ptr[84+addition] = entry8;
				ptr[86+addition] = entry3;
				ptr[88+addition] = entry6;
				
				ptr[90+addition] = entry10;
				ptr[92+addition] = entry4;
				ptr[94+addition] = entry8;
				ptr[96+addition] = entry1;
				ptr[98+addition] = entry3;
				
				ptr[100+addition] = entry6;
				ptr[102+addition] = entry7;
				ptr[104+addition] = entry9;
				ptr[106+addition] = entry5;
				ptr[108+addition] = entry2;
				
				ptr[110+addition] = entry3;
				ptr[112+addition] = entry1;
				ptr[114+addition] = entry9;
				ptr[116+addition] = entry5;
				ptr[118+addition] = entry2;
				
				ptr[120+addition] = entry10;
				ptr[122+addition] = entry8;
				ptr[124+addition] = entry6;

				ptr += BLOCK_SIZE_LONG;
			}
		}

		public unsafe void FillFast(byte* buffer, int blocksCount) {

			var entry1 = this.GetEntry();
			var entry2 = this.GetEntry();
			
			var entry3 = entry1 & entry2;
			
			var entry4 = entry1 ^ entry2;
			var entry5 = entry1 | ~entry2;
			var entry6 = ~entry1 ^ entry2;
			var entry7 = ~entry1 & ~entry2;
			
			// take a small offset (0 to 8) and then set longs one over two
			ulong* ptr = (ulong*)(buffer+(entry1 & 0xF));
			int addition = (int)(entry2 & 0x1);
			for(int i = 0; i < blocksCount; i++) {
				
				ptr[0+addition] = entry1;
				ptr[2+addition] = entry1;
				ptr[4+addition] = entry1;
				ptr[6+addition] = entry1;
				ptr[8+addition] = entry1;
				
				ptr[10+addition] = entry1;
				ptr[12+addition] = entry1;
				ptr[14+addition] = entry1;
				ptr[16+addition] = entry1;
				ptr[18+addition] = entry1;
				
				ptr[20+addition] = entry2;
				ptr[22+addition] = entry2;
				ptr[24+addition] = entry2;
				ptr[26+addition] = entry2;
				ptr[28+addition] = entry2;
				
				ptr[30+addition] = entry2;
				ptr[32+addition] = entry2;
				ptr[34+addition] = entry2;
				ptr[36+addition] = entry2;
				ptr[48+addition] = entry2;
				
				ptr[50+addition] = entry3;
				ptr[52+addition] = entry3;
				ptr[54+addition] = entry3;
				ptr[56+addition] = entry3;
				ptr[58+addition] = entry3;
				
				ptr[60+addition] = entry3;
				ptr[62+addition] = entry3;
				ptr[64+addition] = entry3;
				ptr[66+addition] = entry3;
				ptr[68+addition] = entry3;
				
				ptr[70+addition] = entry4;
				ptr[72+addition] = entry4;
				ptr[74+addition] = entry4;
				ptr[76+addition] = entry4;
				ptr[78+addition] = entry4;
				
				ptr[80+addition] = entry4;
				ptr[82+addition] = entry4;
				ptr[84+addition] = entry4;
				ptr[86+addition] = entry4;
				ptr[88+addition] = entry4;
				
				ptr[90+addition] = entry5;
				ptr[92+addition] = entry5;
				ptr[94+addition] = entry5;
				ptr[96+addition] = entry5;
				ptr[98+addition] = entry5;
				
				ptr[100+addition] = entry5;
				ptr[102+addition] = entry5;
				ptr[104+addition] = entry5;
				ptr[106+addition] = entry5;
				ptr[108+addition] = entry5;
				
				ptr[110+addition] = entry6;
				ptr[112+addition] = entry6;
				ptr[114+addition] = entry6;
				ptr[116+addition] = entry6;
				ptr[118+addition] = entry6;
				
				ptr[120+addition] = entry7;
				ptr[122+addition] = entry7;
				ptr[124+addition] = entry7;

				ptr += BLOCK_SIZE_LONG;
			}
		}

		protected abstract ulong GetEntry();

		protected static ulong Rotate(ulong x, int k) {
			return (x << k) | (x >> (64 - k));
		}

	#region Dispose

		public bool IsDisposed { get; private set; }

		protected virtual void DisposeAll() {

		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				try {
					this.DisposeAll();
				} catch(Exception ex) {
					NLog.Default.Error(ex, "failed to dispose");
				}
			}

			this.IsDisposed = true;
		}

		~THSPrngBase() {
			this.Dispose(false);
		}

	#endregion

	}
}