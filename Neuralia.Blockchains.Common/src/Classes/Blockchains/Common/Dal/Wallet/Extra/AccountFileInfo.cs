using System.Collections.Generic;
using System.Threading.Tasks;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Extra {

	public interface IAccountFileInfo {

		AccountPassphraseDetails              AccountSecurityDetails       { get; }
		WalletChainStateFileInfo              WalletChainStatesInfo        { get; set; }
		WalletElectionCacheFileInfo           WalletElectionCacheInfo      { get; set; }
		WalletKeyLogFileInfo                  WalletKeyLogsInfo            { get; set; }
		Dictionary<string, WalletKeyFileInfo> WalletKeysFileInfo           { get; }
		IWalletAccountSnapshotFileInfo        WalletSnapshotInfo           { get; set; }
		IWalletTransactionCacheFileInfo       WalletTransactionCacheInfo   { get; set; }
		IWalletTransactionHistoryFileInfo     WalletTransactionHistoryInfo { get; set; }
		IWalletElectionsHistoryFileInfo       WalletElectionsHistoryInfo   { get; set; }
		WalletKeyHistoryFileInfo              WalletKeyHistoryInfo         { get; set; }
		Task                                  Load(LockContext lockContext);
		Task                                  Save(LockContext lockContext);
		Task                                  ChangeEncryption(LockContext lockContext);
		Task                                  Reset(LockContext lockContext);
		Task                                  ReloadFileBytes(LockContext lockContext);
	}

	public abstract class AccountFileInfo : IAccountFileInfo {

		public AccountFileInfo(AccountPassphraseDetails accountSecurityDetails) {
			this.AccountSecurityDetails = accountSecurityDetails;
		}

		public AccountPassphraseDetails              AccountSecurityDetails       { get; }
		public WalletChainStateFileInfo              WalletChainStatesInfo        { get; set; }
		public WalletElectionCacheFileInfo           WalletElectionCacheInfo      { get; set; }
		public WalletKeyLogFileInfo                  WalletKeyLogsInfo            { get; set; }
		public Dictionary<string, WalletKeyFileInfo> WalletKeysFileInfo           { get; } = new Dictionary<string, WalletKeyFileInfo>();
		public IWalletAccountSnapshotFileInfo        WalletSnapshotInfo           { get; set; }
		public IWalletTransactionCacheFileInfo       WalletTransactionCacheInfo   { get; set; }
		public IWalletTransactionHistoryFileInfo     WalletTransactionHistoryInfo { get; set; }
		public IWalletElectionsHistoryFileInfo       WalletElectionsHistoryInfo   { get; set; }
		public WalletKeyHistoryFileInfo              WalletKeyHistoryInfo         { get; set; }

		public virtual async Task Load(LockContext lockContext) {
			await this.WalletKeyLogsInfo.Load(lockContext).ConfigureAwait(false);
			;
			await this.WalletChainStatesInfo.Load(lockContext).ConfigureAwait(false);
			;
			await this.WalletTransactionCacheInfo.Load(lockContext).ConfigureAwait(false);
			;
			await this.WalletTransactionHistoryInfo.Load(lockContext).ConfigureAwait(false);
			;
			await this.WalletElectionsHistoryInfo.Load(lockContext).ConfigureAwait(false);
			;
			this.WalletElectionCacheInfo?.Load(lockContext);
			;
			this.WalletSnapshotInfo?.Load(lockContext);
			;
			this.WalletKeyHistoryInfo?.Load(lockContext);
			;
		}

		public virtual async Task Save(LockContext lockContext) {

			this.WalletKeysFileInfo.ForEach(async e => {
				if(e.Value.IsLoaded) {
					await e.Value.Save(lockContext).ConfigureAwait(false);
				}
			});

			await this.WalletKeyLogsInfo.Save(lockContext).ConfigureAwait(false);
			;
			await this.WalletChainStatesInfo.Save(lockContext).ConfigureAwait(false);
			;
			await this.WalletTransactionCacheInfo.Save(lockContext).ConfigureAwait(false);
			;
			await this.WalletTransactionHistoryInfo.Save(lockContext).ConfigureAwait(false);
			;
			await this.WalletElectionsHistoryInfo.Save(lockContext).ConfigureAwait(false);
			;
			this.WalletElectionCacheInfo?.Save(lockContext);
			this.WalletSnapshotInfo?.Save(lockContext);
			this.WalletKeyHistoryInfo?.Save(lockContext);
		}

		public virtual async Task ChangeEncryption(LockContext lockContext) {

			await this.WalletKeyLogsInfo.ChangeEncryption(lockContext).ConfigureAwait(false);
			;
			await this.WalletChainStatesInfo.ChangeEncryption(lockContext).ConfigureAwait(false);
			;
			await this.WalletTransactionCacheInfo.ChangeEncryption(lockContext).ConfigureAwait(false);
			;
			await this.WalletTransactionHistoryInfo.ChangeEncryption(lockContext).ConfigureAwait(false);
			;
			await this.WalletElectionsHistoryInfo.ChangeEncryption(lockContext).ConfigureAwait(false);
			;
			this.WalletElectionCacheInfo?.ChangeEncryption(lockContext);
			this.WalletSnapshotInfo?.ChangeEncryption(lockContext);
			this.WalletKeyHistoryInfo?.ChangeEncryption(lockContext);
		}

		public virtual async Task Reset(LockContext lockContext) {
			this.WalletKeysFileInfo.ForEach(async e => await e.Value.Reset(lockContext).ConfigureAwait(false));
			await this.WalletKeyLogsInfo.Reset(lockContext).ConfigureAwait(false);
			await this.WalletChainStatesInfo.Reset(lockContext).ConfigureAwait(false);
			await this.WalletTransactionCacheInfo.Reset(lockContext).ConfigureAwait(false);
			await this.WalletTransactionHistoryInfo.Reset(lockContext).ConfigureAwait(false);
			await this.WalletElectionsHistoryInfo.Reset(lockContext).ConfigureAwait(false);
			this.WalletElectionCacheInfo?.Reset(lockContext);
			this.WalletSnapshotInfo?.Reset(lockContext);
			this.WalletKeyHistoryInfo?.Reset(lockContext);
		}

		public virtual async Task ReloadFileBytes(LockContext lockContext) {
			this.WalletKeysFileInfo.ForEach(e => e.Value.ReloadFileBytes(lockContext));
			await WalletKeyLogsInfo.ReloadFileBytes(lockContext).ConfigureAwait(false);
			await this.WalletChainStatesInfo.ReloadFileBytes(lockContext).ConfigureAwait(false);
			await this.WalletTransactionCacheInfo.ReloadFileBytes(lockContext).ConfigureAwait(false);
			await this.WalletTransactionHistoryInfo.ReloadFileBytes(lockContext).ConfigureAwait(false);
			await this.WalletElectionsHistoryInfo.ReloadFileBytes(lockContext).ConfigureAwait(false);
			this.WalletElectionCacheInfo?.ReloadFileBytes(lockContext);
			this.WalletSnapshotInfo?.ReloadFileBytes(lockContext);
			this.WalletKeyHistoryInfo?.ReloadFileBytes(lockContext);
		}
	}
}