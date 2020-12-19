using System;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public class MessageInstance : IDisposableExtended {
		public long Hash { get; set; }
		public bool IsCached { get; set; }
		public int Size { get; set; }
		public SafeArrayHandle MessageBytes { get; } = SafeArrayHandle.Create();
		public ISplitMessageEntry SplitMessage { get; set; }

		public bool IsSplitMessage => this.SplitMessage != null;
		
	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				this.MessageBytes?.Dispose();
				this.SplitMessage?.Dispose();
			}

			this.IsDisposed = true;
		}

		~MessageInstance() {
			this.Dispose(false);
		}

	#endregion
	}
}