using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1 {
	public interface IPassiveFinalElectionResults : IFinalElectionResults {
	}

	public abstract class PassiveFinalElectionResults : FinalElectionResults, IPassiveFinalElectionResults {

		public override HashNodeList GetStructuresArray() {
			return base.GetStructuresArray();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
		}

		protected override ComponentVersion<ElectionContextType> SetIdentity() {
			return (ElectionContextTypes.Instance.Passive, 1, 0);
		}
	}
}