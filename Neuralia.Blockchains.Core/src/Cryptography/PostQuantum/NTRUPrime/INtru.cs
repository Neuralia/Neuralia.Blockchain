namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime
{
    public unsafe interface INtru
    {
        int CryptoKemKeypair(byte* pk, byte* sk);

        int CryptoKemEnc(byte* c, byte* k, byte* pk);

        int CryptoKemDec(byte* k, byte* c, byte* sk);
    }
}
