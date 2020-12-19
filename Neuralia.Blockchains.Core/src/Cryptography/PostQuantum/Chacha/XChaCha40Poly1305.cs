namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	public class XChaCha40Poly1305 : XChaChaPoly1305 {
		
		public XChaCha40Poly1305() : base(XChaCha.CHACHA_40_ROUNDS) {
		}
	}
}