using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {

	public interface IStandardPresentationTransaction : IPresentationTransaction, IKeyedTransaction {

		void SetServer();
		bool IsServer { get; }
		XmssCryptographicKey TransactionCryptographicKey { get; }
		XmssCryptographicKey MessageCryptographicKey { get; }
		XmssCryptographicKey ChangeCryptographicKey { get; }
		XmssCryptographicKey SuperCryptographicKey { get; }
		
		Enums.AccountTypes AccountType { get; }

		bool IsTransactionKeyLoaded { get; }
		bool IsMessageKeyLoaded { get; }
		bool IsChangeKeyLoaded { get; }
		bool IsSuperKeyLoaded { get; }
	}

	public abstract class StandardPresentationTransaction : KeyedTransaction, IStandardPresentationTransaction {

		public StandardPresentationTransaction() {
			// TransactionKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.TRANSACTION_KEY_ORDINAL_ID);

			// MessageKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MESSAGE_KEY_ORDINAL_ID);

			// change key
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.CHANGE_KEY_ORDINAL_ID);

			// Superkey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.SUPER_KEY_ORDINAL_ID);
		}
		
		public void SetServer() {
			this.AccountType = Enums.AccountTypes.Server;
		}

		public bool IsServer => this.AccountType == Enums.AccountTypes.Server;
		
		public List<ITransactionAccountAttribute> Attributes { get; } = new List<ITransactionAccountAttribute>();

		/// <summary>
		///     This is a VERY special field. This account ID is not hashed, and will be provided filled by the moderator to assign
		///     a final public accountId to this new Account
		/// </summary>
		public AccountId AssignedAccountId { get; set; } = null;

		public long? CorrelationId { get; set; } = null;

		public XmssCryptographicKey TransactionCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.TRANSACTION_KEY_ORDINAL_ID];
		public XmssCryptographicKey MessageCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MESSAGE_KEY_ORDINAL_ID];
		public XmssCryptographicKey ChangeCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.CHANGE_KEY_ORDINAL_ID];
		public XmssCryptographicKey SuperCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.SUPER_KEY_ORDINAL_ID];

		public Enums.AccountTypes AccountType { get; private set; } = Enums.AccountTypes.User;

		public bool IsTransactionKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.TRANSACTION_KEY_ORDINAL_ID);
		public bool IsMessageKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MESSAGE_KEY_ORDINAL_ID);
		public bool IsChangeKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.CHANGE_KEY_ORDINAL_ID);
		public bool IsSuperKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.SUPER_KEY_ORDINAL_ID);

		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {
			HashNodeList nodeList = base.GetStructuresArray(types);

			nodeList.Add(this.CorrelationId);
			nodeList.Add(this.AccountType);
			
			//note: the THS results SHOULD NOT be hashed. neither should be the AssignedAccountId

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("AssignedAccountId", this.AssignedAccountId?.ToString());
			jsonDeserializer.SetProperty("CorrelationId", this.CorrelationId ?? 0);

			jsonDeserializer.SetProperty("AccountType", this.AccountType.ToString());
			
			jsonDeserializer.SetArray("Attributes", this.Attributes);
		}

		public override Enums.TransactionTargetTypes TargetType => Enums.TransactionTargetTypes.Range;
		public override AccountId[] ImpactedAccounts => TargetAccountsAndSender();
		public override AccountId[] TargetAccounts => GetAccountIds(this.AssignedAccountId);
		

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (PRESENTATION: TransactionTypes.Instance.STANDARD_PRESENTATION, 1, 0);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			bool isNull = rehydrator.ReadBool();

			this.AssignedAccountId = null;
			if(!isNull) {
				this.AssignedAccountId = new AccountId();
				this.AssignedAccountId.Rehydrate(rehydrator);
			}

			this.CorrelationId = rehydrator.ReadNullableLong();

			this.AccountType = rehydrator.ReadByteEnum<Enums.AccountTypes>();
			
			this.Attributes.Clear();
			byte accountFeatureCount = rehydrator.ReadByte();

			for(short i = 0; i < accountFeatureCount; i++) {
				ITransactionAccountAttribute attribute = this.CreateTransactionAccountFeature();

				attribute.Rehydrate(rehydrator);

				this.Attributes.Add(attribute);
			}
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			dehydrator.Write(this.AssignedAccountId == null);

			if(this.AssignedAccountId != null) {
				this.AssignedAccountId.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.CorrelationId);
			dehydrator.Write((byte)this.AccountType);

			dehydrator.Write((byte) this.Attributes.Count);

			foreach(ITransactionAccountAttribute feature in this.Attributes) {
				feature.Dehydrate(dehydrator);
			}
		}

		protected abstract ITransactionAccountAttribute CreateTransactionAccountFeature();
	}
}