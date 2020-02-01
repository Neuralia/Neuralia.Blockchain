using System.Collections.Immutable;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Gated {

	public interface IGatedJudgementTransaction : ITransaction{
		uint CorrelationId { get; set; }
		
		AccountId VerifierAccountId { get; }
		AccountId SenderAccountId { get; set; }
		AccountId ReceiverAccountId { get; set; }
		GatedJudgementTransaction.GatedJudgements Judgement { get; set; }
	}
	
	public abstract class GatedJudgementTransaction : Transaction, IGatedJudgementTransaction{
		public enum GatedJudgements:byte { Rejected = 0, Accepted = 1
		}

		public uint CorrelationId { get; set; }
		public AccountId VerifierAccountId => this.TransactionId.Account;
		public AccountId SenderAccountId { get; set; } = new AccountId();
		public AccountId ReceiverAccountId { get; set; } = new AccountId();
		public GatedJudgements Judgement { get; set; } = GatedJudgements.Rejected;

		
		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add((byte)this.Judgement);
			nodeList.Add(this.CorrelationId);
			nodeList.Add(this.SenderAccountId);
			nodeList.Add(this.ReceiverAccountId);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("Judgement", this.Judgement.ToString());
			
			jsonDeserializer.SetProperty("CorrelationId", this.CorrelationId);
			jsonDeserializer.SetProperty("VerifierAccountId", this.VerifierAccountId);
			jsonDeserializer.SetProperty("SenderAccountId", this.SenderAccountId);
			jsonDeserializer.SetProperty("ReceiverAccountId", this.ReceiverAccountId);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.Judgement = (GatedJudgements)rehydrator.ReadByte();
			
			AdaptiveLong1_9 data = new AdaptiveLong1_9();
			data.Rehydrate(rehydrator);
			this.CorrelationId = (uint)data.Value;
			
			this.SenderAccountId.Rehydrate(rehydrator);
			this.ReceiverAccountId.Rehydrate(rehydrator);
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			dehydrator.Write((byte)this.Judgement);
			
			AdaptiveLong1_9 data = new AdaptiveLong1_9();
			data.Value = this.CorrelationId;
			data.Dehydrate(dehydrator);
			
			this.SenderAccountId.Dehydrate(dehydrator);
			this.ReceiverAccountId.Dehydrate(dehydrator);
		}
		
		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.GATED_JUDGEMENT_TRANSACTION, 1, 0);
		}

		public override ImmutableList<AccountId> TargetAccounts => new [] {this.TransactionId.Account, this.SenderAccountId, this.ReceiverAccountId}.ToImmutableList();


	}
}