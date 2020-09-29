using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ITripleXmssWalletKey : IXmssWalletKey {

		XmssMTWalletKey SecondKey { get; set; }
		XmssMTWalletKey ThirdKey { get; set; }
	}

	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public abstract class TripleXmssWalletKey : XmssWalletKey, ITripleXmssWalletKey {

		public TripleXmssWalletKey() {
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.TripleXMSS, 1,0);
		}
		
		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.SecondKey);
			nodeList.Add(this.ThirdKey);

			return nodeList;
		}
		

		public XmssMTWalletKey SecondKey { get; set; }
		public XmssMTWalletKey ThirdKey { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.SecondKey == null);

			if(this.SecondKey != null) {
				this.SecondKey.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.ThirdKey == null);

			if(this.ThirdKey != null) {
				this.ThirdKey.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			WalletKeyHelper walletKeyHelper = this.CreateWalletKeyHelper();
			bool isNull = rehydrator.ReadBool();
			
			if(isNull == false) {
				this.SecondKey = walletKeyHelper.CreateKey<XmssMTWalletKey>(rehydrator);
				this.SecondKey.Rehydrate(rehydrator);
			}

			isNull = rehydrator.ReadBool();

			if(isNull == false) {
				this.ThirdKey = walletKeyHelper.CreateKey<XmssMTWalletKey>(rehydrator);
				this.ThirdKey.Rehydrate(rehydrator);
			}
		}

		protected abstract WalletKeyHelper CreateWalletKeyHelper();
	}
}