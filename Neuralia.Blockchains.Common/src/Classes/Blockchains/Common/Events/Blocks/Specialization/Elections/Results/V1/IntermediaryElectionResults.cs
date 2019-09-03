using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1 {
	public interface IIntermediaryElectionResults : IElectionResult {
		List<AccountId> ElectedCandidates { get; }
		IElectedResults CreateElectedResult();
	}

	public abstract class IntermediaryElectionResults : ElectionResult, IIntermediaryElectionResults {

		// results of elected of a passive election. 
		public List<AccountId> ElectedCandidates { get; } = new List<AccountId>();

		public override void Rehydrate(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree) {
			base.Rehydrate(rehydrator, transactionIndexesTree);

			this.ElectedCandidates.Clear();
			var parameters = new AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId>();

			parameters.RehydrateExtraData = (accountId, offset, index, dh) => {

				this.ElectedCandidates.Add(accountId);
			};

			AccountIdGroupSerializer.Rehydrate(rehydrator, true, parameters);
		}

		public abstract IElectedResults CreateElectedResult();

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetArray("ElectedCandidates", this.ElectedCandidates);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.ElectedCandidates);

			return nodeList;
		}
	}
}