using Neuralia.Blockchains.Core.Cryptography.Trees;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretPentaWalletKey : ISecretDoubleWalletKey {

		QTeslaWalletKey ThirdKey { get; set; }
		QTeslaWalletKey FourthKey { get; set; }
		XmssMTWalletKey FifthKey { get; set; }
	}

	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public class SecretPentaWalletKey : SecretDoubleWalletKey, ISecretPentaWalletKey {

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.ThirdKey);
			nodeList.Add(this.FourthKey);
			nodeList.Add(this.FifthKey);

			return nodeList;
		}

		public QTeslaWalletKey ThirdKey { get; set; }
		public QTeslaWalletKey FourthKey { get; set; }
		public XmssMTWalletKey FifthKey { get; set; }
	}
}