using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

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
		
		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write((byte)this.SecurityCategory);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
			this.SecurityCategory = rehydrator.ReadByte();
		}
	}
}