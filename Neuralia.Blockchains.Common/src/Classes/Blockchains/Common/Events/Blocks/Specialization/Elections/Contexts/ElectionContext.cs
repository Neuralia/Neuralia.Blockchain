using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts {

	public interface IElectionContext : IVersionable<ElectionContextType>, IJsonSerializable {

		byte Maturity { get; set; }
		byte Publication { get; set; }
		ImmutableList<Enums.MiningTiers> MiningTiers { get; }

		Dictionary<Enums.MiningTiers, ushort> MaximumElectedTransactionCount { get; }

		ICandidatureMethod CandidatureMethod { get; set; }
		IPrimariesBallotingMethod PrimariesBallotingMethod { get; set; }

		ElectionModes ElectionMode { get; }

		void Rehydrate(IDataRehydrator rehydrator, IElectionContextRehydrationFactory electionContextRehydrationFactory);
	}

	public abstract class ElectionContext : Versionable<ElectionContextType>, IElectionContext {

		public ElectionContext() {
		}

		/// <summary>
		///     Duration in amount of blocks before the election begins. if it is passive, then the mods will send Intermediary
		///     results.
		/// </summary>
		public byte Maturity { get; set; }

		/// <summary>
		///     Duration in amount of blocks before the election polls close and the results be published
		/// </summary>
		public byte Publication { get; set; }

		/// <summary>
		///     HThe tiers enabled in this context
		/// </summary>
		public ImmutableList<Enums.MiningTiers> MiningTiers { get; set; }

		public Dictionary<Enums.MiningTiers, ushort> MaximumElectedTransactionCount { get; } = new Dictionary<Enums.MiningTiers, ushort>();

		// election components

		/// <summary>
		///     How do we apply for the election
		/// </summary>
		public ICandidatureMethod CandidatureMethod { get; set; }

		/// <summary>
		///     How do we perform the primaries elections
		/// </summary>
		public IPrimariesBallotingMethod PrimariesBallotingMethod { get; set; }

		public ElectionModes ElectionMode => this.Version.Type == ElectionContextTypes.Instance.Active ? ElectionModes.Active : ElectionModes.Passive;

		public override sealed void Rehydrate(IDataRehydrator rehydrator) {
			throw new NotSupportedException();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator, IElectionContextRehydrationFactory electionContextRehydrationFactory) {

			base.Rehydrate(rehydrator);

			this.Maturity = rehydrator.ReadByte();
			this.Publication = rehydrator.ReadByte();

			this.MiningTiers = MiningTierUtils.RehydrateTierList(rehydrator);

			MiningTierUtils.RehydrateUShortMiningSet(this.MaximumElectedTransactionCount, rehydrator, 1);

			this.CandidatureMethod = CandidatureMethodRehydrator.Rehydrate(rehydrator);
			this.PrimariesBallotingMethod = PrimariesBallotingMethodRehydrator.Rehydrate(rehydrator);

		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Maturity);
			nodeList.Add(this.Publication);

			nodeList.Add(MiningTierUtils.TiersToFlags(this.MiningTiers));

			MiningTierUtils.AddStructuresArray(nodeList, this.MaximumElectedTransactionCount);

			nodeList.Add(this.CandidatureMethod);
			nodeList.Add(this.PrimariesBallotingMethod);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("Maturity", this.Maturity);
			jsonDeserializer.SetProperty("Publication", this.Publication);
			jsonDeserializer.SetProperty("MiningTierCount", this.MiningTiers.Count);

			jsonDeserializer.SetArray("miningTiers", this.MiningTiers);
			
			foreach(Enums.MiningTiers tier in this.MiningTiers) {
				jsonDeserializer.SetProperty($"{tier.ToString()}_MaximumElectedTransactionCount", this.MaximumElectedTransactionCount[tier]);
			}

			jsonDeserializer.SetProperty("CandidatureMethod", this.CandidatureMethod);
			jsonDeserializer.SetProperty("PrimariesBallotingMethod", this.PrimariesBallotingMethod);
		}
	}
}