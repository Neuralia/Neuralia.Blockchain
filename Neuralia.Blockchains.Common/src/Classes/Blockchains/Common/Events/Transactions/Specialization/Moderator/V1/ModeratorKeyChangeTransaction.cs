using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {
	public interface IModeratorKeyChangeTransaction : IModerationKeyedTransaction {

		ICryptographicKey NewCryptographicKey { get; }
		Enums.KeyTypes KeyType { get; }
	}

	public abstract class ModeratorKeyChangeTransaction : ModerationKeyedTransaction, IModeratorKeyChangeTransaction {

		private byte ordinalId;

		public ModeratorKeyChangeTransaction() {
			// used by rehydrationonly
		}

		public ModeratorKeyChangeTransaction(byte ordinalId, ICryptographicKey cryptographicKey) {
			this.ordinalId = ordinalId;
			this.KeyType = cryptographicKey.Type;
			this.Keyset.Add(cryptographicKey, ordinalId);
		}

		public ModeratorKeyChangeTransaction(byte ordinalId, Enums.KeyTypes keyType) {
			this.ordinalId = ordinalId;
			this.KeyType = keyType;
			this.Keyset.Add(ordinalId, this.KeyType);
		}

		public ICryptographicKey NewCryptographicKey => this.Keyset.Keys[this.ordinalId];

		public Enums.KeyTypes KeyType { get; private set; }

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add((byte) this.KeyType);
			nodeList.Add(this.ordinalId);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("ordinalId", this.ordinalId);
			jsonDeserializer.SetProperty("KeyType", this.KeyType);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.ordinalId = rehydrator.ReadByte();
			this.KeyType = (Enums.KeyTypes) rehydrator.ReadByte();
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			dehydrator.Write(this.ordinalId);
			dehydrator.Write((byte) this.KeyType);
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.MODERATION_KEY_CHANGE, 1, 0);
		}
	}
}