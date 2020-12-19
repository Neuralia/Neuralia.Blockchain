using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active.V1.EncryptedSecret;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive.V1.TopLowestHashes;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods {
	public static class RepresentativeBallotingSelectorFactory {

		public static IActiveRepresentativeBallotingSelector GetActiveRepresentativeSelector(IActiveRepresentativeBallotingRules representativeBallotingRules) {

			if(representativeBallotingRules.Version.Type == ActiveRepresentativeBallotingMethodTypes.Instance.EncryptedSecret) {
				if(representativeBallotingRules.Version == (1, 0)) {
					return new EncryptedSecretRepresentativeBallotingSelector((EncryptedSecretRepresentativeBallotingRules) representativeBallotingRules);
				}
			}

			throw new ApplicationException("Invalid representatives context type");
		}

		public static IPassiveRepresentativeBallotingSelector GetPassiveRepresentativeSelector(IPassiveRepresentativeBallotingRules representativeBallotingRules) {

			if(representativeBallotingRules.Version.Type == PassiveRepresentativeBallotingMethodTypes.Instance.TopLowestHashes) {
				if(representativeBallotingRules.Version == (1, 0)) {
					return new TopLowestHashesRepresentativeBallotingSelector((TopLowestHashesRepresentativeBallotingRules) representativeBallotingRules);
				}
			}

			throw new ApplicationException("Invalid representatives context type");
		}
	}
}