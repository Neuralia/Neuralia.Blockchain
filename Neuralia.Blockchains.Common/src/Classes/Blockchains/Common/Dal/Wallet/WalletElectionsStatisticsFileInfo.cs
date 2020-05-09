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
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public interface IWalletElectionsStatisticsFileInfo : ISingleEntryWalletFileInfo {
		Task<WalletElectionsMiningAggregateStatistics> AggregateStatisticsBase(Enums.MiningTiers miningTiers, LockContext lockContext);
		Task<WalletElectionsMiningSessionStatistics> SessionStatisticsBase(Enums.MiningTiers miningTiers, LockContext lockContext);
		WalletElectionsMiningSessionStatistics SessionStatisticsBaseAsIs(Enums.MiningTiers miningTiers, LockContext lockContext);
		Task CloseSessionStatistics(LockContext lockContext);
	}

	public abstract class WalletElectionsStatisticsFileInfo<S, T> : SingleEntryWalletFileInfo<T>, IWalletElectionsStatisticsFileInfo
		where T : WalletElectionsMiningAggregateStatistics , new()
		where S : WalletElectionsMiningSessionStatistics, new() {

		private readonly IWalletAccount account;

		private S sessionStatistics;
		private T aggregateStatistics;

		private readonly RecursiveAsyncLock sessionLocker = new RecursiveAsyncLock();
		private readonly RecursiveAsyncLock aggregateLocker = new RecursiveAsyncLock();
		
		public virtual async Task<T> AggregateStatistics(Enums.MiningTiers miningTier, LockContext lockContext) {

			using(var handle = await this.aggregateLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				
				if(this.aggregateStatistics == default || this.aggregateStatistics.MiningTier != (byte)miningTier) {
					this.aggregateStatistics = default;
					this.aggregateStatistics = await this.RunQueryDbOperation((litedbDal, lc) => {

						byte tier = (byte) miningTier;

						if(litedbDal.CollectionExists<T>() && litedbDal.Exists<T>(s => s.MiningTier == tier)) {
							return Task.FromResult(litedbDal.GetOne<T>(s => s.MiningTier == tier));
						}

						return Task.FromResult(default(T));

					}, lockContext).ConfigureAwait(false);

					if(this.aggregateStatistics == default(T)) {
						this.aggregateStatistics = await this.RunDbOperation((litedbDal, lc) => {

							byte tier = (byte) miningTier;

							if(!litedbDal.CollectionExists<T>()) {
								litedbDal.CreateDbFile<T, ObjectId>(s => s.Id);
							}

							if(!litedbDal.Exists<T>(s => s.MiningTier == tier)) {
								var statistic = new T();
								statistic.MiningTier = tier;
								litedbDal.Insert(statistic, s => s.Id);

								return Task.FromResult(statistic);
							}

							return Task.FromResult(litedbDal.GetOne<T>(s => s.MiningTier == tier));

						}, lockContext).ConfigureAwait(false);
					}
				}
			}

			return this.aggregateStatistics;
		}
		
		public async Task<WalletElectionsMiningAggregateStatistics> AggregateStatisticsBase(Enums.MiningTiers miningTiers, LockContext lockContext) {
			return (WalletElectionsMiningAggregateStatistics) await this.AggregateStatistics(miningTiers, lockContext).ConfigureAwait(false);
		}
		
		public virtual async Task<S> SessionStatistics(Enums.MiningTiers miningTier, LockContext lockContext) {

			using(var handle = await this.sessionLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				
				if(this.sessionStatistics == default || this.sessionStatistics.MiningTier != (byte)miningTier) {

					if(this.sessionStatistics != default && this.sessionStatistics.MiningTier != (byte) miningTier) {
						try {
							await this.CloseSessionStatistics(lockContext).ConfigureAwait(false);
						} catch {
							
						}
						finally{
							this.sessionStatistics = null;
						}
					}
					this.sessionStatistics = await this.RunDbOperation((litedbDal, lc) => {

						byte tier = (byte) miningTier;

						if(!litedbDal.CollectionExists<S>()) {
							litedbDal.CreateDbFile<S, ObjectId>(s => s.Id);
						}
						
						var statistic = new S();
						statistic.MiningTier = tier;
						statistic.Start = DateTimeEx.CurrentTime;
						
						litedbDal.Insert(statistic, s => s.Id);

						return Task.FromResult(statistic);
					}, lockContext).ConfigureAwait(false);
				}
			}

			return this.sessionStatistics;
		}
		
		public async Task<WalletElectionsMiningSessionStatistics> SessionStatisticsBase(Enums.MiningTiers miningTiers, LockContext lockContext) {
			return (WalletElectionsMiningSessionStatistics) await this.SessionStatistics(miningTiers, lockContext).ConfigureAwait(false);
		}

		public WalletElectionsMiningSessionStatistics SessionStatisticsBaseAsIs(Enums.MiningTiers miningTiers, LockContext lockContext) {

			using(var handle = this.sessionLocker.Lock(lockContext)) {
				return (WalletElectionsMiningSessionStatistics) this.sessionStatistics;
			}
		}
		
		public async Task CloseSessionStatistics(LockContext lockContext) {
			using(var handle = await this.sessionLocker.LockAsync(lockContext).ConfigureAwait(false)) {

				if(this.sessionStatistics != null) {
					try {
						this.sessionStatistics.Stop = DateTimeEx.CurrentTime;

						await RunDbOperation((litedbDal, lc) => {
							litedbDal.Update(this.sessionStatistics);

							return Task.CompletedTask;
						}, lockContext).ConfigureAwait(false);
					} 
					finally {
						this.sessionStatistics = null;
					}
				}
			}
		}
		public WalletElectionsStatisticsFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.account = account;

		}
		
		protected override Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext) {
			litedbDal.CreateDbFile<T, ObjectId>(i => i.Id);

			litedbDal.CreateDbFile<S, ObjectId>(s => s.Id);

			return Task.CompletedTask;
		}
		
		public override async Task Reset(LockContext lockContext) {
			await base.Reset(lockContext).ConfigureAwait(false);

			this.ClearCached(lockContext);
		}
		
		public override void ClearCached(LockContext lockContext) {
			base.ClearCached(lockContext);
			
			this.sessionStatistics = null;
			this.aggregateStatistics = null;
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

		protected override async Task UpdateDbEntry(LockContext lockContext) {
			await RunDbOperation((litedbDal, lc) => {
				if(litedbDal.CollectionExists<S>() && this.sessionStatistics != default) {
					litedbDal.Update(this.sessionStatistics);
				}
				
				if(litedbDal.CollectionExists<T>() && this.aggregateStatistics != default) {
					litedbDal.Update(this.aggregateStatistics);
				}

				return Task.CompletedTask;
			}, lockContext).ConfigureAwait(false);
		}

		protected override Task InsertNewDbData(T entry, LockContext lockContext) {
			// do nothing
			return Task.CompletedTask;
		}
	}
}