using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.SequentialFile;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {
	public interface IStandardAccountKeysDigestChannel : IDigestChannel {
		SafeArrayHandle GetKey(long accountId, byte ordinal);
	}

	public interface IStandardAccountKeysDigestChannel<ACCOUNT_KEYS_CARD> : IStandardAccountKeysDigestChannel
		where ACCOUNT_KEYS_CARD : class, IStandardAccountKeysDigestChannelCard {
		List<ACCOUNT_KEYS_CARD> GetKeys(long accountId);
	}

	public abstract class StandardAccountKeysDigestChannel<ACCOUNT_KEYS_CARD> : DigestChannel<AccountKeysDigestChannel.AccountKeysDigestChannelBands, SafeArrayHandle, int, (long accountId, byte ordinal), (uint offset, uint length)>, IStandardAccountKeysDigestChannel<ACCOUNT_KEYS_CARD>
		where ACCOUNT_KEYS_CARD : class, IStandardAccountKeysDigestChannelCard, new() {

		public enum FileTypes {
			Keys = 1
		}

		protected const string KEYS_CHANNEL = "keys";
		protected const string KEYS_BAND_NAME = "keys";
		protected readonly int groupSize;

		public StandardAccountKeysDigestChannel(int groupSize, string folder) : base(folder, KEYS_CHANNEL) {
			this.groupSize = groupSize;
		}

		public override DigestChannelType ChannelType => DigestChannelTypes.Instance.StandardAccountKeys;

		public SafeArrayHandle GetKey(long accountId, byte ordinal) {

			var results = this.channelBandIndexSet.QueryCard((accountId, ordinal));

			if(results.IsEmpty) {
				return null;
			}

			return results[AccountKeysDigestChannel.AccountKeysDigestChannelBands.Keys].Branch();
		}

		public List<ACCOUNT_KEYS_CARD> GetKeys(long accountId) {

			// this works because we have only one channel for now
			var castedIndex = (DualKeySingleKeySequentialFileChannelBandIndex<AccountKeysDigestChannel.AccountKeysDigestChannelBands>) this.channelBandIndexSet.BandIndices.Values.Single();

			var results = castedIndex.QuerySubCards(accountId);

			var cards = new List<ACCOUNT_KEYS_CARD>();

			foreach(var result in results) {
				ACCOUNT_KEYS_CARD card = this.CreateNewCardInstance();

				card.Id = accountId;
				card.OrdinalId = result.Key;
				card.PublicKey = result.Value.Entries[AccountKeysDigestChannel.AccountKeysDigestChannelBands.Keys].ToExactByteArray();

				cards.Add(card);
			}

			return cards;
		}

		protected override void BuildBandsIndices() {

			this.channelBandIndexSet.AddIndex(1, new DualKeySingleKeySequentialFileChannelBandIndex<AccountKeysDigestChannel.AccountKeysDigestChannelBands>(KEYS_BAND_NAME, this.baseFolder, this.scopeFolder, this.groupSize, AccountKeysDigestChannel.AccountKeysDigestChannelBands.Keys, new FileSystem()));
		}

		protected override ComponentVersion<DigestChannelType> SetIdentity() {
			return (DigestChannelTypes.Instance.StandardAccountKeys, 1, 0);
		}

		protected abstract ACCOUNT_KEYS_CARD CreateNewCardInstance();
	}

	public static class AccountKeysDigestChannel {
		[Flags]
		public enum AccountKeysDigestChannelBands {
			Keys = 1
		}
	}
}