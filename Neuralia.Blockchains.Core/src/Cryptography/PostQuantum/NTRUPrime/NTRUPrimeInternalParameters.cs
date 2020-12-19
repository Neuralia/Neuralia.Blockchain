using System;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime
{
    public class NTRUPrimeInternalParameters {
        public const int Rounded_bytes_761 = 1007;
        public const int Rq_bytes_761 = 1158;
        
        public const int Rounded_bytes_857 = 1152;
        public const int Rq_bytes_857 = 1322;

        public NTRUPrimeInternalParameters(NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes keyStrengthTypes)
        {
            switch (keyStrengthTypes)
            {
                case NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_761:
                    this.p = 761;
                    this.q = 4591;
                    this.Rounded_bytes = Rounded_bytes_761;
                    this.Rq_bytes = Rq_bytes_761;
                    this.w = 286;
                    break;
                case NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_857:
                    this.p = 857;
                    this.q = 5167;
                    this.Rounded_bytes = Rounded_bytes_857;
                    this.Rq_bytes = Rq_bytes_857;
                    this.w = 322;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        /*
         * Parameters for selected SIZE type
         */
        public int p { get; }
        public short q { get; }
        public int Rounded_bytes { get; }
        public int Rq_bytes { get; }
        public int w { get; }
        /*
         * 
         */

        /*
         * Common Parameters
         */
        
        public const int HASH_Size  =64;
        public int Hash_bytes { get; } = HASH_Size;
        public int q12 => (this.q - 1) >> 1;
        public int Small_bytes => (this.p + 3) >> 2;
        public int Inputs_bytes => this.Small_bytes;
        public int Confirm_bytes { get; } = HASH_Size;

        public int Ciphertexts_bytes => this.Rounded_bytes;
        public int SecretKeys_bytes => 2 * this.Small_bytes;

        public int PublicKeys_bytes => this.Rq_bytes;
        /*
         * 
         */
    }
}
