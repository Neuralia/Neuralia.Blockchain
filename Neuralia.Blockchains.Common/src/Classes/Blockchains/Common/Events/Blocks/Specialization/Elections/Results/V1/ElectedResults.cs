using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1 {
	public interface IElectedResults : ISerializableCombo {
		List<TransactionId> Transactions { get; set; }
		Enums.ElectedPeerShareTypes PeerShareType { get; set; }

		AccountId DelegateAccountId { get; set; }
	}

	public class ElectedResults : IElectedResults {
		public List<TransactionId> Transactions { get; set; } = new List<TransactionId>();
		public Enums.ElectedPeerShareTypes PeerShareType { get; set; }
		public AccountId DelegateAccountId { get; set; }

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetProperty("PeerShareType", this.PeerShareType);
			jsonDeserializer.SetProperty("DelegateAccountId", this.DelegateAccountId);
			jsonDeserializer.SetArray("Transactions", this.Transactions.Select(t => t.ToString()));
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add((byte) this.PeerShareType);
			nodeList.Add(this.DelegateAccountId);
			nodeList.Add(this.Transactions.OrderBy(t => t));

			return nodeList;
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			// the transactoins, peertype and delegateAccountId are all dehydrated externally for optimization
		}
	}
}