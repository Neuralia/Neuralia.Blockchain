using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1 {

	public interface IDelegateResults : ISerializableCombo {
	}

	public class DelegateResults : IDelegateResults {

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {

		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			return nodeList;
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
		}
	}
}