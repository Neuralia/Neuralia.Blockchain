using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {

	public interface ISecretPentaKey : IKey {
		IXmssKey ThirdKey { get; set; }
		IXmssmtKey FourthKey { get; set; }
		IXmssmtKey FifthKey { get; set; }
	}
	
	public interface ISecretPentaCryptographicKey : ISecretPentaKey, ISecretDoubleCryptographicKey {

		XmssCryptographicKey ThirdCryptographicKey { get; set; }
		XmssmtCryptographicKey FourthCryptographicKey { get; set; }
		XmssmtCryptographicKey FifthCryptographicKey { get; set; }
	}

	public class SecretPentaCryptographicKey : SecretDoubleCryptographicKey, ISecretPentaCryptographicKey {

		public IXmssKey ThirdKey { get; set; } = new XmssCryptographicKey();
		public IXmssmtKey FourthKey { get; set; } = new XmssmtCryptographicKey();
		public IXmssmtKey FifthKey { get; set; } = new XmssmtCryptographicKey();

		public XmssCryptographicKey ThirdCryptographicKey {
			get => (XmssCryptographicKey)this.SecondKey;
			set => this.SecondKey = value;
		}
		public XmssmtCryptographicKey FourthCryptographicKey {
			get => (XmssmtCryptographicKey)this.SecondKey;
			set => this.SecondKey = value;
		}
		public XmssmtCryptographicKey FifthCryptographicKey {
			get => (XmssmtCryptographicKey)this.SecondKey;
			set => this.SecondKey = value;
		}
		public SecretPentaCryptographicKey() {

		}

		public SecretPentaCryptographicKey(ISecretPentaCryptographicKey other) : base(other) {
		}
		
		public override bool IsEmpty => base.IsEmpty || (this.ThirdCryptographicKey?.IsEmpty ?? true) || (this.FourthCryptographicKey?.IsEmpty ?? true) || (this.FifthCryptographicKey?.IsEmpty ?? true);

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.ThirdKey.Dehydrate(dehydrator);
			this.FourthKey.Dehydrate(dehydrator);
			this.FifthKey.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

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

		public override void SetFromKey(IKey walletKey) {
			base.SetFromKey(walletKey);

			if(walletKey is ISecretPentaKey secretPentaWalletKey) {

				this.ThirdCryptographicKey.SetFromKey(secretPentaWalletKey.ThirdKey);

				this.FourthCryptographicKey.SetFromKey(secretPentaWalletKey.FourthKey);

				this.FifthCryptographicKey.SetFromKey(secretPentaWalletKey.FifthKey);
			}
		}

		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.SecretPenta, 1, 0);
		}

	}
}