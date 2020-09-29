using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {

	public interface ITripleXmssKey : IKey {
		IXmssKey FirstKey { get; set; }
		IXmssKey SecondKey { get; set; }
		IXmssKey ThirdKey { get; set; }
	}
	
	public interface ITripleXmssCryptographicKey : ITripleXmssKey, ICryptographicKey {
		XmssmtCryptographicKey FirstCryptographicKey { get; set; }
		XmssmtCryptographicKey SecondCryptographicKey { get; set; }
		XmssmtCryptographicKey ThirdCryptographicKey { get; set; }
	}

	public class TripleXmssCryptographicKey : CryptographicKey, ITripleXmssCryptographicKey {

		public IXmssKey FirstKey { get; set; } = new XmssCryptographicKey();
		public IXmssKey SecondKey { get; set; } = new XmssmtCryptographicKey();
		public IXmssKey ThirdKey { get; set; } = new XmssmtCryptographicKey();

		public XmssmtCryptographicKey FirstCryptographicKey {
			get => (XmssmtCryptographicKey)this.FirstKey;
			set => this.FirstKey = value;
		}

		public XmssmtCryptographicKey SecondCryptographicKey {
			get => (XmssmtCryptographicKey)this.SecondKey;
			set => this.SecondKey = value;
		}
		
		public XmssmtCryptographicKey ThirdCryptographicKey {
			get => (XmssmtCryptographicKey)this.ThirdKey;
			set => this.ThirdKey = value;
		}

		public TripleXmssCryptographicKey() {

		}

		public TripleXmssCryptographicKey(ITripleXmssCryptographicKey other) : base(other) {
		}
		
		public override bool IsEmpty => base.IsEmpty || (this.FirstCryptographicKey?.IsEmpty ?? true) || (this.SecondCryptographicKey?.IsEmpty ?? true) || (this.ThirdCryptographicKey?.IsEmpty ?? true);

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.FirstKey.Dehydrate(dehydrator);
			this.SecondKey.Dehydrate(dehydrator);
			this.ThirdKey.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.FirstKey.Rehydrate(rehydrator);
			this.SecondKey.Rehydrate(rehydrator);
			this.ThirdKey.Rehydrate(rehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.FirstKey);
			nodeList.Add(this.SecondKey);
			nodeList.Add(this.ThirdKey);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("FirstKey", this.FirstKey);
			jsonDeserializer.SetProperty("SecondKey", this.SecondKey);
			jsonDeserializer.SetProperty("ThirdKey", this.ThirdKey);
		}

		public override void SetFromKey(IKey walletKey) {
			base.SetFromKey(walletKey);

			if(walletKey is ITripleXmssKey tripleXmssKey) {

				this.FirstCryptographicKey.SetFromKey(tripleXmssKey);

				this.SecondCryptographicKey.SetFromKey(tripleXmssKey.SecondKey);

				this.ThirdCryptographicKey.SetFromKey(tripleXmssKey.ThirdKey);
			}
		}

		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.TripleXMSS, 1, 0);
		}

	}
}