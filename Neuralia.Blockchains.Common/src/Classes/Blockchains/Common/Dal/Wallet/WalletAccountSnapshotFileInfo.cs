using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface IWalletAccountSnapshotFileInfo : ISingleEntryWalletFileInfo {
		IWalletAccountSnapshot WalletAccountSnapshot { get; }
		void CreateEmptyFile(IWalletStandardAccountSnapshot entry);

		void InsertNewSnapshotBase(IWalletStandardAccountSnapshot snapshot);

		void InsertNewJointSnapshotBase(IWalletJointAccountSnapshot snapshot);
	}

	public interface IWalletAccountSnapshotFileInfo<T> : IWalletAccountSnapshotFileInfo
		where T : IWalletAccountSnapshot {

		new T WalletAccountSnapshot { get; }

		void InsertNewSnapshot(T snapshot);
	}

	public abstract class WalletAccountSnapshotFileInfo<T> : SingleEntryWalletFileInfo<T>, IWalletAccountSnapshotFileInfo<T>
		where T : IWalletAccountSnapshot {

		private readonly IWalletAccount account;
		private T walletAccountSnapshot;

		public WalletAccountSnapshotFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		public IWalletAccountSnapshot WalletSnapshotBase => this.WalletAccountSnapshot;

		public T WalletAccountSnapshot {
			get {
				if(this.walletAccountSnapshot == null) {
					this.walletAccountSnapshot = this.RunQueryDbOperation(litedbDal => {

						if(litedbDal.CollectionExists<T>() && litedbDal.Any<T>()) {
							return litedbDal.GetSingle<T>();
						}

						return default;
					});
				}

				return this.walletAccountSnapshot;
			}
		}

		public override void Reset() {
			base.Reset();

			this.walletAccountSnapshot = default;
		}

		public void InsertNewSnapshot(T snapshot) {
			this.InsertNewDbData(snapshot);
		}

		public void InsertNewSnapshotBase(IWalletStandardAccountSnapshot snapshot) {
			this.InsertNewDbData((T) snapshot);
		}

		public void InsertNewJointSnapshotBase(IWalletJointAccountSnapshot snapshot) {
			this.InsertNewDbData((T) snapshot);
		}

		IWalletAccountSnapshot IWalletAccountSnapshotFileInfo.WalletAccountSnapshot => this.WalletAccountSnapshot;

		public void CreateEmptyFile(IWalletStandardAccountSnapshot entry) {
			if(entry.GetType() != typeof(T)) {
				throw new ApplicationException("Type must be the same as the snapshot type");
			}

			base.CreateEmptyFile((T) entry);
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override void InsertNewDbData(T snapshot) {
			lock(this.locker) {
				this.walletAccountSnapshot = snapshot;

				this.RunDbOperation(litedbDal => {
					if(litedbDal.CollectionExists<T>() && litedbDal.Exists<T>(s => s.AccountId == snapshot.AccountId)) {
						throw new ApplicationException($"Snapshot with Id {snapshot.AccountId} already exists");
					}

					litedbDal.Insert(snapshot, c => c.AccountId);
				});
			}
		}

		protected override void CreateDbFile(LiteDBDAL litedbDal) {
			litedbDal.CreateDbFile<IWalletAccountSnapshot, long>(i => i.AccountId);
		}

		protected override void PrepareEncryptionInfo() {
			this.CreateSecurityDetails();
		}

		protected override void CreateSecurityDetails() {
			lock(this.locker) {
				if(this.EncryptionInfo == null) {
					this.EncryptionInfo = new EncryptionInfo();

					this.EncryptionInfo.Encrypt = this.WalletSecurityDetails.EncryptWallet;

					if(this.EncryptionInfo.Encrypt) {

						this.EncryptionInfo.EncryptionParameters = this.account.KeyLogFileEncryptionParameters;
						this.EncryptionInfo.Secret = () => this.account.KeyLogFileSecret;
					}
				}
			}
		}

		protected override void UpdateDbEntry() {
			this.RunDbOperation(litedbDal => {
				if(litedbDal.CollectionExists<T>()) {
					litedbDal.Update(this.WalletAccountSnapshot);
				}
			});

		}
	}
}