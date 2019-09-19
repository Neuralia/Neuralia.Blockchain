using Neuralia.Blockchains.Core.Cryptography.Trees;
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

			this.Offset = rehydrator.ReadLong();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Offset);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			hashNodeList.Add(this.Offset);

			return hashNodeList;
		}
	}
}