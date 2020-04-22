using System;
using System.Security;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System {

	public class RequestWalletPassphraseSystemMessageTask : SystemMessageTask {
		private readonly Action action;
		public int attempt;
		public int correlationCode;

		public RequestWalletPassphraseSystemMessageTask(int attempt, Action action) : base(BlockchainSystemEventTypes.Instance.RequestWalletPassphrase) {
			this.action = action;
			this.correlationCode = GlobalRandom.GetNext();
			this.attempt = attempt;

			this.parameters = new object[] {this.correlationCode, this.attempt};
		}

		public SecureString Passphrase { get; set; }
		public bool KeysToo { get; set; }
		
		public void Completed() {
			this.action();
		}
	}
}