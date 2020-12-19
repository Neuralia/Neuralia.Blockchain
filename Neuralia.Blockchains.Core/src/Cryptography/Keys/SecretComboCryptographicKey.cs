using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	
	public interface ISecretComboKey : ISecretKey {
		int NonceHash { get; set; }
	}
	
	public interface ISecretComboCryptographicKey : ISecretComboKey, ISecretCryptographicKey {
		
	}

	/// <summary>
	///     a special key where we dont offer the key itself, but rather a hash of the key plus secret nonce
	/// </summary>
	public class SecretComboCryptographicKey : SecretCryptographicKey, ISecretComboCryptographicKey {

		/// <summary>
		///     the small 32 bit non cryptographgic nonce is good, because it offers many posisble solutions
		/// </summary>
		public int NonceHash { get; set; }

		public SecretComboCryptographicKey() {

		}

		public SecretComboCryptographicKey(ISecretComboKey other) : base(other) {
		}
		
		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.NonceHash);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.NonceHash = rehydrator.ReadInt();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.NonceHash);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("NonceHash", this.NonceHash);
		}

		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.SecretCombo, 1, 0);
		}

	}
}