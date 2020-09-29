using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Extra {

	public interface IAccountFileInfo : IDisposableExtended {

		AccountPassphraseDetails AccountSecurityDetails { get; }
		WalletChainStateFileInfo WalletChainStatesInfo { get; set; }
		WalletElectionCacheFileInfo WalletElectionCacheInfo { get; set; }
		WalletKeyLogFileInfo WalletKeyLogsInfo { get; set; }
		Dictionary<string, WalletKeyFileInfo> WalletKeysFileInfo { get; }
		IWalletAccountSnapshotFileInfo WalletSnapshotInfo { get; set; }
		IWalletGenerationCacheFileInfo WalletGenerationCacheInfo { get; set; }
		IWalletTransactionHistoryFileInfo WalletTransactionHistoryInfo { get; set; }
		IWalletElectionsHistoryFileInfo WalletElectionsHistoryInfo { get; set; }
		IWalletElectionsStatisticsFileInfo WalletElectionsStatisticsInfo { get; set; }
		WalletKeyHistoryFileInfo WalletKeyHistoryInfo { get; set; }
		Task Load(LockContext lockContext);
		Task Save(LockContext lockContext);
		Task ChangeEncryption(LockContext lockContext);
		Task Reset(LockContext lockContext);
		Task ReloadFileBytes(LockContext lockContext);
		void ClearCached(LockContext lockContext);
	}

	public abstract class AccountFileInfo : IAccountFileInfo {

		public AccountFileInfo(AccountPassphraseDetails accountSecurityDetails) {
			this.AccountSecurityDetails = accountSecurityDetails;
		}

		public AccountPassphraseDetails AccountSecurityDetails { get; }
		public WalletChainStateFileInfo WalletChainStatesInfo { get; set; }
		public WalletElectionCacheFileInfo WalletElectionCacheInfo { get; set; }
		public WalletKeyLogFileInfo WalletKeyLogsInfo { get; set; }
		public Dictionary<string, WalletKeyFileInfo> WalletKeysFileInfo { get; } = new Dictionary<string, WalletKeyFileInfo>();
		public IWalletAccountSnapshotFileInfo WalletSnapshotInfo { get; set; }
		public IWalletGenerationCacheFileInfo WalletGenerationCacheInfo { get; set; }
		public IWalletTransactionHistoryFileInfo WalletTransactionHistoryInfo { get; set; }
		public IWalletElectionsHistoryFileInfo WalletElectionsHistoryInfo { get; set; }
		public IWalletElectionsStatisticsFileInfo WalletElectionsStatisticsInfo { get; set; }

		public WalletKeyHistoryFileInfo WalletKeyHistoryInfo { get; set; }

		protected async Task RunAllAsync(Func<IWalletFileInfo, LockContext, Task> action, LockContext lockContext, bool keys = true) {
			if(keys) {
				foreach(var e in this.WalletKeysFileInfo) {
					if(e.Value != null) {
						await action(e.Value, lockContext).ConfigureAwait(false);
					}
				}
			}

			if(WalletKeyLogsInfo != null) {
				await action(this.WalletKeyLogsInfo, lockContext).ConfigureAwait(false);
			}
			if(WalletChainStatesInfo != null) {
				await action(this.WalletChainStatesInfo, lockContext).ConfigureAwait(false);
			}
			if(this.WalletGenerationCacheInfo != null) {
				await action(this.WalletGenerationCacheInfo, lockContext).ConfigureAwait(false);
			}
			if(WalletTransactionHistoryInfo != null) {
				await action(this.WalletTransactionHistoryInfo, lockContext).ConfigureAwait(false);
			}
			if(WalletElectionsHistoryInfo != null) {
				await action(this.WalletElectionsHistoryInfo, lockContext).ConfigureAwait(false);
			}
			if(WalletElectionsStatisticsInfo != null) {
				await action(this.WalletElectionsStatisticsInfo, lockContext).ConfigureAwait(false);
			}
			if(WalletElectionCacheInfo != null) {
				await action(this.WalletElectionCacheInfo, lockContext).ConfigureAwait(false);
			}
			if(WalletSnapshotInfo != null) {
				await action(this.WalletSnapshotInfo, lockContext).ConfigureAwait(false);
			}
			if(WalletKeyHistoryInfo != null) {
				await action(this.WalletKeyHistoryInfo, lockContext).ConfigureAwait(false);
			}
		}

		protected void RunAll(Action<IWalletFileInfo, LockContext> action, LockContext lockContext, bool keys = true) {
			this.RunAllAsync((e, lc) => {
				action(e, lc);
				
				return Task.CompletedTask;
			}, lockContext, keys).WaitAndUnwrapException();
		}

		public virtual async Task Load(LockContext lockContext) {

			await this.RunAllAsync((e, lc) => e.Load(lc), lockContext, false).ConfigureAwait(false);
		}

		public virtual async Task Save(LockContext lockContext) {

			await this.RunAllAsync((e, lc) => {
				if(e.IsLoaded) {
					return e.Save(lc);
				}

				return Task.CompletedTask;
			}, lockContext).ConfigureAwait(false);
		}

		public virtual async Task ChangeEncryption(LockContext lockContext) {

			await this.RunAllAsync((e, lc) => e.ChangeEncryption(lc), lockContext, false).ConfigureAwait(false);
		}

		public virtual async Task Reset(LockContext lockContext) {
			
			await this.RunAllAsync((e, lc) => e.Reset(lc), lockContext).ConfigureAwait(false);
		}

		public virtual async Task ReloadFileBytes(LockContext lockContext) {

			await this.RunAllAsync((e, lc) => e.ReloadFileBytes(lc), lockContext).ConfigureAwait(false);
			
		}

		public void ClearCached(LockContext lockContext) {
			this.RunAll((e, lc) => e.ClearCached(lc), lockContext);
		}
		
	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		~AccountFileInfo() {
			this.Dispose(false);
		}

		protected virtual void DisposeAll() {

			this.RunAll((e, lc) => e.Dispose(), null);
		}

	#endregion
	}
}