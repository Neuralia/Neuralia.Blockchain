using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface ISecretCryptographicKey : IQTeslaCryptographicKey {
		SafeArrayHandle NextKeyHashSha2 { get;  }
		SafeArrayHandle NextKeyHashSha3 { get;  }
	}

	/// <summary>
	///     a special key where we dont offer the key itself, but rather a hash of the key
	/// </summary>
	public class SecretCryptographicKey : QTeslaCryptographicKey, ISecretCryptographicKey {

		public SafeArrayHandle NextKeyHashSha2 { get; } = SafeArrayHandle.Create();

		/// <summary>
		///     just a passthrough nextKeyHash
		/// </summary>
		/// 
		public SafeArrayHandle NextKeyHashSha3 {
			get => this.Key;
			set => this.Key.Entry = value.Entry;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.NextKeyHashSha2);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

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

		public override  void SetFromWalletKey(IWalletKey walletKey) {
			base.SetFromWalletKey(walletKey);

			if(walletKey is ISecretWalletKey secretWalletKey) {
				(SafeArrayHandle sha2, SafeArrayHandle sha3) hashes = BlockchainHashingUtils.HashSecretKey(secretWalletKey);

				this.NextKeyHashSha2.Entry = hashes.sha2.Entry;
				this.NextKeyHashSha3.Entry = hashes.sha3.Entry;
			}
		}

		protected override void SetType() {
			this.Type = Enums.KeyTypes.Secret;
		}
	}
}