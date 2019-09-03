using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections {

	public interface IActiveElectionCandidacyMessage : IPassiveElectionCandidacyMessage {
		List<IActiveRepresentativeBallotingApplication> RepresentativeBallotingApplications { get; }
	}

	/// <summary>
	///     In an active election,w e must also send whatever is required to identify our choices in what will become the
	///     result.
	/// </summary>
	public class ActiveElectionCandidacyMessage : PassiveElectionCandidacyMessage, IActiveElectionCandidacyMessage {

		// our application to become a representative, if applicable
		public List<IActiveRepresentativeBallotingApplication> RepresentativeBallotingApplications { get; } = new List<IActiveRepresentativeBallotingApplication>();

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			ushort count = rehydrator.ReadUShort();

			this.RepresentativeBallotingApplications.Clear();

			for(int i = 0; i < count; i++) {
				this.RepresentativeBallotingApplications.Add(RepresentativeBallotingMethodRehydrator.RehydrateActiveApplication(rehydrator));
			}
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			dehydrator.Write((ushort) this.RepresentativeBallotingApplications.Count);

			foreach(IActiveRepresentativeBallotingApplication entry in this.RepresentativeBallotingApplications) {

				dehydrator.Write(entry == null);

				if(entry != null) {
					entry.Dehydrate(dehydrator);
				}
			}
		}

		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.ACTIVE_ELECTION_CANDIDACY, 1, 0);
		}
	}
}