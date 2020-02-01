using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {

	public interface IQTeslaCryptographicKey : ICryptographicKey {
		QTESLASecurityCategory.SecurityCategories SecurityCategory { get; set; }
	}

	public class QTeslaCryptographicKey : CryptographicKey, IQTeslaCryptographicKey {

		public QTESLASecurityCategory.SecurityCategories SecurityCategory { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.SecurityCategory);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

			this.SecurityCategory = (QTESLASecurityCategory.SecurityCategories) rehydrator.ReadByte();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add((byte) this.SecurityCategory);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("SecurityCategory", this.SecurityCategory);
		}

		protected override void SetType() {
			this.Type = Enums.KeyTypes.QTESLA;
		}
		
		public override  void SetFromWalletKey(IWalletKey walletKey) {
			base.SetFromWalletKey(walletKey);

			if(walletKey is IQTeslaWalletKey qTeslaWalletKey) {
				this.SecurityCategory = (QTESLASecurityCategory.SecurityCategories) qTeslaWalletKey.SecurityCategory;
			}
		}
	}
}