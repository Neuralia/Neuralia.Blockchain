using System.Collections.Generic;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active {
	public interface IActiveElectedChoice : IElectedChoice {
		List<IActiveRepresentativeBallotingApplication> ActiveRepresentativeBallotingApplications { get; }
	}

	public class ActiveElectedChoice : ElectedChoice, IActiveElectedChoice {

		public List<IActiveRepresentativeBallotingApplication> ActiveRepresentativeBallotingApplications { get; } = new List<IActiveRepresentativeBallotingApplication>();
	}
}