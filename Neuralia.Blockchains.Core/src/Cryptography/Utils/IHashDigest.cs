using Org.BouncyCastle.Crypto;

namespace Neuralia.Blockchains.Core.Cryptography.Utils {
	public interface IHashDigest : IDigest {
		new int GetByteLength();
		
		new void BlockUpdate(byte[] input, int inOff, int length);

		new int DoFinal(byte[] output, int outOff);
	}
}