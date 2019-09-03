using System;
using System.Security;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System {

	public class RequestWalletKeyPassphraseSystemMessageTask : SystemMessageTask {
		private readonly Action action;
		public int attempt;
		public int correlationCode;

		public RequestWalletKeyPassphraseSystemMessageTask(Guid accountUUid, string keyName, int attempt, Action action) : base(BlockchainSystemEventTypes.Instance.RequestKeyPassphrase) {
			this.action = action;

			this.accountUUid = accountUUid;
			this.keyName = keyName;
			this.correlationCode = GlobalRandom.GetNext();
			this.attempt = attempt;

			this.parameters = new object[] {this.correlationCode, this.accountUUid, this.keyName, this.attempt};
		}

		public Guid accountUUid { get; set; }
		public string keyName { get; set; }

		public SecureString Passphrase { get; set; }

		public void Completed() {
			this.action();
		}
	}
}