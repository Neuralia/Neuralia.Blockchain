using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures {

	public class DataSliceSize : IBinarySerializable, ITreeHashable {

		public DataSliceSize() {

		}

		public DataSliceSize(long length) {
			this.Length = length;
		}

		public long Length { get; set; }

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9();
			adaptiveSet.Rehydrate(rehydrator);
			this.Length = adaptiveSet.Value;
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9(this.Length);
			adaptiveSet.Dehydrate(dehydrator);
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Length);

			return hashNodeList;
		}
	}
}