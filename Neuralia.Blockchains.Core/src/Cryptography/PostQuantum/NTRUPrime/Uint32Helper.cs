using System.Runtime.CompilerServices;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime
{
    public static unsafe class Uint32Helper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void minmax(uint* x, uint* y)
        {
            uint xi = *x;
            uint yi = *y;
            uint xy = xi ^ yi;
            uint c = yi - xi;
            c ^= xy & (c ^ yi ^ 0x80000000);
            c >>= 31;
            c = (uint)-c;
            c &= xy;
            *x = xi ^ c;
            *y = yi ^ c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void uint32_sort(uint* x, int n)
        {
            int top;
            int p;
            int q;
            int i;

            if (n < 2)
            {
                return;
            }
            top = 1;
            while (top < n - top)
            {
                top += top;
            }

            for (p = top; p > 0; p >>= 1)
            {
                for (i = 0; i < n - p; ++i)
                {
                    if ((i & p) == 0)
                    {
                        var xi = x + i;
                        var yi = x + i + p;

                        minmax(xi, yi);
                    }
                }
                for (q = top; q > p; q >>= 1)
                {
                    for (i = 0; i < n - q; ++i)
                    {
                        if ((i & p) == 0)
                        {
                            var xi = x + i;
                            var yi = x + i + p;
                            minmax(xi, yi);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void uint32_divmod_uint14(uint* q, ushort* r, uint x, ushort m)
        {
            uint v = 0x80000000;
            uint qpart;
            uint mask;

            v /= m;

            /* caller guarantees m > 0 */
            /* caller guarantees m < 16384 */
            /* vm <= 2^31 <= vm+m-1 */
            /* xvm <= 2^31 x <= xvm+x(m-1) */

            *q = 0;

            qpart = (uint)((x * (ulong)v) >> 31);
            /* 2^31 qpart <= xv <= 2^31 qpart + 2^31-1 */
            /* 2^31 qpart m <= xvm <= 2^31 qpart m + (2^31-1)m */
            /* 2^31 qpart m <= 2^31 x <= 2^31 qpart m + (2^31-1)m + x(m-1) */
            /* 0 <= 2^31 newx <= (2^31-1)m + x(m-1) */
            /* 0 <= newx <= (1-1/2^31)m + x(m-1)/2^31 */
            /* 0 <= newx <= (1-1/2^31)(2^14-1) + (2^32-1)((2^14-1)-1)/2^31 */

            x -= qpart * m;
            *q += qpart;
            /* x <= 49146 */

            qpart = (uint)((x * (ulong)v) >> 31);
            /* 0 <= newx <= (1-1/2^31)m + x(m-1)/2^31 */
            /* 0 <= newx <= m + 49146(2^14-1)/2^31 */
            /* 0 <= newx <= m + 0.4 */
            /* 0 <= newx <= m */

            x -= qpart * m;
            *q += qpart;
            /* x <= m */

            x -= m;
            *q += 1;
            mask = (uint)(-(x >> 31));
            x += mask & m;
            *q += mask;
            /* x < m */

            *r = (ushort)x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint uint32_div_uint14(uint x, ushort m)
        {
            uint q;
            ushort r;
            uint32_divmod_uint14(&q, &r, x, m);
            return q;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort uint32_mod_uint14(uint x, ushort m)
        {
            uint q;
            ushort r;
            uint32_divmod_uint14(&q, &r, x, m);
            return r;

        }

    }
}
