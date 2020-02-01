using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods {
	public interface IPrimariesBallotingMethod : IVersionable<PrimariesBallotingMethodType> {

		long FirstTierDifficulty { get; set; }
		long SecondTierDifficulty { get; set; }
		long ThirdTierDifficulty { get; set; }
		
		SafeArrayHandle PerformBallot(SafeArrayHandle candidature, BlockElectionDistillate blockElectionDistillate, Enums.MiningTiers miningTier, AccountId miningAccount);
	}

	/// <summary>
	///     Different election methodologies to determine who gets to be elected in the primaries
	/// </summary>
	public abstract class PrimariesBallotingMethod : Versionable<PrimariesBallotingMethodType>, IPrimariesBallotingMethod {

		public long FirstTierDifficulty { get; set; }
		public long SecondTierDifficulty { get; set; }
		public long ThirdTierDifficulty { get; set; }

		public abstract SafeArrayHandle PerformBallot(SafeArrayHandle candidature, BlockElectionDistillate blockElectionDistillate, Enums.MiningTiers miningTier, AccountId miningAccount);

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveLong1_9 entry = new AdaptiveLong1_9();
			entry.Rehydrate(rehydrator);
			this.FirstTierDifficulty = entry.Value;
			
			entry = new AdaptiveLong1_9();
			entry.Rehydrate(rehydrator);
			this.SecondTierDifficulty = entry.Value;
			
			entry = new AdaptiveLong1_9();
			entry.Rehydrate(rehydrator);
			this.ThirdTierDifficulty = entry.Value;
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.FirstTierDifficulty);
			nodeList.Add(this.SecondTierDifficulty);
			nodeList.Add(this.ThirdTierDifficulty);
			
			return nodeList;
		}
		
		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("FirstTierDifficulty", this.FirstTierDifficulty);
			jsonDeserializer.SetProperty("SecondTierDifficulty", this.SecondTierDifficulty);
			jsonDeserializer.SetProperty("ThirdTierDifficulty", this.ThirdTierDifficulty);
		}
	}
}