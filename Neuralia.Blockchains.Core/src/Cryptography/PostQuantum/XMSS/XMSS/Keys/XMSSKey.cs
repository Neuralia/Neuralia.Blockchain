using System;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys {
	public abstract class XMSSKey : IDisposable2 {

		public virtual void LoadKey(IByteArray keyBytes) {
			if(keyBytes == null) {
				throw new ApplicationException("Key not set");
			}

			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(keyBytes);

			this.Rehydrate(rehydrator);
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

		}

		public virtual IByteArray SaveKey() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
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

			try {
				if(disposing && !this.IsDisposed) {
					this.DisposeAll();
				}
			} finally {
				this.IsDisposed = true;
			}
		}

		protected virtual void DisposeAll() {

		}

		~XMSSKey() {
			this.Dispose(false);
		}

	#endregion

	}
}