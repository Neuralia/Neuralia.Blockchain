using Neuralia.Blockchains.Core.Cryptography.Trees;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretDoubleWalletKey : ISecretComboWalletKey {

		QTeslaWalletKey SecondKey { get; set; }
	}

	//TODO: ensure we use sakura trees for the hashing of a secret key.
	//TODO: is it safe to hash a key?  what if another type of key/nonce combination can also give the same hash?
	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public class SecretDoubleWalletKey : SecretComboWalletKey, ISecretDoubleWalletKey {

		public QTeslaWalletKey SecondKey { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.SecondKey);

			return nodeList;
		}
	}
}