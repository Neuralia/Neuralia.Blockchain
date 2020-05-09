using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods {
	public interface IRepresentativeBallotingRules : IVersionableSerializable {

		Dictionary<Enums.MiningTiers, ushort> MiningTierTotals { get; }

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

		public RepresentativeBallotingRules(Dictionary<Enums.MiningTiers, ushort> miningTierTotals) {
			foreach((Enums.MiningTiers key, ushort value) in miningTierTotals) {
				this.SetTotal(key, value);
			}
		}

		public Dictionary<Enums.MiningTiers, ushort> MiningTierTotals { get; } = new Dictionary<Enums.MiningTiers, ushort>();

		public ushort GetTotal(Enums.MiningTiers tier) {
			return this.MiningTierTotals.GetTierValue(tier);
		}

		public void SetTotal(Enums.MiningTiers tier, ushort value) {

			this.MiningTierTotals.SetTierValue(tier, value);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			MiningTierUtils.AddStructuresArray(nodeList, this.MiningTierTotals);

			return nodeList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			MiningTierUtils.RehydrateMiningSet<ushort, AdaptiveShort1_2>(this.MiningTierTotals, 0, rehydrator, v => (ushort) v);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("Mining Tiers Count", this.MiningTierTotals.Count);

			foreach(KeyValuePair<Enums.MiningTiers, ushort> entry in this.MiningTierTotals) {
				jsonDeserializer.SetProperty($"{entry.Key.ToString()}_Total", entry.Value);
			}
		}

		public IBinarySerializable BaseVersion => this.Version;
	}
}