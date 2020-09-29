using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.DataAccess.Interfaces;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.Gates {
	public interface IGatesDal : IDalInterfaceBase {
		
		Task SetKeyGate(AccountId accountId, IdKeyUseIndexSet keyIndexLock);
		Task SetKeyGates(List<(AccountId AccountId, IdKeyUseIndexSet keyGate)> keyGates);
		Task<KeyUseIndexSet> GetKeyGate(AccountId accountId, byte ordinal);
		Task ClearKeyGates(List<AccountId> accountIds);
	}

	public interface IGatesDal<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES> : IGatesDal
		where STANDARD_ACCOUNT_GATES : class, IStandardAccountGates 
		where JOINT_ACCOUNT_GATES : class, IJointAccountGates {
	}
}