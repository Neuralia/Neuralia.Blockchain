using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretPentaWalletKey : ISecretDoubleWalletKey {

		QTeslaWalletKey ThirdKey { get; set; }
		QTeslaWalletKey FourthKey { get; set; }
		XmssMTWalletKey FifthKey { get; set; }
	}

	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public abstract class SecretPentaWalletKey : SecretDoubleWalletKey, ISecretPentaWalletKey {

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

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.ThirdKey == null);

			if(this.ThirdKey != null) {
				this.ThirdKey.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.FourthKey == null);

			if(this.FourthKey != null) {
				this.FourthKey.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.FifthKey == null);

			if(this.FifthKey != null) {
				this.FifthKey.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			WalletKeyHelper walletKeyHelper = this.CreateWalletKeyHelper();
			bool isNull = rehydrator.ReadBool();

			if(isNull == false) {
				this.ThirdKey = walletKeyHelper.CreateKey<QTeslaWalletKey>(rehydrator);
				this.ThirdKey.Rehydrate(rehydrator);
			}

			isNull = rehydrator.ReadBool();

			if(isNull == false) {
				this.FourthKey = walletKeyHelper.CreateKey<QTeslaWalletKey>(rehydrator);
				this.FourthKey.Rehydrate(rehydrator);
			}

			isNull = rehydrator.ReadBool();

			if(isNull == false) {
				this.FifthKey = walletKeyHelper.CreateKey<XmssMTWalletKey>(rehydrator);
				this.FifthKey.Rehydrate(rehydrator);
			}
		}
	}
}