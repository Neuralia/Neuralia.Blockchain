using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface ISecretComboCryptographicKey : ISecretCryptographicKey {
		int NonceHash { get; set; }
	}

	/// <summary>
	///     a special key where we dont offer the key itself, but rather a hash of the key plus secret nonce
	/// </summary>
	public class SecretComboCryptographicKey : SecretCryptographicKey, ISecretComboCryptographicKey {

		/// <summary>
		///     the small 32 bit non cryptographgic nonce is good, because it offers many posisble solutions
		/// </summary>
		public int NonceHash { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.NonceHash);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

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

		protected override void SetType() {
			this.Type = Enums.KeyTypes.SecretCombo;
		}
	}
}