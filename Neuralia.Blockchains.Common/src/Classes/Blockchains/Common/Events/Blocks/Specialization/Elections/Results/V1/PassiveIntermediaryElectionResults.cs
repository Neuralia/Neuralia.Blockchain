using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1 {
	public interface IPassiveIntermediaryElectionResults : IIntermediaryElectionResults {
		Dictionary<AccountId, Enums.MiningTiers> ElectedCandidates { get; }
	}

	public abstract class PassiveIntermediaryElectionResults : IntermediaryElectionResults, IPassiveIntermediaryElectionResults {

		// results of elected of a passive election. 
		public Dictionary<AccountId, Enums.MiningTiers> ElectedCandidates { get; } = new Dictionary<AccountId, Enums.MiningTiers>();

		public override void Rehydrate(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree) {
			base.Rehydrate(rehydrator, transactionIndexesTree);

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydrator);
			int count = (int) adaptiveLong.Value;

			SafeArrayHandle typeBytes = (SafeArrayHandle)rehydrator.ReadArray(SpecialIntegerSizeArray.GetbyteSize(SpecialIntegerSizeArray.BitSizes.B0d5, count));
			using SpecialIntegerSizeArray electorTypesArray = new SpecialIntegerSizeArray(SpecialIntegerSizeArray.BitSizes.B0d5, typeBytes, count);

			this.ElectedCandidates.Clear();
			AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId> parameters = new AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId>();

			parameters.RehydrateExtraData = (accountId, offset, index, totalIndex, dh) => {

				this.ElectedCandidates.Add(accountId, (Enums.MiningTiers) electorTypesArray[totalIndex]);
			};

			AccountIdGroupSerializer.Rehydrate(rehydrator, true, parameters);
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetArray("ElectedCandidates", this.ElectedCandidates, (js, e) => {
				js.WriteObject(s => {
					s.SetProperty("AccountId", e.Key);
					s.SetProperty("ElectedTier", e.Value.ToString());
				});

			});
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.ElectedCandidates.OrderBy(e => e.Key).ToDictionary());

			return nodeList;
		}

		protected override ComponentVersion<ElectionContextType> SetIdentity() {
			return (ElectionContextTypes.Instance.Passive, 1, 0);
		}
	}
}