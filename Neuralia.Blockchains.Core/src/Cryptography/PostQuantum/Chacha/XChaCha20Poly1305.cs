namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha {
	public class XChaCha20Poly1305 : XChaChaPoly1305 {

		public XChaCha20Poly1305() : base(XChaCha.CHACHA_20_ROUNDS) {
		}
	}
}