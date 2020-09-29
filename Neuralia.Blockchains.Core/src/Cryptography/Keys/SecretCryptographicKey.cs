using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	
	public interface ISecretKey : IKey {
		SafeArrayHandle NextKeyHashSha2 { get;  }
		SafeArrayHandle NextKeyHashSha3 { get;  }
	}
	
	public interface ISecretCryptographicKey : ISecretKey, IXmssCryptographicKey {
		
	}

	/// <summary>
	///     a special key where we dont offer the key itself, but rather a hash of the key
	/// </summary>
	public class SecretCryptographicKey : XmssCryptographicKey, ISecretCryptographicKey {

		public SafeArrayHandle NextKeyHashSha2 { get; } = SafeArrayHandle.Create();

		public SecretCryptographicKey() {

		}

		public SecretCryptographicKey(ISecretCryptographicKey other) : base(other) {
		}

		
		/// <summary>
		///     just a passthrough nextKeyHash
		/// </summary>
		/// 
		public SafeArrayHandle NextKeyHashSha3 {
			get => this.PublicKey;
			set => this.PublicKey.Entry = value.Entry;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.NextKeyHashSha2);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.NextKeyHashSha2.Entry = rehydrator.ReadNonNullableArray();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.NextKeyHashSha2);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("NextKeyHashSha2", this.NextKeyHashSha2);
			jsonDeserializer.SetProperty("NextKeyHashSha3", this.NextKeyHashSha3);
		}

		public override  void SetFromKey(IKey walletKey) {
			base.SetFromKey(walletKey);

			if(walletKey is ISecretKey secretWalletKey) {

				this.NextKeyHashSha2.Entry = secretWalletKey.NextKeyHashSha2.Entry;
				this.NextKeyHashSha3.Entry = secretWalletKey.NextKeyHashSha3.Entry;
			}
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.Secret, 1, 0);
		}

	}
}