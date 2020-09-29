#if NETCOREAPP3_1
using System.Runtime.Intrinsics.X86;
#endif
namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	public static class ChachaFactory {
		public static IPoly1305 Create1305() {
#if NETCOREAPP3_1
			if(Avx2.IsSupported) {
				// for now, the avx version is slower than the regular version, so we disable it.
				//return new Poly1305Avx();
			}
#endif
			return new Poly1305();
		}
		
		public static IXChaCha CreateXChacha(int rounds = XChaCha.CHACHA_DEFAULT_ROUNDS) {
#if NETCOREAPP3_1
			// if(XChaChaAvx.IsSupported){
			// 	return new XChaChaAvx(rounds);
			// }
#endif
			return new XChaCha(rounds);
		}
	}
}