using System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods {
	public interface IRepresentativeBallotingRules : IVersionableSerializable {
		ushort FirstTierTotal { get; set; }
		ushort SecondTierTotal { get; set; }
		ushort ThirdTierTotal { get; set; }
		ushort GetTotal(Enums.MiningTiers tier);
		void SetTotal(Enums.MiningTiers tier, ushort value);
	}

	public interface IRepresentativeBallotingRules<T> : IVersionable<T>, IRepresentativeBallotingRules
		where T : SimpleUShort<T>, new() {
	}

	/// <summary>
	///     By what method do we select who will get to be the prime elected candidate and the representative of the election
	///     and by what rules should we operate
	/// </summary>
	public abstract class RepresentativeBallotingRules<T> : Versionable<T>, IRepresentativeBallotingRules<T>
		where T : SimpleUShort<T>, new() {

		public RepresentativeBallotingRules() {

		}

		public RepresentativeBallotingRules(ushort firstTierTotal, ushort secondTierTotal, ushort thirdTierTotal) {
			this.FirstTierTotal = firstTierTotal;
			this.SecondTierTotal = secondTierTotal;
			this.ThirdTierTotal = thirdTierTotal;
		}

		public ushort FirstTierTotal { get; set; } = 10;
		public ushort SecondTierTotal { get; set; } = 10;
		public ushort ThirdTierTotal { get; set; } = 10;

		public ushort GetTotal(Enums.MiningTiers tier) {

			if(tier == Enums.MiningTiers.FirstTier) {
				return this.FirstTierTotal;
			}

			if(tier == Enums.MiningTiers.SecondTier) {
				return this.SecondTierTotal;
			}

			if(tier == Enums.MiningTiers.ThirdTier) {
				return this.ThirdTierTotal;
			}

			throw new ArgumentException();
		}

		public void SetTotal(Enums.MiningTiers tier, ushort value) {

			if(tier == Enums.MiningTiers.FirstTier) {
				this.FirstTierTotal = value;
			}

			if(tier == Enums.MiningTiers.SecondTier) {
				this.SecondTierTotal = value;
			}

			if(tier == Enums.MiningTiers.ThirdTier) {
				this.ThirdTierTotal = value;
			}

		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.FirstTierTotal);
			nodeList.Add(this.SecondTierTotal);
			nodeList.Add(this.ThirdTierTotal);

			return nodeList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveShort1_2 entry = new AdaptiveShort1_2();
			entry.Rehydrate(rehydrator);
			this.FirstTierTotal = entry.Value;

			entry = new AdaptiveShort1_2();
			entry.Rehydrate(rehydrator);
			this.SecondTierTotal = entry.Value;

			entry = new AdaptiveShort1_2();
			entry.Rehydrate(rehydrator);
			this.ThirdTierTotal = entry.Value;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("FirstTierTotal", this.FirstTierTotal);
			jsonDeserializer.SetProperty("SecondTierTotal", this.SecondTierTotal);
			jsonDeserializer.SetProperty("ThirdTierTotal", this.ThirdTierTotal);
		}

		public IBinarySerializable BaseVersion => this.Version;
	}
}