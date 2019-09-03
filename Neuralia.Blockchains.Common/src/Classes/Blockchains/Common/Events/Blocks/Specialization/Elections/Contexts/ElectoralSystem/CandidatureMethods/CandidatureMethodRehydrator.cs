using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods.V1;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods {
	public static class CandidatureMethodRehydrator {
		public static ICandidatureMethod Rehydrate(IDataRehydrator rehydrator) {

			var version = rehydrator.RehydrateRewind<ComponentVersion<CandidatureMethodType>>();

			ICandidatureMethod candidatureMethod = null;

			if(version.Type == CandidatureMethodTypes.Instance.SimpleHash) {
				if((version.Major == 1) && (version.Minor == 0)) {
					candidatureMethod = new SimpleHashCandidatureMethod();
				}
			}

			if(candidatureMethod == null) {
				throw new ApplicationException("Invalid primaries method type");
			}

			candidatureMethod.Rehydrate(rehydrator);

			return candidatureMethod;
		}
	}
}