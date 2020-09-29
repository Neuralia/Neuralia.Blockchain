using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface IWalletGenerationCacheFileInfo : ISingleEntryWalletFileInfo {
		Task InsertCacheEntry(IWalletGenerationCache generationCacheEntry, LockContext lockContext);
		Task RemoveEntry(string key, LockContext lockContext);
		Task RemoveEntry<K>(K key, LockContext lockContext);
		Task UpdateEntryBase(IWalletGenerationCache entry, LockContext lockContext);
		Task<IWalletGenerationCache> GetEntryBase(string key, LockContext lockContext);
		Task<IWalletGenerationCache> GetEntryBase(WalletGenerationCache.DispatchEventTypes type, string subtype, LockContext lockContext);
		Task<IWalletGenerationCache> GetEntryBase<K>(K key, LockContext lockContext);
		Task<List<IWalletGenerationCache>> GetRetryEntriesBase(LockContext lockContext);
		Task<int> ClearTimedOut(DateTime lastBlockTimestamp, LockContext lockContext);
		Task<int> ClearTimedOut(LockContext lockContext);
	}

	public class WalletGenerationCacheFileInfo<T> : TypedEntryWalletFileInfo<T>, IWalletGenerationCacheFileInfo
		where T : WalletGenerationCache {

		private readonly IWalletAccount account;

		public WalletGenerationCacheFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;
		}

		public Task InsertCacheEntry(IWalletGenerationCache generationCacheEntry, LockContext lockContext) {
			return this.InsertCacheEntry((T) generationCacheEntry, lockContext);
		}

		public async Task RemoveEntry(string key, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {
						litedbDal.Remove<T>(k => k.Key == key);
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		public Task RemoveEntry<K>(K key, LockContext lockContext) {
			return this.RemoveEntry(key.ToString(), lockContext);
		}

		public Task UpdateEntryBase(IWalletGenerationCache entry, LockContext lockContext) {
			return UpdateEntry((T)entry, lockContext);
		}

		public async Task UpdateEntry(T entry, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {

				ClosureWrapper<bool> update = false;
				await this.RunDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {

						if(litedbDal.Exists<T>(k => k.Key == entry.Key)) {
							litedbDal.Update(entry);
							update = true;
						} 
					}

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

				if(update) {
					await this.Save(handle).ConfigureAwait(false);
				} else {
					await this.InsertCacheEntry(entry, lockContext).ConfigureAwait(false);
				}
			}
		}

		public async Task<IWalletGenerationCache> GetEntryBase(string key, LockContext lockContext) {
			return await this.GetEntry(key, lockContext).ConfigureAwait(false);
		}

		public Task<IWalletGenerationCache> GetEntryBase<K>(K key, LockContext lockContext) {
			return this.GetEntryBase(key.ToString(), lockContext);
		}

		public async Task<IWalletGenerationCache> GetEntryBase(WalletGenerationCache.DispatchEventTypes type, string subtype, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await this.RunQueryDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {

						return Task.FromResult(litedbDal.GetOne<T>(k => k.EventType == type && k.EventSubType == subtype));
					}

					return Task.FromResult((T) default);
				}, handle).ConfigureAwait(false);
			}
		}

		public async Task<List<IWalletGenerationCache>> GetRetryEntriesBase(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await this.RunQueryDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {

						var now = DateTimeEx.CurrentTime;
						var result = litedbDal.Get<T>(e => e.Expiration.ToUniversalTime() >= now && e.NextRetry.HasValue && e.NextRetry.Value.ToUniversalTime() <= now);

						if(result == null) {
							result = new List<T>();
						}
						return Task.FromResult(result.Cast<IWalletGenerationCache>().ToList());
					}

					return Task.FromResult(new List<IWalletGenerationCache>());
				}, handle).ConfigureAwait(false);
			}
		}

		public Task<int> ClearTimedOut(LockContext lockContext) {
			return this.ClearTimedOut(DateTimeEx.CurrentTime, lockContext);
		}
		
		/// <summary>
		///     remove all timed outs
		/// </summary>
		/// <returns></returns>
		public async Task<int> ClearTimedOut(DateTime lastBlockTimestamp, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await this.RunQueryDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {

						return Task.FromResult(litedbDal.Remove<T>(k => k.Expiration < lastBlockTimestamp));
					}

					return Task.FromResult(0);
				}, handle).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected Task InsertNewDbData(T transactionCache, LockContext lockContext) {

			return this.RunDbOperation((litedbDal, lc) => {
				litedbDal.Insert(transactionCache, c => c.Key);

				return Task.CompletedTask;
			}, lockContext);

		}

		protected override Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext) {
			litedbDal.CreateDbFile<T, string>(i => i.Key);

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
		
		public async Task InsertCacheEntry(T transactionCacheEntry, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				await this.RunDbOperation((litedbDal, lc) => {

					litedbDal.Insert(transactionCacheEntry, k => k.Key);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(true);

				await this.Save(handle).ConfigureAwait(false);
			}
		}

		public Task<bool> EntryExists(string key, LockContext lockContext) {

			return this.RunQueryDbOperation((litedbDal, lc) => {

				if(!litedbDal.CollectionExists<T>()) {
					return Task.FromResult(false);
				}

				return Task.FromResult(litedbDal.Exists<T>(k => k.Key == key.ToString()));
			}, lockContext);
		}

		public Task<T> GetEntry<K>(K key, LockContext lockContext) {
			return this.GetEntry(key.ToString(), lockContext);
		}

		public async Task<T> GetEntry(string key, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				return await this.RunQueryDbOperation((litedbDal, lc) => {
					if(litedbDal.CollectionExists<T>()) {

						return Task.FromResult(litedbDal.GetOne<T>(k => k.Key == key.ToString()));
					}

					return Task.FromResult((T) default);
				}, handle).ConfigureAwait(false);
			}
		}
	}
}