using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards {

	public interface IChainOptionsDigestChannelCard : IChainOptionsSnapshot, IChannelBandSqliteProviderEntry<int> {

		void ConvertToSnapshotEntry(IChainOptionsSnapshot other, ICardUtils cardUtils);
	}

	public abstract class ChainOptionsDigestChannelCard : IChainOptionsDigestChannelCard {

		public int Id { get; set; }
		public string MaximumVersionAllowed { get; set; }
		public string MinimumWarningVersionAllowed { get; set; }
		public string MinimumVersionAllowed { get; set; }
		public int MaxBlockInterval { get; set; }

		public virtual void ConvertToSnapshotEntry(IChainOptionsSnapshot other, ICardUtils cardUtils) {

			cardUtils.Copy(this, other);
		}

		protected abstract IChainOptionsDigestChannelCard CreateCard();
	}
}