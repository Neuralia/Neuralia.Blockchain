using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.SequentialFile;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {
	public interface IModeratorAccountKeysDigestChannel : IDigestChannel {
		SafeArrayHandle GetKey(long accountId, byte ordinal);
	}

	public interface IModeratorAccountKeysDigestChannel<out ACCOUNT_KEYS_CARD> : IStandardAccountKeysDigestChannel
		where ACCOUNT_KEYS_CARD : class, IStandardAccountKeysDigestChannelCard {
		ACCOUNT_KEYS_CARD[] GetKeys(long accountId);
	}

	public abstract class ModeratorAccountKeysDigestChannel<ACCOUNT_KEYS_CARD> : DigestChannel<ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands, SafeArrayHandle, int, (long accountId, byte ordinal), (uint offset, uint length)>, IModeratorAccountKeysDigestChannel<ACCOUNT_KEYS_CARD>
		where ACCOUNT_KEYS_CARD : class, IStandardAccountKeysDigestChannelCard, new() {

		public enum FileTypes {
			ModeratorKeys = 1
		}

		protected const string KEYS_CHANNEL = "moderator-keys";
		protected const string KEYS_BAND_NAME = "moderator-keys";
		protected readonly int groupSize;

		public ModeratorAccountKeysDigestChannel(int groupSize, string folder) : base(folder, KEYS_CHANNEL) {
			this.groupSize = groupSize;
		}

		public override DigestChannelType ChannelType => DigestChannelTypes.Instance.ModeratorAccountKeys;

		public SafeArrayHandle GetKey(long accountId, byte ordinal) {

			DigestChannelBandEntries<SafeArrayHandle, ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands> results = this.channelBandIndexSet.QueryCard((accountId, ordinal));

			if(results.IsEmpty) {
				return null;
			}

			return results[ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands.ModeratorKeys].Branch();
		}

		public ACCOUNT_KEYS_CARD[] GetKeys(long accountId) {

			// this works because we have only one channel for now
			DualKeySingleKeyTrippleFileChannelBandIndex<ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands> castedIndex = (DualKeySingleKeyTrippleFileChannelBandIndex<ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands>) this.channelBandIndexSet.BandIndices.Values.Single();

			Dictionary<byte, DigestChannelBandEntries<SafeArrayHandle, ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands>> results = castedIndex.QuerySubCards(accountId);

			List<ACCOUNT_KEYS_CARD> cards = new List<ACCOUNT_KEYS_CARD>();

			foreach(KeyValuePair<byte, DigestChannelBandEntries<SafeArrayHandle, ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands>> result in results) {
				ACCOUNT_KEYS_CARD card = this.CreateNewCardInstance();

				card.Id = new AccountId(accountId, Enums.AccountTypes.Moderator).ToLongRepresentation();
				card.OrdinalId = result.Key;
				card.PublicKey = result.Value.Entries[ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands.ModeratorKeys].ToExactByteArray();

				card.CompositeKey = this.GetCardUtils().GenerateCompositeKey(card);

				cards.Add(card);
			}

			return cards.ToArray();
		}

		protected abstract ICardUtils GetCardUtils();

		protected override void BuildBandsIndices() {

			using FileSystemWrapper fileSystem = FileSystemWrapper.CreatePhysical();
			this.channelBandIndexSet.AddIndex(1, new DualKeySingleKeyTrippleFileChannelBandIndex<ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands>(KEYS_BAND_NAME, this.baseFolder, this.scopeFolder, this.groupSize, ModeratorAccountKeysDigestChannel.ModeratorAccountKeysDigestChannelBands.ModeratorKeys, fileSystem));
		}

		protected override ComponentVersion<DigestChannelType> SetIdentity() {
			return (DigestChannelTypes.Instance.ModeratorAccountKeys, 1, 0);
		}

		protected abstract ACCOUNT_KEYS_CARD CreateNewCardInstance();
	}

	public static class ModeratorAccountKeysDigestChannel {
		[Flags]
		public enum ModeratorAccountKeysDigestChannelBands {
			ModeratorKeys = 1
		}
	}
}