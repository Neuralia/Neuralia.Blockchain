using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections {
	public interface IElectionCandidacyMessage : IBlockchainMessage {
		int MaturityBlockHash { get; set; }
		long BlockId { get; set; }

		AccountId AccountId { get; set; }
	}

	public abstract class ElectionCandidacyMessage : BlockchainMessage, IElectionCandidacyMessage {

		public long BlockId { get; set; }
		public AccountId AccountId { get; set; } = new AccountId();

		/// <summary>
		///     the hash of the block at maturity time
		/// </summary>
		public int MaturityBlockHash { get; set; }

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			this.BlockId = rehydrator.ReadLong();
			this.MaturityBlockHash = rehydrator.ReadInt();
			this.AccountId.Rehydrate(rehydrator);
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			dehydrator.Write(this.BlockId);
			dehydrator.Write(this.MaturityBlockHash);
			this.AccountId.Dehydrate(dehydrator);
		}
	}
}