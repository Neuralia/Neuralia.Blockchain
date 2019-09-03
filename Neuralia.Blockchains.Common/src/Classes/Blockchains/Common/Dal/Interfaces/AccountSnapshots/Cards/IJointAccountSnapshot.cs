using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {

	public interface IJointAccountSnapshot : IAccountSnapshot, ITypedCollectionExposure<IJointMemberAccount> {
		int RequiredSignatures { get; set; }
	}

	public interface IJointAccountSnapshot<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE> : IAccountSnapshot<ACCOUNT_FEATURE>, IJointAccountSnapshot
		where ACCOUNT_FEATURE : IAccountFeature
		where JOINT_MEMBER_FEATURE : IJointMemberAccount {

		List<JOINT_MEMBER_FEATURE> MemberAccounts { get; set; }
	}

}