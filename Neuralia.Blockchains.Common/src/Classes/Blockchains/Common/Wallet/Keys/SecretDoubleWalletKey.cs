using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretDoubleWalletKey : ISecretComboWalletKey {

		XmssWalletKey SecondKey { get; set; }
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
		
		public XmssWalletKey SecondKey { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.SecondKey);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.SecondKey == null);

			if(this.SecondKey != null) {
				this.SecondKey.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			bool isNull = rehydrator.ReadBool();

			if(isNull == false) {

				WalletKeyHelper walletKeyHelper = this.CreateWalletKeyHelper();
				this.SecondKey = walletKeyHelper.CreateKey<XmssWalletKey>(rehydrator);
				this.SecondKey.Rehydrate(rehydrator);
			}
		}

		protected abstract WalletKeyHelper CreateWalletKeyHelper();
	}
}