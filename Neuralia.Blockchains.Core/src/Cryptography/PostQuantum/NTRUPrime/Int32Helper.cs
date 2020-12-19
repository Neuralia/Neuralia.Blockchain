using System.Runtime.CompilerServices;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime
{
    /// <summary>
    /// Int32 operations helper
    /// </summary>
    public static unsafe class Int32Helper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void int32_divmod_uint14(int* q, ushort* r, int x, ushort m)
        {
            uint uq;
            uint uq2;
            ushort ur;
            ushort ur2;
            uint mask;

            Uint32Helper.uint32_divmod_uint14(&uq, &ur, 0x80000000 + (uint)x, m);
            Uint32Helper.uint32_divmod_uint14(&uq2, &ur2, 0x80000000, m);
            ur -= ur2;
            uq -= uq2;
            mask = (uint)(-(ur >> 15));
            ur += (ushort)(mask & m);
            uq += mask;
            *r = ur;
            *q = (int)uq;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int int32_div_uint14(int x, ushort m)
        {
            int q;
            ushort r;
            int32_divmod_uint14(&q, &r, x, m);
            return q;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort int32_mod_uint14(int x, ushort m)
        {
            int q;
            ushort r;
            int32_divmod_uint14(&q, &r, x, m);
            return r;
        }
    }
}
