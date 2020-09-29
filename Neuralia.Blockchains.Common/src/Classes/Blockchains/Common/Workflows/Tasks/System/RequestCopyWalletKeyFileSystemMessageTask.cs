using System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System {

	public class RequestCopyWalletKeyFileSystemMessageTask : SystemMessageTask {
		private readonly Action action;
		public int attempt;
		public int correlationCode;

		public RequestCopyWalletKeyFileSystemMessageTask(string accountCode, string keyName, int attempt, Action action) : base(BlockchainSystemEventTypes.Instance.RequestCopyKeyFile) {
			this.action = action;

			this.accountCode = accountCode;
			this.keyName = keyName;
			this.correlationCode = GlobalRandom.GetNext();
			this.attempt = attempt;

			this.parameters = new object[] {this.correlationCode, this.accountCode, this.keyName, this.attempt};
		}

		public string accountCode { get; set; }
		public string keyName { get; set; }

		public void Completed() {
			this.action();
		}
	}
}