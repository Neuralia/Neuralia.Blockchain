using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {

	public interface IStandardAccountSnapshotDigestChannel : IAccountSnapshotDigestChannel {
	}

	public interface IStandardAccountSnapshotDigestChannel<out ACCOUNT_SNAPSHOT_CARD> : IStandardAccountSnapshotDigestChannel, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IAccountSnapshotDigestChannelCard {
	}

	public abstract class StandardAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD> : AccountSnapshotDigestChannel<StandardAccountSnapshotDigestChannel.AccountSnapshotDigestChannelBands, ACCOUNT_SNAPSHOT_CARD>, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IStandardAccountSnapshotDigestChannelCard, new() {

		public enum FileTypes {
			StandardAccounts = 1
		}

		protected const string ACCOUNTS_CHANNEL = "standard-accounts";
		protected const string ACCOUNTS_BAND_NAME = "standard-accounts";

		public StandardAccountSnapshotDigestChannel(int groupSize, string folder) : base(StandardAccountSnapshotDigestChannel.AccountSnapshotDigestChannelBands.StandardAccounts, groupSize, folder, ACCOUNTS_CHANNEL, ACCOUNTS_BAND_NAME) {

		}

		protected override Enums.AccountTypes AccountType => Enums.AccountTypes.Standard;

		public override DigestChannelType ChannelType => DigestChannelTypes.Instance.StandardAccountSnapshot;
	}

	public static class StandardAccountSnapshotDigestChannel {
		[Flags]
		public enum AccountSnapshotDigestChannelBands {
			StandardAccounts = 1
		}
	}

}