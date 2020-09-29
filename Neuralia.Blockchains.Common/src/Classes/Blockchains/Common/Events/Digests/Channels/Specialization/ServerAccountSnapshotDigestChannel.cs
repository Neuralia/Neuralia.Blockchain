using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {

	public interface IServerAccountSnapshotDigestChannel : IAccountSnapshotDigestChannel {
	}

	public interface IServerAccountSnapshotDigestChannel<out ACCOUNT_SNAPSHOT_CARD> : IServerAccountSnapshotDigestChannel, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IAccountSnapshotDigestChannelCard {
	}

	public abstract class ServerAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD> : AccountSnapshotDigestChannel<ServerAccountSnapshotDigestChannel.ServerAccountSnapshotDigestChannelBands, ACCOUNT_SNAPSHOT_CARD>, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IStandardAccountSnapshotDigestChannelCard, new() {

		public enum FileTypes {
			ServerAccounts = 1
		}

		protected const string ACCOUNTS_CHANNEL = "server-accounts";
		protected const string ACCOUNTS_BAND_NAME = "server-accounts";

		public ServerAccountSnapshotDigestChannel(int groupSize, string folder) : base(ServerAccountSnapshotDigestChannel.ServerAccountSnapshotDigestChannelBands.ServerAccounts, groupSize, folder, ACCOUNTS_CHANNEL, ACCOUNTS_BAND_NAME) {

		}

		protected override Enums.AccountTypes AccountType => Enums.AccountTypes.Server;

		public override DigestChannelType ChannelType => DigestChannelTypes.Instance.ServerAccountSnapshot;
	}

	public static class ServerAccountSnapshotDigestChannel {
		[Flags]
		public enum ServerAccountSnapshotDigestChannelBands {
			ServerAccounts = 1
		}
	}

}