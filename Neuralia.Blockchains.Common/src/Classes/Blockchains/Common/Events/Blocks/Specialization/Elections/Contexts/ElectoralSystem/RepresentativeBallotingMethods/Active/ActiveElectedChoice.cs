using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active {
	public interface IActiveElectedChoice : IPassiveElectedChoice {
		List<IActiveRepresentativeBallotingApplication> ActiveRepresentativeBallotingApplications { get; }
	}

	public class ActiveElectedChoice : PassiveElectedChoice, IActiveElectedChoice {

		public List<IActiveRepresentativeBallotingApplication> ActiveRepresentativeBallotingApplications { get; } = new List<IActiveRepresentativeBallotingApplication>();
	}
}