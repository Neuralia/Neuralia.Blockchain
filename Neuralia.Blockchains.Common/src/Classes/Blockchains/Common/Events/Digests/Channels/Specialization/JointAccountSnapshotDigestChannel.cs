using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {

	public interface IJointAccountSnapshotDigestChannel : IAccountSnapshotDigestChannel {
	}

	public interface IJointAccountSnapshotDigestChannel<out ACCOUNT_SNAPSHOT_CARD> : IJointAccountSnapshotDigestChannel, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IAccountSnapshotDigestChannelCard {
	}

	public abstract class JointAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD> : AccountSnapshotDigestChannel<JointAccountSnapshotDigestChannel.AccountSnapshotDigestChannelBands, ACCOUNT_SNAPSHOT_CARD>, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where ACCOUNT_SNAPSHOT_CARD : class, IJointAccountSnapshotDigestChannelCard, new() {

		public enum FileTypes {
			JointAccounts = 1
		}

		protected const string ACCOUNTS_CHANNEL = "joint-accounts";
		protected const string ACCOUNTS_BAND_NAME = "joint-accounts";

		public JointAccountSnapshotDigestChannel(int groupSize, string folder) : base(JointAccountSnapshotDigestChannel.AccountSnapshotDigestChannelBands.JointAccounts, groupSize, folder, ACCOUNTS_CHANNEL, ACCOUNTS_BAND_NAME) {

		}

		public override DigestChannelType ChannelType => DigestChannelTypes.Instance.JointAccountSnapshot;
		
		protected override Enums.AccountTypes AccountType => Enums.AccountTypes.Joint;
	}

	public static class JointAccountSnapshotDigestChannel {
		[Flags]
		public enum AccountSnapshotDigestChannelBands {
			JointAccounts = 1
		}
	}
}