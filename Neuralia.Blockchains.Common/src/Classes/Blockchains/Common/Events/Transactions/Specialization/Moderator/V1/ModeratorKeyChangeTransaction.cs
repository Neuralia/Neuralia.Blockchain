using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {
	public interface IModeratorKeyChangeTransaction : IModerationKeyedTransaction, IKeychange {

		ICryptographicKey NewCryptographicKey { get; }
		byte OrdinalId  { get; }
	}

	public abstract class ModeratorKeyChangeTransaction : ModerationKeyedTransaction, IModeratorKeyChangeTransaction {

		public byte OrdinalId => this.NewCryptographicKey?.Ordinal??0;

		public ModeratorKeyChangeTransaction() {
			// used by rehydrationonly
		}

		public ModeratorKeyChangeTransaction(ICryptographicKey cryptographicKey) {
			this.Keyset.Add(cryptographicKey);
		}

		public ModeratorKeyChangeTransaction(byte ordinalId, CryptographicKeyType keyType) {
			this.Keyset.Add(ordinalId, keyType);
		}

		public ICryptographicKey NewCryptographicKey => this.Keyset.Keys.Values.SingleOrDefault();

		public override Enums.TransactionTargetTypes TargetType => Enums.TransactionTargetTypes.All;
		public override AccountId[] ImpactedAccounts =>this.TargetAccounts;
		public override AccountId[] TargetAccounts =>  this.GetSenderList();

		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {

			HashNodeList nodeList = base.GetStructuresArray(types);

			nodeList.Add(this.OrdinalId);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("ordinalId", this.OrdinalId);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.MODERATION_KEY_CHANGE, 1, 0);
		}
	}
}