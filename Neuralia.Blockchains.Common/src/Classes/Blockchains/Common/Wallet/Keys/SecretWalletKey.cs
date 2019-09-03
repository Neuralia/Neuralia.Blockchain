using Neuralia.Blockchains.Core.Cryptography.Trees;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretWalletKey : IQTeslaWalletKey {
	}

	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public class SecretWalletKey : QTeslaWalletKey, ISecretWalletKey {

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			return nodeList;
		}
	}
}