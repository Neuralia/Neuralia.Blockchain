using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {

	public interface IUserAccountSnapshotDigestChannel : IAccountSnapshotDigestChannel {
	}

	public interface IUserAccountSnapshotDigestChannel<out ACCOUNT_SNAPSHOT_CARD> : IUserAccountSnapshotDigestChannel, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IAccountSnapshotDigestChannelCard {
	}

	public abstract class UserAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD> : AccountSnapshotDigestChannel<UserAccountSnapshotDigestChannel.AccountSnapshotDigestChannelBands, ACCOUNT_SNAPSHOT_CARD>, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IStandardAccountSnapshotDigestChannelCard, new() {

		public enum FileTypes {
			UserAccounts = 1
		}

		protected const string ACCOUNTS_CHANNEL = "user-accounts";
		protected const string ACCOUNTS_BAND_NAME = "user-accounts";

		public UserAccountSnapshotDigestChannel(int groupSize, string folder) : base(UserAccountSnapshotDigestChannel.AccountSnapshotDigestChannelBands.UserAccounts, groupSize, folder, ACCOUNTS_CHANNEL, ACCOUNTS_BAND_NAME) {

		}

		protected override Enums.AccountTypes AccountType => Enums.AccountTypes.User;

		public override DigestChannelType ChannelType => DigestChannelTypes.Instance.UserAccountSnapshot;
	}

	public static class UserAccountSnapshotDigestChannel {
		[Flags]
		public enum AccountSnapshotDigestChannelBands {
			UserAccounts = 1
		}
	}

}