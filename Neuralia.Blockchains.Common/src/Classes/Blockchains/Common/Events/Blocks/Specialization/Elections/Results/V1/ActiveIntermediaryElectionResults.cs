using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1 {
	public interface IActiveIntermediaryElectionResults : IIntermediaryElectionResults {
	}

	public abstract class ActiveIntermediaryElectionResults : IntermediaryElectionResults, IActiveIntermediaryElectionResults {
		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
		}

		public override HashNodeList GetStructuresArray() {
			return base.GetStructuresArray();
		}

		protected override ComponentVersion<ElectionContextType> SetIdentity() {
			return (ElectionContextTypes.Instance.Active, 1, 0);
		}
	}
}