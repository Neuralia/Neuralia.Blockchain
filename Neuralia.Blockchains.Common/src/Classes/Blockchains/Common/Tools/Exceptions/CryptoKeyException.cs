using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions {

	public static class CryptoKeysExceptionUtils {

		public static bool IsCryptoKeyException(Exception ex) {

			return ex is WalletFileMissingException || ex is WalletPassphraseMissingException || ex is WalletDecryptionException || ex is KeyFileMissingException || ex is KeyPassphraseMissingException || ex is KeyDecryptionException;
		}
	}

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

		public KeyEventException(string accountCode, string keyName, int attempt, Exception ex = null) : base(ex) {
			this.AccountCode = accountCode;
			this.KeyName = keyName;
			this.Attempt = attempt;
		}

		public string AccountCode { get; }
		public string KeyName { get; }
		public int Attempt { get; }
	}

	public class KeyFileMissingException : KeyEventException {

		public KeyFileMissingException(string accountCode, string keyName, int attempt) : base(accountCode, keyName, attempt) {
		}
	}

	public class KeyPassphraseMissingException : KeyEventException {

		public KeyPassphraseMissingException(string accountCode, string keyName, int attempt) : base(accountCode, keyName, attempt) {
		}
	}

	public class KeyDecryptionException : KeyEventException {

		public KeyDecryptionException(string accountCode, string keyName, Exception ex) : base(accountCode, keyName, 1, ex) {
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