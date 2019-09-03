using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards {

	public interface IJointAccountSnapshotDigestChannelCard : IAccountSnapshotDigestChannelCard, IJointAccountSnapshot<IAccountFeature, IJointMemberAccount> {
		void ConvertToSnapshotEntry(IJointAccountSnapshot other, ICardUtils cardUtils);
	}

	public abstract class JointAccountSnapshotDigestChannelCard : AccountSnapshotDigestChannelCard, IJointAccountSnapshotDigestChannelCard {

		public int RequiredSignatures { get; set; }
		public List<IJointMemberAccount> MemberAccounts { get; set; } = new List<IJointMemberAccount>();

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.RequiredSignatures = rehydrator.ReadInt();
			int count = rehydrator.ReadByte();

			this.MemberAccounts.Clear();

			foreach(long accountId in AccountIdGroupSerializer.RehydrateLong(rehydrator, true)) {
				IJointMemberAccount entry = this.CreateJointMemberAccount();
				entry.AccountId = accountId;

				this.MemberAccounts.Add(entry);
			}
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.RequiredSignatures);

			AccountIdGroupSerializer.Dehydrate(this.MemberAccounts.ToDictionary(e => e.AccountId), dehydrator, true);
		}

		public void ConvertToSnapshotEntry(IJointAccountSnapshot other, ICardUtils cardUtils) {
			cardUtils.Copy(this, other);
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

		ImmutableList<IJointMemberAccount> ITypedCollectionExposure<IJointMemberAccount>.CollectionCopy => TypedCollectionExposureUtil<IJointMemberAccount>.GetCollection(this.MemberAccounts);

		protected abstract IJointMemberAccount CreateJointMemberAccount();
	}
}