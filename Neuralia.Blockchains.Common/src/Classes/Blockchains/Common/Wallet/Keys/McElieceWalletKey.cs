using Neuralia.Blockchains.Core.Cryptography.Trees;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface IMcElieceWalletKey : IWalletKey {

		byte McElieceCipherMode { get; set; }
		int M { get; set; }
		int T { get; set; }
		byte McElieceHashMode { get; set; }
	}

	public class McElieceWalletKey : WalletKey, IMcElieceWalletKey {

		public byte McElieceCipherMode { get; set; }
		public int M { get; set; }
		public int T { get; set; }
		public byte McElieceHashMode { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.McElieceCipherMode);
			nodeList.Add(this.M);
			nodeList.Add(this.T);
			nodeList.Add(this.McElieceHashMode);

			return nodeList;
		}
	}
}