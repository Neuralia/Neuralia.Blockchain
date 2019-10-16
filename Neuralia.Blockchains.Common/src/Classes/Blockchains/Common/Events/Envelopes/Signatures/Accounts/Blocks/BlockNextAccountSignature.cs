using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public interface IBlockNextAccountSignature : ISerializableCombo {

	}

	public abstract class BlockNextAccountSignature : IBlockNextAccountSignature {
		
		public virtual void Rehydrate(IDataRehydrator rehydrator) {

		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			
			return nodeList;
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {
		}
	}
}