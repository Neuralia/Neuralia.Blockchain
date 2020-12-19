using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	
	public interface ISecretDoubleKey : ISecretComboKey {
		IXmssKey SecondKey { get; set; }

	}
	
	public interface ISecretDoubleCryptographicKey : ISecretDoubleKey, ISecretComboCryptographicKey {
		XmssCryptographicKey SecondCryptographicKey { get; set; }
	}

	/// <summary>
	///     a special key where we dont offer the key itself, but rather a hash of the key plus secret nonce
	/// </summary>
	public class SecretDoubleCryptographicKey : SecretComboCryptographicKey, ISecretDoubleCryptographicKey {

		public IXmssKey SecondKey { get; set; } = new XmssCryptographicKey();

		public XmssCryptographicKey SecondCryptographicKey {
			get => (XmssCryptographicKey)this.SecondKey;
			set => this.SecondKey = value;
		}
		public SecretDoubleCryptographicKey() {

		}

		public SecretDoubleCryptographicKey(ISecretDoubleKey other) : base(other) {
		}

		
		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.SecondKey.Dehydrate(dehydrator);
		}

		public override bool IsEmpty => base.IsEmpty || (this.SecondCryptographicKey?.IsEmpty ?? true);

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.SecondKey.Rehydrate(rehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.SecondKey);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("SecondKey", this.SecondKey);
		}

		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.SecretDouble, 1, 0);
		}

	}
}