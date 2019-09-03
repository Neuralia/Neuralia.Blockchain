using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures {

	public class DataSliceSize : IBinarySerializable, ITreeHashable {

		public long Length { get; set; }

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			this.Length = rehydrator.ReadLong();

		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Length);

		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Length);

			return hashNodeList;
		}
	}
}