using System;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime
{
	public struct NTRUPrimeApiParameters
	{
		public int SecretKeyBytes { get; }
		public int PublicKeyBytes { get; }
		public int CipherTextBytes { get; }

		public NTRUPrimeApiParameters(NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes keyStrengthTypes)
		{
			switch (keyStrengthTypes)
			{
				case NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_761:
					SecretKeyBytes = 1731+NTRUPrimeInternalParameters.HASH_Size;
					PublicKeyBytes = NTRUPrimeInternalParameters.Rq_bytes_761;
					CipherTextBytes = NTRUPrimeInternalParameters.Rounded_bytes_761+NTRUPrimeInternalParameters.HASH_Size;
					break;
				case NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_857:
					SecretKeyBytes = 1967+NTRUPrimeInternalParameters.HASH_Size;
					PublicKeyBytes = NTRUPrimeInternalParameters.Rq_bytes_857;
					CipherTextBytes = NTRUPrimeInternalParameters.Rounded_bytes_857+NTRUPrimeInternalParameters.HASH_Size;
					break;
				default:
					throw new NotImplementedException();
			}
		}
	}
}