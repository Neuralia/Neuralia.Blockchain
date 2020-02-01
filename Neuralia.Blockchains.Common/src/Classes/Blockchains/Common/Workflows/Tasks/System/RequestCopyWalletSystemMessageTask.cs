using System;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System {
	public class RequestCopyWalletSystemMessageTask : SystemMessageTask {
		private readonly Action action;

		public RequestCopyWalletSystemMessageTask(Action action) : base(BlockchainSystemEventTypes.Instance.WalletLoadingStarted) {
			this.action = action;
		}

		public void Completed() {
			this.action();
		}
	}
}