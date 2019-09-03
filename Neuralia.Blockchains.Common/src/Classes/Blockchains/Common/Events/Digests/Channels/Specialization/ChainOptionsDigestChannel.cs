using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {
	public interface IChainOptionsDigestChannel : IDigestChannel {
	}

	public interface IChainOptionsDigestChannel<CHAIN_OPTIONS_CARD> : IChainOptionsDigestChannel
		where CHAIN_OPTIONS_CARD : class, IChainOptionsDigestChannelCard {
		CHAIN_OPTIONS_CARD GetChainOptions(int id);

		List<CHAIN_OPTIONS_CARD> GetChainOptionss();
	}

	public abstract class ChainOptionsDigestChannel<CHAIN_OPTIONS_CARD> : DigestChannel<ChainOptionsDigestChannel.ChainOptionsDigestChannelBands, CHAIN_OPTIONS_CARD, int, int, int>, IChainOptionsDigestChannel<CHAIN_OPTIONS_CARD>
		where CHAIN_OPTIONS_CARD : class, IChainOptionsDigestChannelCard, new() {

		public enum FileTypes {
			Certificates = 1
		}

		protected const string CERTIFICATES_CHANNEL = "certificates";
		protected const string CERTIFICATES_BAND_NAME = "certificates";

		protected ChainOptionsDigestChannel(string folder) : base(folder, CERTIFICATES_CHANNEL) {
		}

		public override DigestChannelType ChannelType => DigestChannelTypes.Instance.ChainOptions;

		public CHAIN_OPTIONS_CARD GetChainOptions(int id) {

			var results = this.channelBandIndexSet.QueryCard(id);

			if(results.IsEmpty) {
				return null;
			}

			return results[ChainOptionsDigestChannel.ChainOptionsDigestChannelBands.ChainOptions];
		}

		public List<CHAIN_OPTIONS_CARD> GetChainOptionss() {
			// this works because we have only one channel for now
			var castedIndex = (SingleSqliteChannelBandIndex<ChainOptionsDigestChannel.ChainOptionsDigestChannelBands, CHAIN_OPTIONS_CARD, int, int, int>) this.channelBandIndexSet.BandIndices.Values.Single();

			return castedIndex.QueryCards();
		}

		protected override void BuildBandsIndices() {

			this.channelBandIndexSet.AddIndex(1, new SingleSqliteChannelBandIndex<ChainOptionsDigestChannel.ChainOptionsDigestChannelBands, CHAIN_OPTIONS_CARD, int, int, int>(CERTIFICATES_BAND_NAME, this.baseFolder, this.scopeFolder, ChainOptionsDigestChannel.ChainOptionsDigestChannelBands.ChainOptions, new FileSystem(), key => key));
		}

		protected override ComponentVersion<DigestChannelType> SetIdentity() {
			return (DigestChannelTypes.Instance.ChainOptions, 1, 0);
		}
	}

	public static class ChainOptionsDigestChannel {
		[Flags]
		public enum ChainOptionsDigestChannelBands {
			ChainOptions = 1
		}
	}
}