using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections {
	public interface IElectionCandidacyMessage : IBlockchainMessage {
		int MaturityBlockHash { get; set; }
		BlockId BlockId { get; set; }
		
		AdaptiveLong1_9 SimpleAnswer { get; set; }
		AdaptiveLong1_9 HardAnswer { get; set; }

		AccountId AccountId { get; set; }
	}

	public abstract class ElectionCandidacyMessage : BlockchainMessage, IElectionCandidacyMessage {

		public BlockId BlockId { get; set; } = new BlockId();
		public AdaptiveLong1_9 SimpleAnswer { get; set; }
		public AdaptiveLong1_9 HardAnswer { get; set; }
		public AccountId AccountId { get; set; } = new AccountId();

		/// <summary>
		///     the hash of the block at maturity time
		/// </summary>
		public int MaturityBlockHash { get; set; }

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			this.BlockId.Rehydrate(rehydrator);
			this.MaturityBlockHash = rehydrator.ReadInt();
			this.AccountId.Rehydrate(rehydrator);

			this.SimpleAnswer = null;
			bool hasValue = rehydrator.ReadBool();

			if(hasValue) {
				this.SimpleAnswer = new AdaptiveLong1_9();
				this.SimpleAnswer.Rehydrate(rehydrator);
			}
			
			this.HardAnswer = null;
			hasValue = rehydrator.ReadBool();

			if(hasValue) {
				this.HardAnswer = new AdaptiveLong1_9();
				this.HardAnswer.Rehydrate(rehydrator);
			}
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			this.BlockId.Dehydrate(dehydrator);
			dehydrator.Write(this.MaturityBlockHash);
			this.AccountId.Dehydrate(dehydrator);

			dehydrator.Write(this.SimpleAnswer != null);

			if(this.SimpleAnswer != null) {
				this.SimpleAnswer.Dehydrate(dehydrator);
			}
			
			dehydrator.Write(this.HardAnswer != null);

			if(this.HardAnswer != null) {
				this.HardAnswer.Dehydrate(dehydrator);
			}
		}
	}
}