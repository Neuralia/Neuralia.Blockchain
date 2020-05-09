using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {

	public interface ISecretPentaCryptographicKey : ISecretDoubleCryptographicKey {

		QTeslaCryptographicKey ThirdKey { get; set; }
		QTeslaCryptographicKey FourthKey { get; set; }
		XmssmtCryptographicKey FifthKey { get; set; }
	}

	public class SecretPentaCryptographicKey : SecretDoubleCryptographicKey, ISecretPentaCryptographicKey {

		public QTeslaCryptographicKey ThirdKey { get; set; } = new QTeslaCryptographicKey();
		public QTeslaCryptographicKey FourthKey { get; set; } = new QTeslaCryptographicKey();
		public XmssmtCryptographicKey FifthKey { get; set; } = new XmssmtCryptographicKey();

		public override bool IsEmpty => base.IsEmpty || (this.ThirdKey?.IsEmpty ?? true) || (this.FourthKey?.IsEmpty ?? true) || (this.FifthKey?.IsEmpty ?? true);

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.ThirdKey.Dehydrate(dehydrator);
			this.FourthKey.Dehydrate(dehydrator);
			this.FifthKey.Dehydrate(dehydrator);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

			this.ThirdKey.Rehydrate(rehydrator);
			this.FourthKey.Rehydrate(rehydrator);
			this.FifthKey.Rehydrate(rehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.ThirdKey);
			nodeList.Add(this.FourthKey);
			nodeList.Add(this.FifthKey);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("ThirdKey", this.ThirdKey);
			jsonDeserializer.SetProperty("FourthKey", this.FourthKey);
			jsonDeserializer.SetProperty("FifthKey", this.FifthKey);
		}

		public override void SetFromWalletKey(IWalletKey walletKey) {
			base.SetFromWalletKey(walletKey);

			if(walletKey is ISecretPentaWalletKey secretPentaWalletKey) {

				this.ThirdKey.SetFromWalletKey(secretPentaWalletKey.ThirdKey);
				this.ThirdKey.Id = secretPentaWalletKey.KeyAddress.OrdinalId;

				this.FourthKey.SetFromWalletKey(secretPentaWalletKey.FourthKey);
				this.FourthKey.Id = secretPentaWalletKey.KeyAddress.OrdinalId;

				this.FifthKey.SetFromWalletKey(secretPentaWalletKey.FifthKey);
				this.FifthKey.Id = secretPentaWalletKey.KeyAddress.OrdinalId;
			}
		}

		protected override void SetType() {
			this.Type = Enums.KeyTypes.SecretPenta;
		}
	}
}