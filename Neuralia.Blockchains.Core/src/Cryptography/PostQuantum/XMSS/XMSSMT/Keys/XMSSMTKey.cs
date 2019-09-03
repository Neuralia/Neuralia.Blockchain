using System;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {
	public abstract class XMSSMTKey : IDisposable2 {

		public virtual void LoadKey(IByteArray publicKey) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(publicKey);

			this.Rehydrate(rehydrator);
		}

		protected virtual void Rehydrate(IDataRehydrator rehydrator) {

		}

		public virtual IByteArray SaveKey() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		protected virtual void Dehydrate(IDataDehydrator dehydrator) {

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

		~XMSSMTKey() {
			this.Dispose(false);
		}

	#endregion

	}
}