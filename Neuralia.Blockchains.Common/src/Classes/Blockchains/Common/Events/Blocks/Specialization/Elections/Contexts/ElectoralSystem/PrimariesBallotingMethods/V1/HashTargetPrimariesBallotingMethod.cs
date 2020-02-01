using System.Linq;
using System.Numerics;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods.V1 {

	/// <summary>
	///     This is a simple hash compare to a minimum target value base on a difficulty setting.
	/// </summary>
	public class HashTargetPrimariesBallotingMethod : PrimariesBallotingMethod {

		protected override ComponentVersion<PrimariesBallotingMethodType> SetIdentity() {
			return (PrimariesBallotingMethodTypes.Instance.HashTarget, 1, 0);
		}

		public override SafeArrayHandle PerformBallot(SafeArrayHandle candidature, BlockElectionDistillate blockElectionDistillate, Enums.MiningTiers miningTier, AccountId miningAccount) {

			long difficulty = 0;

			string tierName = "";
			if(miningTier.HasFlag(Enums.MiningTiers.FirstTier)) {
				difficulty = this.FirstTierDifficulty;
				tierName = "first";
			}
			else if(miningTier.HasFlag(Enums.MiningTiers.SecondTier)) {
				difficulty = this.SecondTierDifficulty;
				tierName = "second";
			}
			else{
				difficulty = this.ThirdTierDifficulty;
				tierName = "third";
			}
		
			BigInteger hashTarget = HashDifficultyUtils.GetHash512TargetByIncrementalDifficulty(difficulty);
			BigInteger currentBallotHash = HashDifficultyUtils.GetBigInteger(candidature);

			Log.Verbose($"Comparing our candidacy ballot {currentBallotHash} with the {tierName} tier election target {hashTarget}");

			if(currentBallotHash < hashTarget) {
				// wow, we got in! :D
				return candidature;
			}

			return null; // return null if we are not elected
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			return nodeList;
		}
		
		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("Name", "HashTarget");
		}
	}
}