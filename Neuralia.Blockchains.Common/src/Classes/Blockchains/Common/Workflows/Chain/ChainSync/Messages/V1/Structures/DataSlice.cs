using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures {
	public class DataSlice : DataSliceInfo, IDisposableExtended {

		public DataSlice() {

		}

		public DataSlice(long length, long offset, SafeArrayHandle data) : base(length, offset) {
			this.Data.Entry = data.Entry;
		}

		public SafeArrayHandle Data { get; } = SafeArrayHandle.Create();

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.Data.Entry = rehydrator.ReadNonNullableArray();
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

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.Data?.Dispose();
			}

			this.IsDisposed = true;
		}

		~DataSlice() {
			this.Dispose(false);
		}

	#endregion

	}
}