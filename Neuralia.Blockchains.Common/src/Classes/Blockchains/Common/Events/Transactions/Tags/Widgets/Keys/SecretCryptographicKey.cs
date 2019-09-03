using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface ISecretCryptographicKey : IQTeslaCryptographicKey {
		IByteArray NextKeyHashSha2 { get; set; }
		IByteArray NextKeyHashSha3 { get; set; }
	}

	/// <summary>
	///     a special key where we dont offer the key itself, but rather a hash of the key
	/// </summary>
	public class SecretCryptographicKey : QTeslaCryptographicKey, ISecretCryptographicKey {

		public IByteArray NextKeyHashSha2 { get; set; }

		/// <summary>
		///     just a passthrough nextKeyHash
		/// </summary>
		public IByteArray NextKeyHashSha3 {
			get => this.Key;
			set => this.Key = value;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.NextKeyHashSha2);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

			this.NextKeyHashSha2 = rehydrator.ReadNonNullableArray();
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

		protected override void SetType() {
			this.Type = Enums.KeyTypes.Secret;
		}
	}
}