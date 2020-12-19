using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures {

	public interface ITransactionJointAccountMember : ISerializableCombo, IJointMemberAccount {
	}

	public abstract class TransactionJointAccountMember : ITransactionJointAccountMember {

		public enum Actions : byte {
			Add = 1,
			Update = 2,
			Remove = 3
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			AccountId accountId = new AccountId();
			accountId.Rehydrate(rehydrator);
			this.AccountId = accountId.ToLongRepresentation();
			this.Required = rehydrator.ReadBool();

		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			AccountId accountId = this.AccountId.ToAccountId();
			accountId.Dehydrate(dehydrator);
			dehydrator.Write(this.Required);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.AccountId);
			hashNodeList.Add(this.Required);

			return hashNodeList;
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("AccountId", this.AccountId.ToAccountId());
			jsonDeserializer.SetProperty("Required", this.Required);
		}

		public long AccountId { get; set; }
		public bool Required { get; set; }
	}
}