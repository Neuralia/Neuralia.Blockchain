using System.Collections.Generic;
using System.Text;
using LiteDB;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ITripleXmssWalletKey : IWalletKey, ITripleXmssKey {
		XmssWalletKey FirstWalletKey { get; set; }
		XmssMTWalletKey SecondWalletKey { get; set; }
		XmssMTWalletKey ThirdWalletKey { get; set; }
	}

	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public abstract class TripleXmssWalletKey : WalletKey, ITripleXmssWalletKey {

		public TripleXmssWalletKey() {
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.TripleXMSS, 1,0);
		}
		
		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.FirstKey);
			nodeList.Add(this.SecondKey);
			nodeList.Add(this.ThirdKey);

			return nodeList;
		}

		[BsonIgnore, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public IXmssKey FirstKey {
			get => this.FirstWalletKey;
			set => this.FirstWalletKey = (XmssWalletKey)value;
		}

		[BsonIgnore, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public IXmssmtKey SecondKey {
			get => this.SecondWalletKey;
			set => this.SecondWalletKey = (XmssMTWalletKey)value;
		}
		
		[BsonIgnore, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public IXmssmtKey ThirdKey {
			get => this.ThirdWalletKey;
			set => this.ThirdWalletKey = (XmssMTWalletKey)value;
		}
		
		public XmssWalletKey FirstWalletKey { get; set; }
		public XmssMTWalletKey SecondWalletKey { get; set; }
		public XmssMTWalletKey ThirdWalletKey { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.FirstKey == null);

			if(this.FirstKey != null) {
				this.FirstKey.Dehydrate(dehydrator);
			}

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
				this.FirstKey = walletKeyHelper.CreateKey<XmssWalletKey>(rehydrator);
				this.FirstKey.Rehydrate(rehydrator);
			}
			
			isNull = rehydrator.ReadBool();
			
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
		
		protected virtual string KeyExportName => "TRIPLE_XMSS_WALLET";
		protected virtual string ExportKey() {
			
			var key = new {
				Version = this.Version.ToString(), 
				this.Name,
				this.Ordinal, 
				this.AccountCode,
				this.Hash, 
				this.CreatedTime,
				FirstWalletKey = this.FirstWalletKey.ExportKey(), 
				SecondWalletKey = this.SecondWalletKey.ExportKey(),  
				ThirdWalletKey = this.ThirdWalletKey.ExportKey()
			};
			
			string keyString = JsonConvert.SerializeObject(key);
			
			using var hash = HashingUtils.HashSha256(hasher => {

				using var parts = (SafeArrayHandle) Encoding.Unicode.GetBytes(keyString);
				return hasher.Hash(parts);
			});
			
			return $"{this.KeyExportName}:::{keyString}:::{hash.ToBase64()}";
		}

		protected override void DisposeAll() {
			base.DisposeAll();
			
			this.FirstWalletKey?.Dispose();
			this.SecondWalletKey?.Dispose();
			this.ThirdWalletKey?.Dispose();
		}

		protected abstract WalletKeyHelper CreateWalletKeyHelper();
	}
}