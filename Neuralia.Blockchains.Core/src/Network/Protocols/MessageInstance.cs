using System;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public class MessageInstance : IDisposable2 {
		public long Hash { get; set; }
		public bool IsCached { get; set; }
		public int Size { get; set; }
		public IByteArray MessageBytes { get; set; }
		public ISplitMessageEntry SplitMessage { get; set; }

		public bool IsSpliMessage => this.SplitMessage != null;

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {
				try {
					this.MessageBytes?.Dispose();
					this.SplitMessage?.Dispose();
				} finally {
					this.IsDisposed = true;
				}
			}
		}

		~MessageInstance() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}