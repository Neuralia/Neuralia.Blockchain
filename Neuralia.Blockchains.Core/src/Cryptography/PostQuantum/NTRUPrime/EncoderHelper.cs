using System.Runtime.CompilerServices;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime
{
	/// <summary>
	/// Math Encode and Decode Helper
	/// </summary>
    public static class EncoderHelper
    {
		/// <summary>
		/// Math Encode 0 <= R[i] < M[i] < 16384
		/// </summary>
		/// <param name="output"></param>
		/// <param name="R"></param>
		/// <param name="M"></param>
		/// <param name="len"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void Encode(byte* output, ushort* R, ushort* M, long len)
		{
			if (len == 1)
			{
				ushort r = R[0];
				ushort m = M[0];
				while (m > 1)
				{
					*output++ = (byte)r;
					r >>= 8;
					m = (ushort)((m + 255) >> 8);
				}
			}
			if (len > 1)
			{
				ushort[] R2 = new ushort[(len + 1) / 2];
				ushort[] M2 = new ushort[(len + 1) / 2];
				long i;
				for (i = 0; i < len - 1; i += 2)
				{
					uint m0 = M[i];
					uint r = R[i] + R[i + 1] * m0;
					uint m = M[i + 1] * m0;
					while (m >= 16384)
					{
						*output++ = (byte)r;
						r >>= 8;
						m = (uint)((m + 255) >> 8);
					}
					R2[i / 2] = (ushort)r;
					M2[i / 2] = (ushort)m;
				}
				if (i < len)
				{
					R2[i / 2] = R[i];
					M2[i / 2] = M[i];
				}
                fixed (ushort* R2ptr = R2, M2ptr = M2)
                {
					Encode(output, R2ptr, M2ptr, (len + 1) / 2);

				}
				
			}
		}

		/// <summary>
		/// Math Decode
		/// </summary>
		/// <param name="output"></param>
		/// <param name="S"></param>
		/// <param name="M"></param>
		/// <param name="len"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void Decode(ushort *output, byte *S, ushort *M, long len)
		{
			if (len == 1)
			{
				if (M[0] == 1)
				{
					*output = 0;
				}
				else if (M[0] <= 256)
				{
					*output = Uint32Helper.uint32_mod_uint14(S[0], M[0]);
				}
				else
				{
					*output = Uint32Helper.uint32_mod_uint14((uint)(S[0] + (((ushort)S[1]) << 8)), M[0]);
				}
			}
			if (len > 1)
			{
				ushort[] R2 = new ushort[(len + 1) / 2];
				ushort[] M2 = new ushort[(len + 1) / 2];
				ushort[] bottomr = new ushort[len / 2];
				uint[] bottomt = new uint[len / 2];
				long i;
				for (i = 0; i < len - 1; i += 2)
				{
					uint m = M[i] * (uint)M[i + 1];
					if (m > 256 * 16383)
					{
						bottomt[i / 2] = 256 * 256;
						bottomr[i / 2] = (ushort)(S[0] + (256 * S[1]));
						S += 2;
						M2[i / 2] = (ushort)((((m + 255) >> 8) + 255) >> 8);
					}
					else if (m >= 16384)
					{
						bottomt[i / 2] = 256;
						bottomr[i / 2] = S[0];
						S += 1;
						M2[i / 2] = (ushort)((m + 255) >> 8);
					}
					else
					{
						bottomt[i / 2] = 1;
						bottomr[i / 2] = 0;
						M2[i / 2] = (ushort)m;
					}
				}
				if (i < len)
				{
					M2[i / 2] = M[i];
				}

                fixed (ushort* R2ptr = R2)
                {
                    fixed (ushort* M2ptr = M2)
                    {
						Decode(R2ptr, S, M2ptr, (len + 1) / 2);
					}
					
				}
				
				for (i = 0; i < len - 1; i += 2)
				{
					uint r = bottomr[i / 2];
					uint r1;
					ushort r0;
					r += bottomt[i / 2] * R2[i / 2];
					Uint32Helper.uint32_divmod_uint14(&r1, &r0, r, M[i]);
					r1 = Uint32Helper.uint32_mod_uint14(r1, M[i + 1]); // only needed for invalid inputs
					*output++ = r0;
					*output++ = (ushort)r1;
				}
				if (i < len)
				{
					*output++ = R2[i / 2];
				}
			}
		}
	}

	

}
