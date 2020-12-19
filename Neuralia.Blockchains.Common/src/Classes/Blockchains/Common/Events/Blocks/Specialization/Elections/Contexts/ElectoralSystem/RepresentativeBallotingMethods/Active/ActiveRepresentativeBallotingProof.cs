using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active {
	public interface IActiveRepresentativeBallotingProof : IVersionable<ActiveRepresentativeBallotingMethodType>, ITreeHashable, IJsonSerializable {
	}

	/// <summary>
	///     The proof for any conditions we had hanging in the selection of represntatives
	/// </summary>
	public abstract class ActiveRepresentativeBallotingProof : Versionable<ActiveRepresentativeBallotingMethodType>, IActiveRepresentativeBallotingProof {

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}
	}
}