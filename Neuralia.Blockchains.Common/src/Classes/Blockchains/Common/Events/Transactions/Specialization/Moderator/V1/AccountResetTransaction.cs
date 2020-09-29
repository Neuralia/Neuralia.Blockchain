using System.Collections.Immutable;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {
	public interface IAccountResetTransaction : IModerationKeyedTransaction {

		AccountId Account { get; set; }

		SafeArrayHandle RecoverySecret { get; }
		SafeArrayHandle NextRecoveryHash { get; }

		XmssCryptographicKey TransactionCryptographicKey { get; }
		XmssCryptographicKey MessageCryptographicKey { get; }
		XmssCryptographicKey ChangeCryptographicKey { get; }
		XmssmtCryptographicKey SuperCryptographicKey { get; }
		
		bool IsTransactionKeyLoaded { get; }
		bool IsMessageKeyLoaded { get; }
		bool IsChangeKeyLoaded { get; }
		bool IsSuperKeyLoaded { get; }
	}

	public class AccountResetTransaction : ModerationKeyedTransaction, IAccountResetTransaction {

		public AccountResetTransaction() {
			// TransactionKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.TRANSACTION_KEY_ORDINAL_ID);

			// MessageKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MESSAGE_KEY_ORDINAL_ID);

			// ChangeKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.CHANGE_KEY_ORDINAL_ID);

			// Superkey
			this.Keyset.Add<SecretDoubleCryptographicKey>(GlobalsService.SUPER_KEY_ORDINAL_ID);
		}
		
		public AccountId Account { get; set; }

		public SafeArrayHandle RecoverySecret { get; } = SafeArrayHandle.Create();
		public SafeArrayHandle NextRecoveryHash { get; } = SafeArrayHandle.Create();

		public XmssCryptographicKey TransactionCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.TRANSACTION_KEY_ORDINAL_ID];
		public XmssCryptographicKey MessageCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MESSAGE_KEY_ORDINAL_ID];
		public XmssCryptographicKey ChangeCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.CHANGE_KEY_ORDINAL_ID];
		public XmssmtCryptographicKey SuperCryptographicKey => (XmssmtCryptographicKey) this.Keyset.Keys[GlobalsService.SUPER_KEY_ORDINAL_ID];

		public bool IsTransactionKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.TRANSACTION_KEY_ORDINAL_ID);
		public bool IsMessageKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MESSAGE_KEY_ORDINAL_ID);
		public bool IsChangeKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.CHANGE_KEY_ORDINAL_ID);
		public bool IsSuperKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.SUPER_KEY_ORDINAL_ID);

		
		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {
			HashNodeList nodeList = base.GetStructuresArray(types);

			nodeList.Add(this.Account);
			nodeList.Add(this.RecoverySecret);
			nodeList.Add(this.NextRecoveryHash);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("RecoverySecret", this.RecoverySecret);
			jsonDeserializer.SetProperty("NextRecoveryHash", this.NextRecoveryHash);
		}

		public override Enums.TransactionTargetTypes TargetType => Enums.TransactionTargetTypes.Range;
		public override AccountId[] ImpactedAccounts => this.TargetAccounts;
		public override AccountId[] TargetAccounts => new[] {this.Account};

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.Account.Rehydrate(rehydrator);
			this.RecoverySecret.Entry = rehydrator.ReadNonNullableArray();
			this.NextRecoveryHash.Entry = rehydrator.ReadNonNullableArray();
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			this.Account.Dehydrate(dehydrator);
			dehydrator.WriteNonNullable(this.RecoverySecret);
			dehydrator.WriteNonNullable(this.NextRecoveryHash);

		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.MODERATION_ACCOUNT_RESET, 1, 0);
		}
	}
}