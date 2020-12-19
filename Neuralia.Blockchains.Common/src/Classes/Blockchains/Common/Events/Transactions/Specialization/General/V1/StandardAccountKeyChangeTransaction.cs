using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {
	public interface IStandardAccountKeyChangeTransaction : IKeyedTransaction, IKeychange {

		CryptographicKey NewCryptographicKey { get; }
		bool IsChangingChangeKey { get; }
		bool IsChangingSuperKey { get; }
	}

	public abstract class StandardAccountKeyChangeTransaction : KeyedTransaction, IStandardAccountKeyChangeTransaction {

		public StandardAccountKeyChangeTransaction() {

		}

		public StandardAccountKeyChangeTransaction(byte changeOrdinalOrdinal) {
			this.ChangeOrdinalOrdinal = changeOrdinalOrdinal;

			ICryptographicKey changeKey = null;

			if(changeOrdinalOrdinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				changeKey = new XmssmtCryptographicKey();
			} 
			else if(changeOrdinalOrdinal == GlobalsService.VALIDATOR_SECRET_KEY_ORDINAL_ID) {
				changeKey = new NTRUPrimeCryptographicKey();
			}
			else {
				changeKey = new XmssCryptographicKey();
			}

			changeKey.Ordinal = changeOrdinalOrdinal;
			this.Keyset.Add(changeKey);
		}

		public byte ChangeOrdinalOrdinal { get; set; }

		public bool IsChangingChangeKey => this.ChangeOrdinalOrdinal == GlobalsService.CHANGE_KEY_ORDINAL_ID;
		public bool IsChangingSuperKey => this.ChangeOrdinalOrdinal == GlobalsService.SUPER_KEY_ORDINAL_ID;

		public CryptographicKey NewCryptographicKey => (CryptographicKey) this.Keyset.Keys[this.ChangeOrdinalOrdinal];
		
		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {

			HashNodeList nodeList = base.GetStructuresArray(types);

			nodeList.Add(this.ChangeOrdinalOrdinal);

			nodeList.Add(this.NewCryptographicKey);
			
			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("ChangeOrdinalId", this.ChangeOrdinalOrdinal);
		}

		public override Enums.TransactionTargetTypes TargetType => Enums.TransactionTargetTypes.Range;
		public override AccountId[] ImpactedAccounts => this.TargetAccounts;
		public override AccountId[] TargetAccounts => this.GetSenderList();
		

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.ChangeOrdinalOrdinal = rehydrator.ReadByte();
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			dehydrator.Write(this.ChangeOrdinalOrdinal);
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.KEY_CHANGE, 1, 0);
		}
	}
}