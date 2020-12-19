using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures {
	public class DataSliceInfo : DataSliceSize {

		public DataSliceInfo() {

		}

		public DataSliceInfo(long length, long offset) : base(length) {
			this.Offset = offset;
		}

		public long Offset { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9();
			adaptiveSet.Rehydrate(rehydrator);
			this.Offset = adaptiveSet.Value;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveLong1_9 adaptiveSet = new AdaptiveLong1_9(this.Offset);
			adaptiveSet.Dehydrate(dehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			hashNodeList.Add(this.Offset);

			return hashNodeList;
		}
	}
}