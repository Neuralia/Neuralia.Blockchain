using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {
	public interface IStandardAccountKeyChangeTransaction : IKeyedTransaction, IKeychange {

		CryptographicKey NewCryptographicKey { get; }
		SecretCryptographicKey NextSuperCryptographicKey { get; }
		XmssCryptographicKey XmssNewCryptographicKey { get; }
		bool IsNextSuperCryptographicKeySet { get; }
		bool IsChangingChangeKey { get; }
		bool IsChangingSuperKey { get; }
	}

	public abstract class StandardAccountKeyChangeTransaction : KeyedTransaction, IStandardAccountKeyChangeTransaction {

		public StandardAccountKeyChangeTransaction() {

		}

		public StandardAccountKeyChangeTransaction(byte changeOrdinalId) {
			this.ChangeOrdinalId = changeOrdinalId;

			ICryptographicKey changeKey = null;

			if((changeOrdinalId == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) || (changeOrdinalId == GlobalsService.MESSAGE_KEY_ORDINAL_ID) || (changeOrdinalId == GlobalsService.CHANGE_KEY_ORDINAL_ID)) {
				changeKey = new XmssCryptographicKey();
			} else {
				changeKey = new SecretCryptographicKey();
			}

			changeKey.Id = changeOrdinalId;
			this.Keyset.Add(changeKey);

			if(this.IsChangingChangeKey || this.IsChangingSuperKey) {
				this.Keyset.Add<SecretCryptographicKey>(GlobalsService.SUPER_KEY_ORDINAL_ID);
			}
		}

		public byte ChangeOrdinalId { get; set; }

		public bool IsChangingChangeKey => this.ChangeOrdinalId == GlobalsService.CHANGE_KEY_ORDINAL_ID;
		public bool IsChangingSuperKey => this.ChangeOrdinalId == GlobalsService.SUPER_KEY_ORDINAL_ID;

		public CryptographicKey NewCryptographicKey => (CryptographicKey) this.Keyset.Keys[this.ChangeOrdinalId];
		public XmssCryptographicKey XmssNewCryptographicKey => (XmssCryptographicKey) this.NewCryptographicKey;
		public SecretCryptographicKey NextSuperCryptographicKey => (SecretCryptographicKey) this.Keyset.Keys[GlobalsService.SUPER_KEY_ORDINAL_ID];
		public bool IsNextSuperCryptographicKeySet => (this.IsChangingChangeKey || this.IsChangingSuperKey) && this.Keyset.KeyLoaded(GlobalsService.SUPER_KEY_ORDINAL_ID);

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.ChangeOrdinalId);

			nodeList.Add(this.NewCryptographicKey);

			if(this.IsNextSuperCryptographicKeySet) {
				nodeList.Add(this.NextSuperCryptographicKey);
			}

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("ChangeOrdinalId", this.ChangeOrdinalId);
		}

		public override ImmutableList<AccountId> TargetAccounts => new[] {this.TransactionId.Account}.ToImmutableList();

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.ChangeOrdinalId = rehydrator.ReadByte();
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			dehydrator.Write(this.ChangeOrdinalId);
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.KEY_CHANGE, 1, 0);
		}
	}
}