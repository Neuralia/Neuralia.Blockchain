using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets {
	public struct RejectedTransaction : ISerializableCombo {
		
		public RejectedTransaction(TransactionId transactionId, int reason) {
			this.TransactionId = transactionId;
			this.Reason = new RejectionCode((ushort)reason);
		}
		
		public RejectedTransaction(TransactionId transactionId, ushort reason) {
			this.TransactionId = transactionId;
			this.Reason = new RejectionCode(reason);
		}
		
		public RejectedTransaction(TransactionId transactionId, RejectionCode reason) {
			this.TransactionId = transactionId;
			this.Reason = reason;
		}

		public TransactionId TransactionId { get; set; }
		public RejectionCode Reason { get; set; }

		
		public void Dehydrate(IDataDehydrator dehydrator) {
			this.TransactionId.Dehydrate(dehydrator);
			dehydrator.Write(this.Reason.Value);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.TransactionId = new TransactionId();
			this.TransactionId.Rehydrate(rehydrator);
			this.Reason = rehydrator.ReadUShort();
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.TransactionId);
			nodeList.Add(this.Reason.Value);

			return nodeList;
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("TransactionId", this.TransactionId);
			jsonDeserializer.SetProperty("Reason", this.Reason.Value);
		}

	}
}