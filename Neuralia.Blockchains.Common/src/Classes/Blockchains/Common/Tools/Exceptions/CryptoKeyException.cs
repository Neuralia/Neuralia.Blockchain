using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions {
	public class BlockchainEventException : ApplicationException {
		public BlockchainEventException() {

		}

		public BlockchainEventException(Exception ex) : base("", ex) {

		}
	}

	public class WalletEventException : BlockchainEventException {
		public WalletEventException() {

		}

		public WalletEventException(Exception ex) : base(ex) {

		}
	}

	public class KeyEventException : BlockchainEventException {

		public KeyEventException(Guid accountUuid, string keyName, int attempt, Exception ex = null) : base(ex) {
			this.AccountUuid = accountUuid;
			this.KeyName = keyName;
			this.Attempt = attempt;
		}

		public Guid AccountUuid { get; }
		public string KeyName { get; }
		public int Attempt { get; }
	}

	public class KeyFileMissingException : KeyEventException {

		public KeyFileMissingException(Guid accountUuid, string keyName, int attempt) : base(accountUuid, keyName, attempt) {
		}
	}

	public class KeyPassphraseMissingException : KeyEventException {

		public KeyPassphraseMissingException(Guid accountUuid, string keyName, int attempt) : base(accountUuid, keyName, attempt) {
		}
	}

	public class KeyDecryptionException : KeyEventException {

		public KeyDecryptionException(Guid accountUuid, string keyName, Exception ex) : base(accountUuid, keyName, 1, ex) {
		}
	}

	public class WalletFileMissingException : WalletEventException {
	}

	public class WalletPassphraseMissingException : WalletEventException {
	}

	public class WalletDecryptionException : WalletEventException {
		public WalletDecryptionException(Exception ex) : base(ex) {
		}
	}
}