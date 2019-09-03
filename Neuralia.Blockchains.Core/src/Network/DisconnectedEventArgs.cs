using System;
using Neuralia.Blockchains.Tools.Data.Allocation;

namespace Neuralia.Blockchains.Core.Network {

	public class DisconnectedEventArgs : EventArgs {

		private static readonly MemoryBlockPool<DisconnectedEventArgs> objectPool = new MemoryBlockPool<DisconnectedEventArgs>(() => new DisconnectedEventArgs());

		private DisconnectedEventArgs() {
		}

		public Exception Exception { get; private set; }

		public void Recycle() {
			objectPool.PutObject(this);
		}

		internal static DisconnectedEventArgs GetObject() {
			return objectPool.GetObject();
		}

		internal void Set(Exception e) {
			this.Exception = e;
		}
	}
}