using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active.V1.EncryptedSecret;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive.V1.TopLowestHashes;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods {
	public static class RepresentativeBallotingMethodRehydrator {

		public static IRepresentativeBallotingRules RehydrateActiveRules(IDataRehydrator rehydrator) {

			var version = rehydrator.RehydrateRewind<ComponentVersion<ActiveRepresentativeBallotingMethodType>>();

			IRepresentativeBallotingRules representativeBallotingRules = null;

			bool isActive = rehydrator.ReadBool();

			if(isActive) {
				if(version.Type == ActiveRepresentativeBallotingMethodTypes.Instance.EncryptedSecret) {
					if(version.Major == 1 && version.Minor == 0) {
						representativeBallotingRules = new EncryptedSecretRepresentativeBallotingRules();
					}
				}

				if(representativeBallotingRules == null) {
					throw new ApplicationException("Invalid representative context type");
				}

				representativeBallotingRules.Rehydrate(rehydrator);
			} else {
				representativeBallotingRules = RehydratePassiveRules(rehydrator);
			}

			return representativeBallotingRules;
		}

		public static IRepresentativeBallotingRules RehydratePassiveRules(IDataRehydrator rehydrator) {

			var version = rehydrator.RehydrateRewind<ComponentVersion<PassiveRepresentativeBallotingMethodType>>();

			IPassiveRepresentativeBallotingRules representativeBallotingRules = null;

			if(version.Type == PassiveRepresentativeBallotingMethodTypes.Instance.TopLowestHashes) {
				if(version.Major == 1 && version.Minor == 0) {
					representativeBallotingRules = new TopLowestHashesRepresentativeBallotingRules();
				}
			}

			if(representativeBallotingRules == null) {
				throw new ApplicationException("Invalid representative context type");
			}

			representativeBallotingRules.Rehydrate(rehydrator);

			return representativeBallotingRules;
		}

		public static IActiveRepresentativeBallotingApplication RehydrateActiveApplication(IDataRehydrator rehydrator) {

			var version = rehydrator.RehydrateRewind<ComponentVersion<ActiveRepresentativeBallotingMethodType>>();

			IActiveRepresentativeBallotingApplication representativeBallotingApplication = null;

			if(version.Type == ActiveRepresentativeBallotingMethodTypes.Instance.EncryptedSecret) {
				if(version.Major == 1 && version.Minor == 0) {
					representativeBallotingApplication = new EncryptedSecretRepresentativeBallotingApplication();
				}
			}

			if(representativeBallotingApplication == null) {
				throw new ApplicationException("Invalid representative application type");
			}

			representativeBallotingApplication.Rehydrate(rehydrator);

			return representativeBallotingApplication;
		}

		public static IActiveRepresentativeBallotingProof RehydrateActiveProof(IDataRehydrator rehydrator) {

			bool isNull = rehydrator.ReadBool();

			if(isNull) {
				return null;
			}

			var version = rehydrator.RehydrateRewind<ComponentVersion<ActiveRepresentativeBallotingMethodType>>();

			IActiveRepresentativeBallotingProof representativeBallotingProof = null;

			if(version.Type == ActiveRepresentativeBallotingMethodTypes.Instance.EncryptedSecret) {
				if(version.Major == 1 && version.Minor == 0) {
					representativeBallotingProof = new EncryptedSecretRepresentativeBallotingProof();
				}
			}

			if(representativeBallotingProof == null) {
				throw new ApplicationException("Invalid representative proof type");
			}

			representativeBallotingProof.Rehydrate(rehydrator);

			return representativeBallotingProof;
		}
	}
}