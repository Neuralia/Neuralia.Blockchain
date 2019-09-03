using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface ISecretDoubleCryptographicKey : ISecretComboCryptographicKey {

		QTeslaCryptographicKey SecondKey { get; set; }
	}

	/// <summary>
	///     a special key where we dont offer the key itself, but rather a hash of the key plus secret nonce
	/// </summary>
	public class SecretDoubleCryptographicKey : SecretComboCryptographicKey, ISecretDoubleCryptographicKey {

		public QTeslaCryptographicKey SecondKey { get; set; } = new QTeslaCryptographicKey();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.SecondKey.Dehydrate(dehydrator);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

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

		protected override void SetType() {
			this.Type = Enums.KeyTypes.SecretDouble;
		}
	}
}