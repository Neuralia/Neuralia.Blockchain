using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots {

	public interface IWalletJointAccountSnapshot : IJointAccountSnapshot, IWalletAccountSnapshot {
	}

	public interface IWalletJointAccountSnapshot<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE> : IJointAccountSnapshot<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE>, IWalletAccountSnapshot<ACCOUNT_ATTRIBUTE>, IWalletJointAccountSnapshot
		where ACCOUNT_ATTRIBUTE : IAccountAttribute
		where JOINT_MEMBER_FEATURE : IJointMemberAccount {
	}

	public abstract class WalletJointAccountSnapshot<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE> : WalletAccountSnapshot<ACCOUNT_ATTRIBUTE>, IWalletJointAccountSnapshot<ACCOUNT_ATTRIBUTE, JOINT_MEMBER_FEATURE>
		where ACCOUNT_ATTRIBUTE : AccountAttribute, new()
		where JOINT_MEMBER_FEATURE : JointMemberAccount, new() {

		public int RequiredSignatures { get; set; }
		public ImmutableList<IJointMemberAccount> MemberAccountsBase => this.MemberAccounts.Cast<IJointMemberAccount>().ToImmutableList();
		public List<JOINT_MEMBER_FEATURE> MemberAccounts { get; set; } = new List<JOINT_MEMBER_FEATURE>();

		void ITypedCollectionExposure<IJointMemberAccount>.ClearCollection() {
			this.MemberAccounts.Clear();
		}

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

		[BsonIgnore]
		ImmutableList<IJointMemberAccount> ITypedCollectionExposure<IJointMemberAccount>.CollectionCopy => TypedCollectionExposureUtil<IJointMemberAccount>.GetCollection(this.MemberAccounts);
	}
}