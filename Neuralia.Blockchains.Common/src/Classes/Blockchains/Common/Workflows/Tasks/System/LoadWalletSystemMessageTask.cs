using System;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System {
	public class LoadWalletSystemMessageTask : SystemMessageTask {
		private readonly Action action;

		public LoadWalletSystemMessageTask(Action action) : base(BlockchainSystemEventTypes.Instance.WalletLoadingStarted) {
			this.action = action;
		}

		public void Completed() {
			this.action();
		}
	}
}