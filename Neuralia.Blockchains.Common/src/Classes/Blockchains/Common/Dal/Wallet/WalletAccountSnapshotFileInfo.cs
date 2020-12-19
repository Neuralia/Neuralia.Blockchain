using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface IWalletAccountSnapshotFileInfo : ISingleEntryWalletFileInfo {
		Task<IWalletAccountSnapshot> WalletAccountSnapshot(LockContext lockContext);
		void SetWalletAccountSnapshot(IWalletAccountSnapshot snapshot);
		Task CreateEmptyFile(IWalletStandardAccountSnapshot entry, LockContext lockContext);

		Task InsertNewSnapshotBase(IWalletStandardAccountSnapshot snapshot, LockContext lockContext);

		Task InsertNewJointSnapshotBase(IWalletJointAccountSnapshot snapshot, LockContext lockContext);
	}

	public interface IWalletAccountSnapshotFileInfo<T> : IWalletAccountSnapshotFileInfo
		where T : IWalletAccountSnapshot {

		Task<T> WalletAccountSnapshot(LockContext lockContext);
		void SetWalletAccountSnapshot(T snapshot);

		Task InsertNewSnapshot(T snapshot, LockContext lockContext);
	}

	public abstract class WalletAccountSnapshotFileInfo<T> : SingleEntryWalletFileInfo<T>, IWalletAccountSnapshotFileInfo<T>
		where T : IWalletAccountSnapshot {

		private readonly IWalletAccount account;
		private T walletAccountSnapshot;

		public WalletAccountSnapshotFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		public async Task<T> WalletAccountSnapshot(LockContext lockContext) {

			if(this.walletAccountSnapshot == null) {
				this.walletAccountSnapshot = await this.RunQueryDbOperation(async (dbdal, lc) => {

					if(dbdal.CollectionExists<T>() && dbdal.Any<T>()) {
						return dbdal.GetSingle<T>();
					}

					return default;
				}, lockContext).ConfigureAwait(false);
			}

			return this.walletAccountSnapshot;

		}
		protected override T CreateEntryType() {
			return default;
		}

		public void SetWalletAccountSnapshot(T snapshot) {
			this.walletAccountSnapshot = snapshot;
		}

		public void SetWalletAccountSnapshot(IWalletAccountSnapshot snapshot) {
			if(snapshot is T castedSnapshot) {
				this.SetWalletAccountSnapshot(castedSnapshot);
			}
		}

		public override async Task Reset(LockContext lockContext) {
			await base.Reset(lockContext).ConfigureAwait(false);

			this.walletAccountSnapshot = default;
		}

		public Task InsertNewSnapshot(T snapshot, LockContext lockContext) {
			return this.InsertNewDbData(snapshot, lockContext);
		}

		public Task InsertNewSnapshotBase(IWalletStandardAccountSnapshot snapshot, LockContext lockContext) {
			return this.InsertNewDbData((T) snapshot, lockContext);
		}

		public Task InsertNewJointSnapshotBase(IWalletJointAccountSnapshot snapshot, LockContext lockContext) {
			return this.InsertNewDbData((T) snapshot, lockContext);
		}

		async Task<IWalletAccountSnapshot> IWalletAccountSnapshotFileInfo.WalletAccountSnapshot(LockContext lockContext) {
			return await this.WalletAccountSnapshot(lockContext).ConfigureAwait(false);
		}

		public Task CreateEmptyFile(IWalletStandardAccountSnapshot entry, LockContext lockContext) {
			if(entry.GetType() != typeof(T)) {
				throw new ApplicationException("Type must be the same as the snapshot type");
			}

			return base.CreateEmptyFile((T) entry, lockContext);
		}

		public async Task<IWalletAccountSnapshot> WalletSnapshotBase(LockContext lockContext) {
			return await this.WalletAccountSnapshot(lockContext).ConfigureAwait(false);
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override async Task InsertNewDbData(T snapshot, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.walletAccountSnapshot = snapshot;

				await this.RunDbOperation((dbdal, lc) => {
					if(dbdal.CollectionExists<T>() && dbdal.Exists<T>(s => s.AccountId == snapshot.AccountId)) {
						throw new ApplicationException($"Snapshot with Id {snapshot.AccountId} already exists");
					}

					dbdal.Insert(snapshot, c => c.AccountId);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);
			}
		}

		protected override Task CreateDbFile(IWalletDBDAL dbdal, LockContext lockContext) {
			dbdal.CreateDbFile<IWalletAccountSnapshot, long>(i => i.AccountId);

			return Task.CompletedTask;
		}

		protected override Task PrepareEncryptionInfo(LockContext lockContext) {
			return this.CreateSecurityDetails(lockContext);
		}

		protected override async Task CreateSecurityDetails(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.EncryptionInfo == null) {
					this.EncryptionInfo = new EncryptionInfo();

					this.EncryptionInfo.Encrypt = this.WalletSecurityDetails.EncryptWallet;

					if(this.EncryptionInfo.Encrypt) {

						this.EncryptionInfo.EncryptionParameters = this.account.SnapshotFileEncryptionParameters;
						this.EncryptionInfo.SecretHandler = () => this.account.KeyLogFileSecret;
					}
				}
			}
		}

		protected override Task UpdateDbEntry(LockContext lockContext) {
			return this.RunDbOperation((dbdal, lc) => {
				if(dbdal.CollectionExists<T>()) {
					dbdal.Update(this.WalletAccountSnapshot(lc).WaitAndUnwrapException());
				}

				return Task.CompletedTask;
			}, lockContext);

		}
	}
}