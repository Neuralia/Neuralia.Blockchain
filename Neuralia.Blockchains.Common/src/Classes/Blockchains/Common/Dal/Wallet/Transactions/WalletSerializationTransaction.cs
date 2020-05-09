using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Transactions {

	public interface IWalletSerializationTransaction : IDisposableExtended {
		long SessionId { get; }

		void ValidateSessionContext(long sessionId);
		void ValidateSessionContext(IWalletSerializationTransaction walletSerializationTransaction);
	}

	public interface IWalletSerializationTransactionExtension : IWalletSerializationTransaction {
		Task CommitTransaction();

		Task RollbackTransaction();

		bool IsInTransaction { get; }
		
		// called when the element is disposed.
		event Func<long, Task> Disposed;
	}

	public class WalletSerializationTransaction : IWalletSerializationTransactionExtension {

		private readonly WalletSerializationTransactionalLayer walletSerializationTransactionalLayer;
		private bool completed;

		public WalletSerializationTransaction(WalletSerializationTransactionalLayer walletSerializationTransactionalLayer) {
			this.walletSerializationTransactionalLayer = walletSerializationTransactionalLayer;
			this.SessionId = GlobalRandom.GetNextLong();
		}

		public FileSystemWrapper FileSystem => this.walletSerializationTransactionalLayer.FileSystem;
		public long SessionId { get; private set; }

		public async Task CommitTransaction() {
			if(!this.completed) {
				this.completed = true; // set here to prevent recursive loops

				try {
					if(walletSerializationTransactionalLayer != null) {

						await (walletSerializationTransactionalLayer.CommitTransaction()).ConfigureAwait(false);
					}

					this.SessionId = 0;
				} catch {
					this.completed = false;

					throw;
				}
			}
		}

		public async Task RollbackTransaction() {
			try {
				await this.RollbackTransactionInternal().ConfigureAwait(false);
			} finally {
				this.Dispose();
			}
		}

		public bool IsInTransaction => this.walletSerializationTransactionalLayer?.IsInTransaction ?? false;

		private async Task RollbackTransactionInternal() {
			if(!this.completed) {
				try {
					this.completed = true;

					if(this.walletSerializationTransactionalLayer != null) {
						await walletSerializationTransactionalLayer.RollbackTransaction().ConfigureAwait(false);
					}

					if(this.Disposed != null) {
						await this.Disposed(this.SessionId).ConfigureAwait(false);
					}

					this.SessionId = 0;
				}catch {
					this.completed = false;

					throw;
				}
			}
		}

		public event Func<long, Task> Disposed;

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
				this.RollbackTransactionInternal().WaitAndUnwrapException();
			}

			this.IsDisposed = true;
		}

		~WalletSerializationTransaction() {
			this.Dispose(false);
		}

	#endregion

	}
}