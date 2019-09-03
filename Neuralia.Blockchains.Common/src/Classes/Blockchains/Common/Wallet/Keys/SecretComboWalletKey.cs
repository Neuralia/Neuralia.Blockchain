using Neuralia.Blockchains.Core.Cryptography.Trees;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretComboWalletKey : ISecretWalletKey {

		long PromisedNonce1 { get; set; }

		long PromisedNonce2 { get; set; }
	}

	//TODO: ensure we use sakura trees for the hashing of a secret key.
	//TODO: is it safe to hash a key?  what if another type of key/nonce combination can also give the same hash?
	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public class SecretComboWalletKey : SecretWalletKey, ISecretComboWalletKey {

		public long PromisedNonce1 { get; set; }

		public long PromisedNonce2 { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.PromisedNonce1);
			nodeList.Add(this.PromisedNonce2);

			return nodeList;
		}
	}
}