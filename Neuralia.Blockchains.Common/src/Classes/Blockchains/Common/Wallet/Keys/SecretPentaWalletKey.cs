using System.Text.Json.Serialization;
using LiteDB;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretPentaWalletKey : ISecretDoubleWalletKey, ISecretPentaKey {

		XmssWalletKey ThirdWalletKey { get; set; }
		XmssMTWalletKey FourthWalletKey { get; set; }
		XmssMTWalletKey FifthWalletKey { get; set; }
	}

	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public abstract class SecretPentaWalletKey : SecretDoubleWalletKey, ISecretPentaWalletKey {

		public SecretPentaWalletKey() {
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.SecretPenta, 1,0);
		}
		
		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.ThirdKey);
			nodeList.Add(this.FourthKey);
			nodeList.Add(this.FifthKey);

			return nodeList;
		}

		[BsonIgnore, JsonIgnore]
		public IXmssKey ThirdKey {
			get => this.ThirdWalletKey;
			set => this.ThirdWalletKey = (XmssWalletKey)value;
		}
		
		[BsonIgnore, JsonIgnore]
		public IXmssmtKey FourthKey {
			get => this.FourthWalletKey;
			set => this.FourthWalletKey = (XmssMTWalletKey)value;
		}
		
		[BsonIgnore, JsonIgnore]
		public IXmssmtKey FifthKey {
			get => this.FifthWalletKey;
			set => this.FifthWalletKey = (XmssMTWalletKey)value;
		}

		public XmssWalletKey ThirdWalletKey { get; set; }
		public XmssMTWalletKey FourthWalletKey { get; set; }
		public XmssMTWalletKey FifthWalletKey { get; set; }
		

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.ThirdWalletKey == null);

			if(this.ThirdWalletKey != null) {
				this.ThirdWalletKey.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.FourthWalletKey == null);

			if(this.FourthWalletKey != null) {
				this.FourthWalletKey.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.FifthWalletKey == null);

			if(this.FifthWalletKey != null) {
				this.FifthWalletKey.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			WalletKeyHelper walletKeyHelper = this.CreateWalletKeyHelper();
			bool isNull = rehydrator.ReadBool();

			if(isNull == false) {
				this.ThirdWalletKey = walletKeyHelper.CreateKey<XmssWalletKey>(rehydrator);
				this.ThirdWalletKey.Rehydrate(rehydrator);
			}

			isNull = rehydrator.ReadBool();

			if(isNull == false) {
				this.FourthWalletKey = walletKeyHelper.CreateKey<XmssMTWalletKey>(rehydrator);
				this.FourthWalletKey.Rehydrate(rehydrator);
			}

			isNull = rehydrator.ReadBool();

			if(isNull == false) {
				this.FifthWalletKey = walletKeyHelper.CreateKey<XmssMTWalletKey>(rehydrator);
				this.FifthWalletKey.Rehydrate(rehydrator);
			}
		}
		
		protected override void DisposeAll() {
			base.DisposeAll();
			
			this.ThirdWalletKey?.Dispose();
			this.FourthWalletKey?.Dispose();
			this.FifthWalletKey?.Dispose();
		}
	}
}