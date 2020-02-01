using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Specialized;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts {

	public interface IElectionContext : IVersionable<ElectionContextType>, IJsonSerializable {

		byte Maturity { get; set; }
		byte Publication { get; set; }
		AdaptiveShort1_2 FirstTierMaximumElectedTransactionCount { get; set; }
		AdaptiveShort1_2 SecondTierMaximumElectedTransactionCount { get; set; }
		AdaptiveShort1_2 ThirdTierMaximumElectedTransactionCount { get; set; }

		ICandidatureMethod CandidatureMethod { get; set; }
		IPrimariesBallotingMethod PrimariesBallotingMethod { get; set; }

		ElectionModes ElectionMode { get; }

		void Rehydrate(IDataRehydrator rehydrator, IElectionContextRehydrationFactory electionContextRehydrationFactory);
	}

	public abstract class ElectionContext : Versionable<ElectionContextType>, IElectionContext {

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
		///     This is the maximum amount of transactions an elected can select
		/// </summary>
		public AdaptiveShort1_2 FirstTierMaximumElectedTransactionCount { get; set; } = new AdaptiveShort1_2();
		
		/// <summary>
		///     This is the maximum amount of transactions an elected can select
		/// </summary>
		public AdaptiveShort1_2 SecondTierMaximumElectedTransactionCount { get; set; } = new AdaptiveShort1_2();
		
		/// <summary>
		///     This is the maximum amount of transactions an elected can select
		/// </summary>
		public AdaptiveShort1_2 ThirdTierMaximumElectedTransactionCount { get; set; } = new AdaptiveShort1_2();

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
			this.FirstTierMaximumElectedTransactionCount.Rehydrate(rehydrator);
			this.SecondTierMaximumElectedTransactionCount.Rehydrate(rehydrator);
			this.ThirdTierMaximumElectedTransactionCount.Rehydrate(rehydrator);

			this.CandidatureMethod = CandidatureMethodRehydrator.Rehydrate(rehydrator);
			this.PrimariesBallotingMethod = PrimariesBallotingMethodRehydrator.Rehydrate(rehydrator);

		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Maturity);
			nodeList.Add(this.Publication);
			
			nodeList.Add(this.FirstTierMaximumElectedTransactionCount);
			nodeList.Add(this.SecondTierMaximumElectedTransactionCount);
			nodeList.Add(this.ThirdTierMaximumElectedTransactionCount);

			nodeList.Add(this.CandidatureMethod);
			nodeList.Add(this.PrimariesBallotingMethod);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("Maturity", this.Maturity);
			jsonDeserializer.SetProperty("Publish", this.Publication);

			jsonDeserializer.SetProperty("FirstTierMaximumElectedTransactionCount", this.FirstTierMaximumElectedTransactionCount);
			jsonDeserializer.SetProperty("SecondTierMaximumElectedTransactionCount", this.SecondTierMaximumElectedTransactionCount);
			jsonDeserializer.SetProperty("ThirdTierMaximumElectedTransactionCount", this.ThirdTierMaximumElectedTransactionCount);

			jsonDeserializer.SetProperty("CandidatureMethod", this.CandidatureMethod);
			jsonDeserializer.SetProperty("PrimariesBallotingMethod", this.PrimariesBallotingMethod);
		}
	}
}