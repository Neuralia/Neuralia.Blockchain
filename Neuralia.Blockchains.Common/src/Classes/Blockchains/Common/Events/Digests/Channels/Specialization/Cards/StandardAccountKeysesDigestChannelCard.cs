using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards {

	public interface IStandardAccountKeysDigestChannelCard : IAccountKeysSnapshot, IChannelBandSqliteProviderEntry<long> {
		AccountId AccountIdFull { get; set; }
		void ConvertToSnapshotEntry(IAccountKeysSnapshotEntry other, ICardUtils cardUtils);
	}

	public abstract class StandardAccountKeysDigestChannelCard : IStandardAccountKeysDigestChannelCard {

		public string CompositeKey { get; set; }
		public byte OrdinalId { get; set; }

		public long AccountId {
			get => this.AccountIdFull.ToLongRepresentation();
			set => this.AccountIdFull = value.ToAccountId();
		}

		public AccountId AccountIdFull { get; set; } = new AccountId();

		public byte[] PublicKey { get; set; }
		public string DeclarationTransactionId { get; set; }
		public long DeclarationBlockId { get; set; }
		public long Id { get; set; }

		public virtual void ConvertToSnapshotEntry(IAccountKeysSnapshotEntry other, ICardUtils cardUtils) {

			cardUtils.Copy(this, other);
		}

		protected abstract IStandardAccountKeysDigestChannelCard CreateCard();
	}
}