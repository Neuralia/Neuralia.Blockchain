using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods {
	public interface IPrimariesBallotingMethod : IVersionable<PrimariesBallotingMethodType> {

		Dictionary<Enums.MiningTiers, long> MiningTierDifficulties { get; }

		SafeArrayHandle PerformBallot(SafeArrayHandle candidature, BlockElectionDistillate blockElectionDistillate, Enums.MiningTiers miningTier, AccountId miningAccount);
	}

	/// <summary>
	///     Different election methodologies to determine who gets to be elected in the primaries
	/// </summary>
	public abstract class PrimariesBallotingMethod : Versionable<PrimariesBallotingMethodType>, IPrimariesBallotingMethod {

		public PrimariesBallotingMethod() {
		}

		public Dictionary<Enums.MiningTiers, long> MiningTierDifficulties { get; } = new Dictionary<Enums.MiningTiers, long>();

		public abstract SafeArrayHandle PerformBallot(SafeArrayHandle candidature, BlockElectionDistillate blockElectionDistillate, Enums.MiningTiers miningTier, AccountId miningAccount);

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			MiningTierUtils.RehydrateLongMiningSet(this.MiningTierDifficulties, rehydrator, HashDifficultyUtils.Default512Difficulty);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			MiningTierUtils.AddStructuresArray(nodeList, this.MiningTierDifficulties);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("Mining Tiers Count", this.MiningTierDifficulties.Count);

			foreach(KeyValuePair<Enums.MiningTiers, long> entry in this.MiningTierDifficulties) {
				jsonDeserializer.SetProperty($"{entry.Key.ToString()}_Difficulty", entry.Value);
			}

		}
	}
}