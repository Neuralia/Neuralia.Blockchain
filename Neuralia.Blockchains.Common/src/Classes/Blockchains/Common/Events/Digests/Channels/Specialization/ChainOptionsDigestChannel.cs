using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization {
	public interface IChainOptionsDigestChannel : IDigestChannel {
	}

	public interface IChainOptionsDigestChannel<out CHAIN_OPTIONS_CARD> : IChainOptionsDigestChannel
		where CHAIN_OPTIONS_CARD : class, IChainOptionsDigestChannelCard {
		CHAIN_OPTIONS_CARD GetChainOptions(int id);

		CHAIN_OPTIONS_CARD[] GetChainOptionss();
	}

	public abstract class ChainOptionsDigestChannel<CHAIN_OPTIONS_CARD> : DigestChannel<ChainOptionsDigestChannel.ChainOptionsDigestChannelBands, CHAIN_OPTIONS_CARD, int, int, int>, IChainOptionsDigestChannel<CHAIN_OPTIONS_CARD>
		where CHAIN_OPTIONS_CARD : class, IChainOptionsDigestChannelCard, new() {

		public enum FileTypes {
			ChainOptions = 1
		}

		protected const string CHAIN_OPTIONS_CHANNEL = "chain-options";
		protected const string CHAIN_OPTIONS_BAND_NAME = "chain-options";

		protected ChainOptionsDigestChannel(string folder) : base(folder, CHAIN_OPTIONS_CHANNEL) {
		}

		public override DigestChannelType ChannelType => DigestChannelTypes.Instance.ChainOptions;

		public CHAIN_OPTIONS_CARD GetChainOptions(int id) {

			DigestChannelBandEntries<CHAIN_OPTIONS_CARD, ChainOptionsDigestChannel.ChainOptionsDigestChannelBands> results = this.channelBandIndexSet.QueryCard(id);

			if(results.IsEmpty) {
				return null;
			}

			return results[ChainOptionsDigestChannel.ChainOptionsDigestChannelBands.ChainOptions];
		}

		public CHAIN_OPTIONS_CARD[] GetChainOptionss() {
			// this works because we have only one channel for now
			SingleSqliteChannelBandIndex<ChainOptionsDigestChannel.ChainOptionsDigestChannelBands, CHAIN_OPTIONS_CARD, int, int, int> castedIndex = (SingleSqliteChannelBandIndex<ChainOptionsDigestChannel.ChainOptionsDigestChannelBands, CHAIN_OPTIONS_CARD, int, int, int>) this.channelBandIndexSet.BandIndices.Values.Single();

			return castedIndex.QueryCards().ToArray();
		}

		protected override void BuildBandsIndices() {

			SingleSqliteChannelBandIndex<ChainOptionsDigestChannel.ChainOptionsDigestChannelBands, CHAIN_OPTIONS_CARD, int, int, int> index = new SingleSqliteChannelBandIndex<ChainOptionsDigestChannel.ChainOptionsDigestChannelBands, CHAIN_OPTIONS_CARD, int, int, int>(CHAIN_OPTIONS_BAND_NAME, this.baseFolder, this.scopeFolder, ChainOptionsDigestChannel.ChainOptionsDigestChannelBands.ChainOptions, FileSystemWrapper.CreatePhysical(), key => key);
			this.InitIndexGenerator(index);

			this.channelBandIndexSet.AddIndex(1, index);
		}

		protected virtual void InitIndexGenerator(SingleSqliteChannelBandIndex<ChainOptionsDigestChannel.ChainOptionsDigestChannelBands, CHAIN_OPTIONS_CARD, int, int, int> generator) {
			generator.ModelBuilder = builder => {

				builder.Entity<CHAIN_OPTIONS_CARD>(o => {

				});

				builder.Entity<CHAIN_OPTIONS_CARD>().ToTable(CHAIN_OPTIONS_CHANNEL);
			};
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