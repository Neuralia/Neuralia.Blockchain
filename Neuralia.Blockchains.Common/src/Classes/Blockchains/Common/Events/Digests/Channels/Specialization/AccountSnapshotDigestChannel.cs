using System;

using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.SequentialFile;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Zio.FileSystems;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {

	public interface IAccountSnapshotDigestChannel : IDigestChannel {
	}

	public interface IAccountSnapshotDigestChannel<out ACCOUNT_SNAPSHOT_CARD> : IAccountSnapshotDigestChannel
		where ACCOUNT_SNAPSHOT_CARD : class, IAccountSnapshotDigestChannelCard {
		ACCOUNT_SNAPSHOT_CARD GetAccount(long accountId);
	}

	public abstract class AccountSnapshotDigestChannel<CHANEL_BANDS, ACCOUNT_SNAPSHOT_CARD> : DigestChannel<CHANEL_BANDS, SafeArrayHandle, int, long, (uint offset, uint length)>, IAccountSnapshotDigestChannel<ACCOUNT_SNAPSHOT_CARD>
		where CHANEL_BANDS : struct, Enum
		where ACCOUNT_SNAPSHOT_CARD : class, IAccountSnapshotDigestChannelCard, new() {
		protected readonly string bandName;
		protected readonly CHANEL_BANDS channelBand;

		protected readonly int groupSize;

		protected abstract Enums.AccountTypes AccountType { get; }

		public AccountSnapshotDigestChannel(CHANEL_BANDS channelBand, int groupSize, string folder, string channelName, string bandName) : base(folder, channelName) {
			this.groupSize = groupSize;
			this.channelBand = channelBand;
			this.bandName = bandName;
		}

		public ACCOUNT_SNAPSHOT_CARD GetAccount(long accountId) {

			var results = this.channelBandIndexSet.QueryCard(accountId);

			if(results.IsEmpty) {
				return null;
			}

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(results[this.channelBand]);

			Enums.AccountTypes accountType = AccountSnapshotDigestChannelCard.GetAccountType(rehydrator);

			ACCOUNT_SNAPSHOT_CARD card = this.CreateNewCardInstance();

			card.Rehydrate(rehydrator);

			card.AccountId = new AccountId(accountId, this.AccountType).ToLongRepresentation();

			return card;

		}

		protected override void BuildBandsIndices() {

			this.channelBandIndexSet.AddIndex(1, new SingleKeyTrippleFileChannelBandIndex<CHANEL_BANDS>(this.bandName, this.baseFolder, this.scopeFolder, this.groupSize, this.channelBand, FileSystemWrapper.CreatePhysical()));
		}

		protected override ComponentVersion<DigestChannelType> SetIdentity() {
			return (this.ChannelType, 1, 0);
		}

		protected abstract ACCOUNT_SNAPSHOT_CARD CreateNewCardInstance();
	}

}