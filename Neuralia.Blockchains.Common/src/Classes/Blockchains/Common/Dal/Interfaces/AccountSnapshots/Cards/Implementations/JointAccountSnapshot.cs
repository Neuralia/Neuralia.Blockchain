using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations {

	public class JointAccountSnapshot<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE> : AccountSnapshot<ACCOUNT_FEATURE>, IJointAccountSnapshot<ACCOUNT_FEATURE, JOINT_MEMBER_FEATURE>
		where ACCOUNT_FEATURE : IAccountFeature
		where JOINT_MEMBER_FEATURE : IJointMemberAccount {

		public int RequiredSignatures { get; set; }
		public List<JOINT_MEMBER_FEATURE> MemberAccounts { get; set; }

		public void CreateNewCollectionEntry(out IJointMemberAccount result) {
			TypedCollectionExposureUtil<IJointMemberAccount>.CreateNewCollectionEntry(this.MemberAccounts, out result);
		}

		public void AddCollectionEntry(IJointMemberAccount entry) {
			TypedCollectionExposureUtil<IJointMemberAccount>.AddCollectionEntry(entry, this.MemberAccounts);
		}

		public void RemoveCollectionEntry(Func<IJointMemberAccount, bool> predicate) {
			TypedCollectionExposureUtil<IJointMemberAccount>.RemoveCollectionEntry(predicate, this.MemberAccounts);
		}

		public IJointMemberAccount GetCollectionEntry(Func<IJointMemberAccount, bool> predicate) {
			return TypedCollectionExposureUtil<IJointMemberAccount>.GetCollectionEntry(predicate, this.MemberAccounts);
		}

		public List<IJointMemberAccount> GetCollectionEntries(Func<IJointMemberAccount, bool> predicate) {
			return TypedCollectionExposureUtil<IJointMemberAccount>.GetCollectionEntries(predicate, this.MemberAccounts);
		}

		ImmutableList<IJointMemberAccount> ITypedCollectionExposure<IJointMemberAccount>.CollectionCopy => TypedCollectionExposureUtil<IJointMemberAccount>.GetCollection(this.MemberAccounts);
	}
}