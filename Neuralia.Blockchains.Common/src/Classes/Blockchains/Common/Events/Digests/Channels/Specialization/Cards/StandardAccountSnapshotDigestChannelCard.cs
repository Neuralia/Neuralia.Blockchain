using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards {

	public interface IStandardAccountSnapshotDigestChannelCard : IAccountSnapshotDigestChannelCard, IStandardAccountSnapshot<IAccountAttribute> {
		void ConvertToSnapshotEntry(IStandardAccountSnapshot other, ICardUtils cardUtils);
	}

	public abstract class StandardAccountSnapshotDigestChannelCard : AccountSnapshotDigestChannelCard, IAccountSnapshotDigestChannelCard {

		public void ConvertToSnapshotEntry(IStandardAccountSnapshot other, ICardUtils cardUtils) {
			cardUtils.Copy(this, other);
		}
	}
}