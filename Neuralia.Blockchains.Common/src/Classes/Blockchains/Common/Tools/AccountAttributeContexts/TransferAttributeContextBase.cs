using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.AccountAttributeContexts {
	public abstract class TransferAttributeContextBase {

		public void Rehydrate(byte[] context) {
			this.Rehydrate(SafeArrayHandle.Wrap(context));
		}

		public void Rehydrate(SafeArrayHandle context) {
			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(context);

			this.Rehydrate(rehydrator);

		}

		public byte[] DehydrateContext() {

			SafeArrayHandle handle = this.Dehydrate();

			return handle.ToExactByteArrayCopy();
		}

		public SafeArrayHandle Dehydrate() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();

		}

		protected virtual void Rehydrate(IDataRehydrator rehydrator) {

		}

		protected virtual void Dehydrate(IDataDehydrator dehydrator) {

		}
	}
}