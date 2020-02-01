using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {

	public interface IChainOperatingRulesTransaction : IModerationTransaction {
		SoftwareVersion MaximumVersionAllowed { get; set; }
		SoftwareVersion MinimumWarningVersionAllowed { get; set; }
		SoftwareVersion MinimumVersionAllowed { get; set; }
		int MaxBlockInterval { get; set; }
		bool AllowGossipPresentations { get; set; }
	}

	/// <summary>
	///     A special transaction that allows us to determine the current operating settingsBase
	///     for the chain. Among others, we
	///     can specify the accetable highest versions for various components of our system.
	///     This is useful for older clients who wont recognize newer versions, to know if they are acceptable, or just spam.
	/// </summary>
	/// <typeparam name="REHYDRATION_FACTORY"></typeparam>
	public abstract class ChainOperatingRulesTransaction : ModerationTransaction, IChainOperatingRulesTransaction {

		/// <summary>
		///     The highest major client version allowed. We use this to define the rules clearly, and avoid spoofs
		/// </summary>
		public SoftwareVersion MaximumVersionAllowed { get; set; } = new SoftwareVersion();

		/// <summary>
		///     The minimum allowed client version. Clients below this version are considered obsolete and rejected from the
		///     network.
		/// </summary>
		public SoftwareVersion MinimumWarningVersionAllowed { get; set; } = new SoftwareVersion();

		/// <summary>
		///     The minimum allowed client version. Clients below this version are considered obsolete and rejected from the
		///     network.
		/// </summary>
		public SoftwareVersion MinimumVersionAllowed { get; set; } = new SoftwareVersion();

		/// <summary>
		///     The maximum time distance in seconds between each block. if this limit is passed, we could say something is wrong.
		/// </summary>
		public int MaxBlockInterval { get; set; } = 60 * 3;

		/// <summary>
		/// are presentation transactions allowed on gossip protocol?
		/// </summary>
		public bool AllowGossipPresentations { get; set; } = false;

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.MaximumVersionAllowed);
			nodeList.Add(this.MinimumWarningVersionAllowed);
			nodeList.Add(this.MinimumVersionAllowed);
			nodeList.Add(this.MaxBlockInterval);
			nodeList.Add(this.AllowGossipPresentations);
			
			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("MaximumVersionAllowed", this.MaximumVersionAllowed);

			//
			jsonDeserializer.SetProperty("MinimumWarningVersionAllowed", this.MinimumWarningVersionAllowed);

			//
			jsonDeserializer.SetProperty("MinimumVersionAllowed", this.MinimumVersionAllowed);

			//
			jsonDeserializer.SetProperty("MaxBlockInterval", this.MaxBlockInterval);
			
			jsonDeserializer.SetProperty("AllowGossipPresentations", this.AllowGossipPresentations);
		}

		protected override void RehydrateContents(ChannelsEntries<IDataRehydrator> dataChannels, ITransactionRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(dataChannels, rehydrationFactory);

			this.MaximumVersionAllowed.Rehydrate(dataChannels.ContentsData);
			this.MinimumWarningVersionAllowed.Rehydrate(dataChannels.ContentsData);
			this.MinimumVersionAllowed.Rehydrate(dataChannels.ContentsData);

			this.MaxBlockInterval = dataChannels.ContentsData.ReadInt();
			this.AllowGossipPresentations = dataChannels.ContentsData.ReadBool();
		}

		protected override void DehydrateContents(ChannelsEntries<IDataDehydrator> dataChannels) {
			base.DehydrateContents(dataChannels);

			this.MaximumVersionAllowed.Dehydrate(dataChannels.ContentsData);
			this.MinimumWarningVersionAllowed.Dehydrate(dataChannels.ContentsData);
			this.MinimumVersionAllowed.Dehydrate(dataChannels.ContentsData);

			dataChannels.ContentsData.Write(this.MaxBlockInterval);
			dataChannels.ContentsData.Write(this.AllowGossipPresentations);
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (OPERATING_RULES: TransactionTypes.Instance.MODERATION_OPERATING_RULES, 1, 0);
		}
		
		public override ImmutableList<AccountId> TargetAccounts => new List<AccountId>().ToImmutableList();
	}
}