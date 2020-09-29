using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections {

	public interface IPassiveElectionCandidacyMessage : IElectionCandidacyMessage {
		List<TransactionId> SelectedTransactions { get; }
	}

	/// <summary>
	///     in a passive election, the election office already elected us through our registration. we only need now to
	///     indicate what we choose to become truth in the chain
	/// </summary>
	public class PassiveElectionCandidacyMessage : ElectionCandidacyMessage, IPassiveElectionCandidacyMessage {

		public List<TransactionId> SelectedTransactions { get; } = new List<TransactionId>();

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			byte count = rehydrator.ReadByte();

			for(byte i = 0; i < count; i++) {
				TransactionId transactionId = new TransactionId();
				transactionId.Rehydrate(rehydrator);
				this.SelectedTransactions.Add(transactionId);
			}
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			dehydrator.Write((byte) this.SelectedTransactions.Count);

			foreach(TransactionId transaction in this.SelectedTransactions) {
				transaction.Dehydrate(dehydrator);
			}
		}
		
		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.SelectedTransactions.Count);

			foreach(var entry in this.SelectedTransactions.OrderBy(t => t)) {
				nodesList.Add(entry);
			}

			return nodesList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetArray("SelectedTransactions", this.SelectedTransactions);
		}

		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.PASSIVE_ELECTION_CANDIDACY, 1, 0);
		}
	}
}