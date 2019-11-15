using System;
using System.IO.Abstractions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Transactions {

	public interface IWalletSerializationTransaction : IDisposableExtended {
		long SessionId { get; }

		void ValidateSessionContext(long sessionId);
		void ValidateSessionContext(IWalletSerializationTransaction walletSerializationTransaction);
	}

	public interface IWalletSerializationTransactionExtension : IWalletSerializationTransaction {
		void CommitTransaction();

		void RollbackTransaction();

		// called when the element is disposed.
		event Action<long> Disposed;
	}

	public class WalletSerializationTransaction : IWalletSerializationTransactionExtension {

		private readonly WalletSerializationTransactionalLayer walletSerializationTransactionalLayer;
		private bool completed;

		public WalletSerializationTransaction(WalletSerializationTransactionalLayer walletSerializationTransactionalLayer) {
			this.walletSerializationTransactionalLayer = walletSerializationTransactionalLayer;
			this.SessionId = GlobalRandom.GetNextLong();
		}

		public IFileSystem FileSystem => this.walletSerializationTransactionalLayer.FileSystem;
		public long SessionId { get; private set; }

		public void CommitTransaction() {
			if(!this.completed) {
				this.completed = true;
				this.walletSerializationTransactionalLayer?.CommitTransaction();
				this.SessionId = 0;
			}
		}

		public void RollbackTransaction() {
			if(!this.completed) {
				this.completed = true;
				this.walletSerializationTransactionalLayer?.RollbackTransaction();
				this.Disposed?.Invoke(this.SessionId);
				this.SessionId = 0;
			}
		}

		public event Action<long> Disposed;

		public void ValidateSessionContext(IWalletSerializationTransaction walletSerializationTransaction) {
			this.ValidateSessionContext(walletSerializationTransaction?.SessionId ?? 0);
		}

		public void ValidateSessionContext(long sessionId) {
			if(sessionId != this.SessionId) {
				throw new BadTransactionContextException();
			}
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.RollbackTransaction();
			}

			this.IsDisposed = true;
		}

		~WalletSerializationTransaction() {
			this.Dispose(false);
		}

	#endregion

	}
}