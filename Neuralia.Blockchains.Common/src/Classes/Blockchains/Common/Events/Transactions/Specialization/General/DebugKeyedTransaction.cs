using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General {
	public interface IDebugKeyedTransaction : IKeyedTransaction {
	}

	public class DebugKeyedTransaction : KeyedTransaction, IDebugKeyedTransaction {

		public override ImmutableList<AccountId> TargetAccounts => new List<AccountId>().ToImmutableList();

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.DEBUG_KEYED, 1, 0);
		}
	}
}