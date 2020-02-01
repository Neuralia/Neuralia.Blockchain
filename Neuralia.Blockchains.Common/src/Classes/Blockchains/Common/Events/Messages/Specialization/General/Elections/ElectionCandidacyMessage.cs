using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections {
	public interface IElectionCandidacyMessage : IBlockchainMessage {
		int MaturityBlockHash { get; set; }
		BlockId BlockId { get; set; }
		
		AdaptiveLong1_9 SecondTierAnswer { get; set; }
		AdaptiveLong1_9 DigestAnswer { get; set; }
		AdaptiveLong1_9 FirstTierAnswer { get; set; }

		AccountId AccountId { get; set; }
	}

	public abstract class ElectionCandidacyMessage : BlockchainMessage, IElectionCandidacyMessage {

		public BlockId BlockId { get; set; } = new BlockId();
		public AdaptiveLong1_9 SecondTierAnswer { get; set; }
		public AdaptiveLong1_9 DigestAnswer { get; set; }
		public AdaptiveLong1_9 FirstTierAnswer { get; set; }
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

			this.SecondTierAnswer = null;
			bool hasValue = rehydrator.ReadBool();

			if(hasValue) {
				this.SecondTierAnswer = new AdaptiveLong1_9();
				this.SecondTierAnswer.Rehydrate(rehydrator);
			}
			
			this.DigestAnswer = null;
			hasValue = rehydrator.ReadBool();

			if(hasValue) {
				this.DigestAnswer = new AdaptiveLong1_9();
				this.DigestAnswer.Rehydrate(rehydrator);
			}
			
			this.FirstTierAnswer = null;
			hasValue = rehydrator.ReadBool();

			if(hasValue) {
				this.FirstTierAnswer = new AdaptiveLong1_9();
				this.FirstTierAnswer.Rehydrate(rehydrator);
			}
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			this.BlockId.Dehydrate(dehydrator);
			dehydrator.Write(this.MaturityBlockHash);
			this.AccountId.Dehydrate(dehydrator);

			dehydrator.Write(this.SecondTierAnswer != null);

			if(this.SecondTierAnswer != null) {
				this.SecondTierAnswer.Dehydrate(dehydrator);
			}
			
			dehydrator.Write(this.DigestAnswer != null);

			if(this.DigestAnswer != null) {
				this.DigestAnswer.Dehydrate(dehydrator);
			}
			
			dehydrator.Write(this.FirstTierAnswer != null);

			if(this.FirstTierAnswer != null) {
				this.FirstTierAnswer.Dehydrate(dehydrator);
			}
		}
	}
}