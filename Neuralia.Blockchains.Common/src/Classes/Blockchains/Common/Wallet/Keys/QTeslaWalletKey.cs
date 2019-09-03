using Neuralia.Blockchains.Core.Cryptography.Trees;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface IQTeslaWalletKey : IWalletKey {
		byte SecurityCategory { get; set; }
	}

	public class QTeslaWalletKey : WalletKey, IQTeslaWalletKey {

		public byte SecurityCategory { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.SecurityCategory);

			return nodeList;
		}
	}
}