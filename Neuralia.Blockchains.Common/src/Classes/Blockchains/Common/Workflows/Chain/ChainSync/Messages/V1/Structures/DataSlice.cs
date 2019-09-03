using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures {
	public class DataSlice : DataSliceInfo {
		public IByteArray Data { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.Data = rehydrator.ReadNonNullableArray();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.Data);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			hashNodeList.Add(this.Data);

			return hashNodeList;
		}
	}
}