using System;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys {
	public abstract class XMSSKey : IDisposableExtended {

		public virtual void LoadKey(ByteArray keyBytes) {
			if(keyBytes == null) {
				throw new ApplicationException("Key not set");
			}

			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(keyBytes);

			this.Rehydrate(rehydrator);
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

		}

		public virtual ByteArray SaveKey() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			//TODO: this should be a realease, not clone
			return dehydrator.ToArray().Entry.Clone();
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			
			if(disposing && !this.IsDisposed) {
				this.DisposeAll();
			}
			this.IsDisposed = true;
		}

		protected virtual void DisposeAll() {

		}

		~XMSSKey() {
			this.Dispose(false);
		}

	#endregion

	}
}