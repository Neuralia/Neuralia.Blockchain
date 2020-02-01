using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {

	public interface IJointAccountSnapshot : IAccountSnapshot, ITypedCollectionExposure<IJointMemberAccount> {
		int RequiredSignatures { get; set; }
		
		ImmutableList<IJointMemberAccount> MemberAccountsBase { get; }
	}

	public interface IJointAccountSnapshot<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE> : IAccountSnapshot<ACCOUNT_ATTRIBUTE>, IJointAccountSnapshot
		where ACCOUNT_ATTRIBUTE : IAccountAttribute
		where JOINT_MEMBER_FEATURE : IJointMemberAccount {

		List<JOINT_MEMBER_FEATURE> MemberAccounts { get; set; }
	}

}