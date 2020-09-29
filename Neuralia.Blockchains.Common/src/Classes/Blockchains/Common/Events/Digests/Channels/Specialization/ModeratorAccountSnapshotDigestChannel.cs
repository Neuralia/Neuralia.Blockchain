using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {

	public interface IModeratorAccountSnapshotDigestChannel : IAccountSnapshotDigestChannel {
	}

	public interface IModeratorAccountSnapshotDigestChannel<out ACCOUNT_SNAPSHOT_CARD> : IModeratorAccountSnapshotDigestChannel, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IAccountSnapshotDigestChannelCard {
	}

	public abstract class ModeratorAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD> : AccountSnapshotDigestChannel<ModeratorAccountSnapshotDigestChannel.ModeratorAccountSnapshotDigestChannelBands, ACCOUNT_SNAPSHOT_CARD>, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IStandardAccountSnapshotDigestChannelCard, new() {

		public enum FileTypes {
			ModeratorAccounts = 1
		}

		protected const string ACCOUNTS_CHANNEL = "moderator-accounts";
		protected const string ACCOUNTS_BAND_NAME = "moderator-accounts";

		public ModeratorAccountSnapshotDigestChannel(int groupSize, string folder) : base(ModeratorAccountSnapshotDigestChannel.ModeratorAccountSnapshotDigestChannelBands.ModeratorAccounts, groupSize, folder, ACCOUNTS_CHANNEL, ACCOUNTS_BAND_NAME) {

		}

		protected override Enums.AccountTypes AccountType => Enums.AccountTypes.Moderator;

		public override DigestChannelType ChannelType => DigestChannelTypes.Instance.ModeratorAccountSnapshot;
	}

	public static class ModeratorAccountSnapshotDigestChannel {
		[Flags]
		public enum ModeratorAccountSnapshotDigestChannelBands {
			ModeratorAccounts = 1
		}
	}

}