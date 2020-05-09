using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public interface IWalletElectionsHistoryFileInfo : ITypedEntryWalletFileInfo {
		Task InsertElectionsHistoryEntry(IWalletElectionsHistory transactionHistoryEntry, LockContext lockContext);
	}

	public abstract class WalletElectionsHistoryFileInfo<T> : TypedEntryWalletFileInfo<T>, IWalletElectionsHistoryFileInfo
		where T : WalletElectionsHistory {

		private readonly IWalletAccount account;

		public WalletElectionsHistoryFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}

		public Task InsertElectionsHistoryEntry(IWalletElectionsHistory transactionHistoryEntry, LockContext lockContext) {
			return this.InsertElectionsHistoryEntry((T) transactionHistoryEntry, lockContext);
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected Task InsertNewDbData(T transactionHistory, LockContext lockContext) {

			return this.RunDbOperation((litedbDal, lc) => {
				litedbDal.Insert(transactionHistory, c => c.BlockId);

				return Task.CompletedTask;
			}, lockContext);
		}

		protected override Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext) {
			litedbDal.CreateDbFile<T, long>(i => i.BlockId);
			
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

						this.EncryptionInfo.EncryptionParameters = this.account.KeyLogFileEncryptionParameters;
						this.EncryptionInfo.Secret = () => this.account.KeyLogFileSecret;
					}
				}
			}
		}

		public async Task InsertElectionsHistoryEntry(T transactionHistoryEntry, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((litedbDal, lc) => {

					if(litedbDal.CollectionExists<T>() && litedbDal.Exists<T>(k => k.BlockId == transactionHistoryEntry.BlockId)) {
						return Task.CompletedTask;
					}

					litedbDal.Insert(transactionHistoryEntry, k => k.BlockId);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}
	}
}