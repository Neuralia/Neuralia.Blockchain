using System.Text.Json.Serialization;
using LiteDB;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretDoubleWalletKey : ISecretComboWalletKey, ISecretDoubleKey {

		XmssWalletKey SecondWalletKey { get; set; }
	}

	//TODO: ensure we use sakura trees for the hashing of a secret key.
	//TODO: is it safe to hash a key?  what if another type of key/nonce combination can also give the same hash?
	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public abstract class SecretDoubleWalletKey : SecretComboWalletKey, ISecretDoubleWalletKey {

		public SecretDoubleWalletKey() {
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.SecretDouble, 1,0);
		}
		

		[BsonIgnore, JsonIgnore]
		public IXmssKey SecondKey {
			get => this.SecondWalletKey;
			set => this.SecondWalletKey = (XmssWalletKey)value;
		}

		public XmssWalletKey SecondWalletKey { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.SecondWalletKey);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.SecondWalletKey == null);

			if(this.SecondWalletKey != null) {
				this.SecondWalletKey.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			bool isNull = rehydrator.ReadBool();

			if(isNull == false) {

				WalletKeyHelper walletKeyHelper = this.CreateWalletKeyHelper();
				this.SecondWalletKey = walletKeyHelper.CreateKey<XmssWalletKey>(rehydrator);
				this.SecondWalletKey.Rehydrate(rehydrator);
			}
		}

		protected override void DisposeAll() {
			base.DisposeAll();
			
			this.SecondWalletKey?.Dispose();
		}
		
		protected abstract WalletKeyHelper CreateWalletKeyHelper();
	}
}