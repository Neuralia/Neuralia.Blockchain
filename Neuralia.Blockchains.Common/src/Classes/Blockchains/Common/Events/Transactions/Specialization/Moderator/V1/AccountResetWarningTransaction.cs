using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {
	public interface IAccountResetWarningTransaction : IModerationTransaction {

		byte Echo { get; set; }
		AccountId Account { get; set; }
	}

	public class AccountResetWarningTransaction : ModerationTransaction, IAccountResetWarningTransaction {

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			hashNodeList.Add(this.Account);
			hashNodeList.Add(this.Echo);

			return hashNodeList;
		}

		public byte Echo { get; set; }
		public AccountId Account { get; set; } = new AccountId();

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("Echo", this.Echo);
			jsonDeserializer.SetProperty("Account", this.Account);
		}

		protected override void RehydrateContents(ChannelsEntries<IDataRehydrator> dataChannels, ITransactionRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(dataChannels, rehydrationFactory);

			this.Account.Rehydrate(dataChannels.ContentsData);
			this.Echo = dataChannels.ContentsData.ReadByte();
		}

		protected override void DehydrateContents(ChannelsEntries<IDataDehydrator> dataChannels) {
			base.DehydrateContents(dataChannels);

			this.Account.Dehydrate(dataChannels.ContentsData);
			dataChannels.ContentsData.Write(this.Echo);
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.MODERATION_ACCOUNT_RESET_WARNING, 1, 0);
		}
		
		public override ImmutableList<AccountId> TargetAccounts => new []{this.Account}.ToImmutableList();
	}
}