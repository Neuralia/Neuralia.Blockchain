using System;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods {
	public interface ICandidatureMethod : IVersionable<CandidatureMethodType> {
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
		
		
	}
}