using System;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {
	public abstract class XMSSMTKey : IDisposableExtended {

		public virtual void LoadKey(ByteArray publicKey) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(publicKey);

			this.Rehydrate(rehydrator);
		}

		protected virtual void Rehydrate(IDataRehydrator rehydrator) {

		}

		public virtual ByteArray SaveKey() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			//TODO: this should be a realease, not clone
			return dehydrator.ToArray().Entry.Clone();
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

	
			if(disposing && !this.IsDisposed) {
				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		protected virtual void DisposeAll() {

		}

		~XMSSMTKey() {
			this.Dispose(false);
		}

	#endregion

	}
}