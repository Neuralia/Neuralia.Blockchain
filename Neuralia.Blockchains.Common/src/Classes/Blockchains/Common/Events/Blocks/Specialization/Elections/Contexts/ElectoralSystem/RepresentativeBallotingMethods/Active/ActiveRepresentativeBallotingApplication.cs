using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active {
	public interface IActiveRepresentativeBallotingApplication : IVersionable<ActiveRepresentativeBallotingMethodType>, ISerializableCombo {
	}

	/// <summary>
	///     Our application for the final representative election
	/// </summary>
	public abstract class ActiveRepresentativeBallotingApplication : Versionable<ActiveRepresentativeBallotingMethodType>, IActiveRepresentativeBallotingApplication {

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);
		}
	}
}