using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	public interface IPoly1305 {
		unsafe void ComputeMac(SafeArrayHandle data, SafeArrayHandle key, SafeArrayHandle mac, int? length = null);
		unsafe void ComputeMac(SafeArrayHandle data, SafeArrayHandle key, int? length = null);
	}

}