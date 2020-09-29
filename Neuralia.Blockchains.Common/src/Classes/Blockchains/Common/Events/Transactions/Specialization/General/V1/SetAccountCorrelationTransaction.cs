using System.Collections.Immutable;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {

	public interface ISetAccountCorrelationTransaction : ITransaction {
	}

	public abstract class SetAccountCorrelationTransaction : Transaction, ISetAccountCorrelationTransaction {

		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {
			HashNodeList hashNodeList = base.GetStructuresArray(types);

			return hashNodeList;
		}

		public override Enums.TransactionTargetTypes TargetType => Enums.TransactionTargetTypes.Range;
		public override AccountId[] ImpactedAccounts =>this.TargetAccounts;
		public override AccountId[] TargetAccounts => this.GetSenderList();
		

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.SET_ACCOUNT_CORRELATION, 1, 0);
		}
	}
}