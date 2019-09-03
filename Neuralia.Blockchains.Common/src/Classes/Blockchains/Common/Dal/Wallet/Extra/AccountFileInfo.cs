using System.Collections.Generic;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Extra {

	public interface IAccountFileInfo {

		AccountPassphraseDetails AccountSecurityDetails { get; }
		WalletChainStateFileInfo WalletChainStatesInfo { get; set; }
		WalletElectionCacheFileInfo WalletElectionCacheInfo { get; set; }
		WalletKeyLogFileInfo WalletKeyLogsInfo { get; set; }
		Dictionary<string, WalletKeyFileInfo> WalletKeysFileInfo { get; }
		IWalletAccountSnapshotFileInfo WalletSnapshotInfo { get; set; }
		IWalletTransactionCacheFileInfo WalletTransactionCacheInfo { get; set; }
		IWalletTransactionHistoryFileInfo WalletTransactionHistoryInfo { get; set; }
		IWalletElectionsHistoryFileInfo WalletElectionsHistoryInfo { get; set; }
		WalletKeyHistoryFileInfo WalletKeyHistoryInfo { get; set; }
		void Load();
		void Save();
		void ChangeEncryption();
		void Reset();
		void ReloadFileBytes();
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
		public IWalletTransactionCacheFileInfo WalletTransactionCacheInfo { get; set; }
		public IWalletTransactionHistoryFileInfo WalletTransactionHistoryInfo { get; set; }
		public IWalletElectionsHistoryFileInfo WalletElectionsHistoryInfo { get; set; }
		public WalletKeyHistoryFileInfo WalletKeyHistoryInfo { get; set; }

		public virtual void Load() {
			this.WalletKeyLogsInfo.Load();
			this.WalletChainStatesInfo.Load();
			this.WalletTransactionCacheInfo.Load();
			this.WalletTransactionHistoryInfo.Load();
			this.WalletElectionsHistoryInfo.Load();
			this.WalletElectionCacheInfo?.Load();
			this.WalletSnapshotInfo?.Load();
			this.WalletKeyHistoryInfo?.Load();
		}

		public virtual void Save() {

			this.WalletKeyLogsInfo.Save();
			this.WalletChainStatesInfo.Save();
			this.WalletTransactionCacheInfo.Save();
			this.WalletTransactionHistoryInfo.Save();
			this.WalletElectionsHistoryInfo.Save();
			this.WalletElectionCacheInfo?.Save();
			this.WalletSnapshotInfo?.Save();
			this.WalletKeyHistoryInfo?.Save();
		}

		public virtual void ChangeEncryption() {

			this.WalletKeyLogsInfo.ChangeEncryption();
			this.WalletChainStatesInfo.ChangeEncryption();
			this.WalletTransactionCacheInfo.ChangeEncryption();
			this.WalletTransactionHistoryInfo.ChangeEncryption();
			this.WalletElectionsHistoryInfo.ChangeEncryption();
			this.WalletElectionCacheInfo?.ChangeEncryption();
			this.WalletSnapshotInfo?.ChangeEncryption();
			this.WalletKeyHistoryInfo?.ChangeEncryption();
		}

		public virtual void Reset() {
			this.WalletKeysFileInfo.ForEach(e => e.Value.Reset());
			this.WalletKeyLogsInfo.Reset();
			this.WalletChainStatesInfo.Reset();
			this.WalletTransactionCacheInfo.Reset();
			this.WalletTransactionHistoryInfo.Reset();
			this.WalletElectionsHistoryInfo.Reset();
			this.WalletElectionCacheInfo?.Reset();
			this.WalletSnapshotInfo?.Reset();
			this.WalletKeyHistoryInfo?.Reset();
		}

		public virtual void ReloadFileBytes() {
			this.WalletKeysFileInfo.ForEach(e => e.Value.ReloadFileBytes());
			this.WalletKeyLogsInfo.ReloadFileBytes();
			this.WalletChainStatesInfo.ReloadFileBytes();
			this.WalletTransactionCacheInfo.ReloadFileBytes();
			this.WalletTransactionHistoryInfo.ReloadFileBytes();
			this.WalletElectionsHistoryInfo.ReloadFileBytes();
			this.WalletElectionCacheInfo?.ReloadFileBytes();
			this.WalletSnapshotInfo?.ReloadFileBytes();
			this.WalletKeyHistoryInfo?.ReloadFileBytes();
		}
	}
}