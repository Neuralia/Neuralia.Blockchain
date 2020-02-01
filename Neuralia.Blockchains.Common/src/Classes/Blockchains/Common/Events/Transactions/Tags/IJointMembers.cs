using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags {
	public interface IJointMembers {
		List<ITransactionJointAccountMember> MemberAccounts { get; }
	}
}