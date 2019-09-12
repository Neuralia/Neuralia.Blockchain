using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions {
	public interface IElectionQuestion : IVersionable<ElectionQuestionType> {

	}

	public abstract class ElectionQuestion : Versionable<ElectionQuestionType>, IElectionQuestion {
		

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new ApplicationException();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();
			
			return nodeList;
		}
	}
}