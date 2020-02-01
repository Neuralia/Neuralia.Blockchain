using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Specialized;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions.V1 {
	
	public interface IBlockTransactionIdElectionQuestion : IElectionBlockQuestion {
		BlockId BlockId { get; set; }
		AdaptiveLong1_9 TransactionIndex { get; set; }
		BlockTransactionIdElectionQuestion.QuestionTransactionSection SelectedTransactionSection { get; set; }
		BlockTransactionIdElectionQuestion.QuestionTransactionIdComponents SelectedComponent { get; set; }
	}
	
	public class BlockTransactionIdElectionQuestion : ElectionBlockQuestion, IBlockTransactionIdElectionQuestion {
		public enum QuestionTransactionSection:byte {
			
			ConfirmedMasterTransactions = 1,
			ConfirmedTransactions = 2,
			RejectedTransactions = 3,
			Block = 4
		}
		
		public enum QuestionTransactionIdComponents:byte {
			
			AccountId = 1,
			Timestamp = 2,
			Scope=3,
			Hash = 4,
			BlockTimestamp = 5
		}

		public BlockId BlockId { get; set; } = new BlockId();

		public AdaptiveLong1_9 TransactionIndex { get; set; }

		public QuestionTransactionSection SelectedTransactionSection { get; set; } = QuestionTransactionSection.ConfirmedTransactions;
		public QuestionTransactionIdComponents SelectedComponent { get; set; } = QuestionTransactionIdComponents.AccountId;

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty(nameof(this.BlockId), this.BlockId.ToString());
			jsonDeserializer.SetProperty(nameof(this.TransactionIndex), (this.TransactionIndex==null?"": this.TransactionIndex.ToString()));
			
			jsonDeserializer.SetProperty(nameof(this.SelectedTransactionSection), this.SelectedTransactionSection.ToString());
			jsonDeserializer.SetProperty(nameof(this.SelectedComponent), this.SelectedComponent.ToString());
		}

		protected override ComponentVersion<ElectionQuestionType> SetIdentity() {
			return (ElectionQuestionTypes.Instance.BlockTransactionIndex, 1,0);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.BlockId.Rehydrate(rehydrator);

			this.TransactionIndex = null;
			bool isIndexSet = rehydrator.ReadBool();

			if(isIndexSet) {
				this.TransactionIndex= new AdaptiveLong1_9();
				this.TransactionIndex.Rehydrate(rehydrator);
			}

			this.SelectedTransactionSection = (QuestionTransactionSection)rehydrator.ReadByte();
			this.SelectedComponent = (QuestionTransactionIdComponents)rehydrator.ReadByte();
		}
		

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.BlockId);
			nodeList.Add(this.TransactionIndex);
			
			nodeList.Add((byte)this.SelectedTransactionSection);
			nodeList.Add((byte)this.SelectedComponent);

			return nodeList;
		}
	}
}