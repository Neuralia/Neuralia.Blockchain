using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods {
	public interface ICandidatureMethod : IVersionable<CandidatureMethodType> {
		SafeArrayHandle DetermineCandidacy(BlockElectionDistillate matureBlockElectionDistillate, BlockElectionDistillate currentBlockElectionDistillate, AccountId miningAccount);
	}

	/// <summary>
	///     How do we determine the candidature of a candidate to the election based on their political properties
	/// </summary>
	public abstract class CandidatureMethod : Versionable<CandidatureMethodType>, ICandidatureMethod {

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
		}

		public abstract SafeArrayHandle DetermineCandidacy(BlockElectionDistillate matureBlockElectionDistillate, BlockElectionDistillate currentBlockElectionDistillate, AccountId miningAccount);
		public abstract SafeArrayHandle DetermineCandidacy(BlockElectionDistillate matureBlockElectionDistillate, SafeArrayHandle currentBlockHash, AccountId miningAccount);
	}
}