using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations {

	public class JointAccountSnapshot<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE> : AccountSnapshot<ACCOUNT_ATTRIBUTE>, IJointAccountSnapshot<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE>
		where ACCOUNT_ATTRIBUTE : IAccountAttribute
		where JOINT_MEMBER_FEATURE : IJointMemberAccount {

		public int RequiredSignatures { get; set; }
		public ImmutableList<IJointMemberAccount> MemberAccountsBase => this.MemberAccounts.Cast<IJointMemberAccount>().ToImmutableList();
		public List<JOINT_MEMBER_FEATURE> MemberAccounts { get; set; } = new List<JOINT_MEMBER_FEATURE>();

		public void CreateNewCollectionEntry(out IJointMemberAccount result) {
			TypedCollectionExposureUtil<IJointMemberAccount>.CreateNewCollectionEntry(this.MemberAccounts, out result);
		}
		
		void ITypedCollectionExposure<IJointMemberAccount>.ClearCollection() {
			this.MemberAccounts.Clear();
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