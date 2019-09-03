using Neuralia.Blockchains.Core.Cryptography.Trees;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public interface IXmssMTWalletKey : IXmssWalletKey {
		int TreeLayers { get; set; }
	}

	public class XmssMTWalletKey : XmssWalletKey, IXmssMTWalletKey {

		/// <summary>
		///     xmss layers if XMSSMT
		/// </summary>
		public int TreeLayers { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TreeLayers);

			return nodeList;
		}
	}
}