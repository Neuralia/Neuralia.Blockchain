using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods.V1;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods {
	public static class PrimariesBallotingMethodRehydrator {
		public static IPrimariesBallotingMethod Rehydrate(IDataRehydrator rehydrator) {

			return Rehydrate<IPrimariesBallotingMethod>(rehydrator);
		}

		public static T Rehydrate<T>(IDataRehydrator rehydrator)
			where T : class, IPrimariesBallotingMethod {

			ComponentVersion<PrimariesBallotingMethodType> version = rehydrator.RehydrateRewind<ComponentVersion<PrimariesBallotingMethodType>>();

			T ballotingMethod = null;

			if(version.Type == PrimariesBallotingMethodTypes.Instance.HashTarget) {
				if(version == (1, 0)) {
					ballotingMethod = new HashTargetPrimariesBallotingMethod() as T;
				}
			}

			if(ballotingMethod == null) {
				throw new ApplicationException("Invalid ballot type");
			}

			ballotingMethod.Rehydrate(rehydrator);

			return ballotingMethod;
		}
	}
}