using System.Collections.Immutable;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {

	public interface ISetAccountCorrelationTransaction : ITransaction {
	}

	public abstract class SetAccountCorrelationTransaction : Transaction, ISetAccountCorrelationTransaction {

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			return hashNodeList;
		}

		public override ImmutableList<AccountId> TargetAccounts => new[] {this.TransactionId.Account}.ToImmutableList();

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.SET_ACCOUNT_CORRELATION, 1, 0);
		}
	}
}