using System.Text.Json.Serialization;
using LiteDB;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretWalletKey : IXmssWalletKey, ISecretKey {
	}

	/// <summary>
	///     A secret key is a key we keep secret and use only once.
	/// </summary>
	public class SecretWalletKey : XmssWalletKey, ISecretWalletKey {
		[BsonIgnore]
		private SafeArrayHandle nextKeyHashSha2 = SafeArrayHandle.Create();
		[BsonIgnore]
		private SafeArrayHandle nextKeyHashSha3 = SafeArrayHandle.Create();

		public SecretWalletKey() {
		}

		public SafeArrayHandle NextKeyHashSha2 {
			get => this.nextKeyHashSha2;
			set {
				this.nextKeyHashSha2?.Dispose();
				this.nextKeyHashSha2 = value;
			}
		}

		public SafeArrayHandle NextKeyHashSha3 {
			get => this.nextKeyHashSha3;
			set {
				this.nextKeyHashSha3?.Dispose();
				this.nextKeyHashSha3 = value;
			}
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.NextKeyHashSha2);
			dehydrator.Write(this.NextKeyHashSha3);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.NextKeyHashSha2.Entry = rehydrator.ReadArray();
			this.NextKeyHashSha3.Entry = rehydrator.ReadArray();
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.Secret, 1,0);
		}
		
		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(NextKeyHashSha2);
			nodeList.Add(NextKeyHashSha3);
			
			return nodeList;
		}
		
		protected override void DisposeAll() {
			base.DisposeAll();
			
			this.NextKeyHashSha2?.Dispose();
			this.NextKeyHashSha3?.Dispose();
		}
	}
}