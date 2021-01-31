using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Extra;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IWalletProviderProxyTransactions : IChainProvider {

		bool IsActiveTransaction { get; }
		bool IsActiveTransactionThread(int threadId);

		Task<(K result, bool completed)> ScheduleReadNoWait<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext);
		Task<K> ScheduleRead<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, int timeout = 60);
		Task<K> ScheduleRead<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, int timeout = 60);
		Task ScheduleRead(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, int timeout = 60);
		Task<K> ScheduleWrite<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, int timeout = 60);
		Task<K> ScheduleWrite<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, int timeout = 60);
		Task ScheduleWrite(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, int timeout = 60);
		Task ScheduleKeyedRead(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K> ScheduleKeyedRead<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K> ScheduleKeyedRead<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K> ScheduleKeyedWrite<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K> ScheduleKeyedWrite<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task ScheduleKeyedWrite(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task ScheduleKeyedWrite(Action<IWalletProviderInternal, LockContext> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K> ScheduleTransaction<K>(Func<IWalletProvider, CancellationToken, LockContext, Task<K>> action, LockContext lockContext, int timeout = 60, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task ScheduleTransaction(Func<IWalletProvider, CancellationToken, LockContext, Task> action, LockContext lockContext, int timeout = 60, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);

		public Task AddTransactionSuccessActions(List<Func<LockContext, Task>> transactionalSuccessActions, LockContext lockContext);
	}

	public interface IWalletProviderProxy : IWalletProvider, IWalletProviderProxyTransactions, IDisposableExtended {
		Task<bool?> SyncedNoWait(LockContext lockContext);
	}

	public interface IWalletProviderProxyInternal : IWalletProviderProxy {
		public IWalletProvider UnderlyingWalletProvider { get; }
	}

	public abstract class WalletProviderProxy<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IWalletProviderProxy, IWalletProviderProxyInternal
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		protected static readonly TimeSpan defaultTransactionTimeout = TimeSpan.FromMinutes(1);

		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		protected readonly RecursiveResourceAccessScheduler<IWalletProviderInternal> RecursiveResourceAccessScheduler;

		protected readonly IWalletProviderInternal walletProvider;

		public WalletProviderProxy(CENTRAL_COORDINATOR centralCoordinator, IWalletProvider walletProvider) {
			this.walletProvider = (IWalletProviderInternal) walletProvider;

			this.centralCoordinator = centralCoordinator;
			this.RecursiveResourceAccessScheduler = new RecursiveResourceAccessScheduler<IWalletProviderInternal>(this.walletProvider);
		}

		public string GetChainDirectoryPath() {
			return this.walletProvider.GetChainDirectoryPath();
		}

		public string GetChainStorageFilesPath() {
			return this.walletProvider.GetChainStorageFilesPath();
		}

		public string GetSystemFilesDirectoryPath() {
			return this.walletProvider.GetSystemFilesDirectoryPath();
		}

		public SynthesizedBlockAPI DeserializeSynthesizedBlockAPI(string synthesizedBlock) {
			return this.walletProvider.DeserializeSynthesizedBlockAPI(synthesizedBlock);
		}

		public Task<SynthesizedBlock> ConvertApiSynthesizedBlock(SynthesizedBlockAPI synthesizedBlockApi, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.ConvertApiSynthesizedBlock(synthesizedBlockApi, lc), lockContext);
		}

		public bool IsWalletLoaded => this.walletProvider.IsWalletLoaded;

		public Task<bool> IsWalletEncrypted(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.IsWalletEncrypted(lc), lockContext);
		}

		public Task<bool> IsWalletAccountLoaded(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.IsWalletAccountLoaded(lc), lockContext);
		}

		public Task<bool> WalletFileExists(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.WalletFileExists(lc), lockContext);
		}
		
		public Task<bool> WalletFullyCreated(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.WalletFullyCreated(lc), lockContext);
		}

		public void EnsureWalletIsLoaded() {
			this.walletProvider.EnsureWalletIsLoaded();
		}

		public Task RemovePIDLock() {
			return this.walletProvider.RemovePIDLock();
		}

		public Task<long?> LowestAccountBlockSyncHeight(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.LowestAccountBlockSyncHeight(lc), lockContext);
		}
		
		public Task<long?> LowestAccountPreviousBlockSyncHeight(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.LowestAccountPreviousBlockSyncHeight(lc), lockContext);
		}

		public Task<bool?> Synced(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.Synced(lc), lockContext);
		}

		public async Task<bool?> SyncedNoWait(LockContext lockContext) {

			(bool? result, bool completed) = await this.ScheduleReadNoWait((p, lc) => p.Synced(lc), lockContext).ConfigureAwait(false);

			return completed ? result : null;

		}

		public Task<bool> WalletContainsAccount(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.WalletContainsAccount(accountCode, lc), lockContext);
		}

		public Task<List<IWalletAccount>> GetWalletSyncableAccounts(long? newBlockId, long latestSyncedBlockId, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletSyncableAccounts(newBlockId, latestSyncedBlockId, lc), lockContext);
		}

		public Task<IAccountFileInfo> GetAccountFileInfo(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAccountFileInfo(accountCode, lc), lockContext);
		}

		public Task<List<IWalletAccount>> GetAccounts(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAccounts(lc), lockContext);
		}

		public Task<List<IWalletAccount>> GetAllAccounts(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAllAccounts(lc), lockContext);
		}

		public Task<string> GetAccountCode(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAccountCode(lc), lockContext);
		}

		public Task<AccountId> GetPublicAccountId(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetPublicAccountId(lc), lockContext);
		}

		public Task<AccountId> GetPublicAccountId(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetPublicAccountId(accountCode, lc), lockContext);
		}

		public Task<AccountId> GetInitiationId(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetInitiationId(lc), lockContext);
		}

		public Task<bool> IsDefaultAccountPublished(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.IsDefaultAccountPublished(lc), lockContext);
		}

		public Task<bool> IsAccountPublished(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.IsAccountPublished(accountCode, lc), lockContext);
		}

		public Task<bool> HasAccount(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.HasAccount(lc), lockContext);
		}
		
		public Task<IWalletAccount> GetActiveAccount(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetActiveAccount(lc), lockContext);
		}

		public Task<IWalletAccount> GetWalletAccount(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletAccount(accountCode, lc), lockContext);
		}

		public Task<IWalletAccount> GetWalletAccountByName(string name, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletAccountByName(name, lc), lockContext);
		}

		public Task<IWalletAccount> GetWalletAccount(AccountId accountId, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletAccount(accountId, lc), lockContext);
		}

		public Task WriteDistilledAppointmentContextFile(DistilledAppointmentContext distilledAppointmentContext) {
			return this.walletProvider.WriteDistilledAppointmentContextFile(distilledAppointmentContext);
		}

		public void ClearDistilledAppointmentContextFile() {
			this.walletProvider.ClearDistilledAppointmentContextFile();
		}

		public Task UpdateMiningStatistics(AccountId accountId, Enums.MiningTiers miningTiers, Action<WalletElectionsMiningSessionStatistics> sessionCallback, Action<WalletElectionsMiningAggregateStatistics> totalCallback, LockContext lockContext, bool resetSession = false) {
			return this.ScheduleTransaction((p, ct, lc) => p.UpdateMiningStatistics(accountId, miningTiers, sessionCallback, totalCallback, lc, resetSession), lockContext);
		}
		
		public Task StopSessionMiningStatistics(AccountId accountId, LockContext lockContext) {
			return this.ScheduleTransaction((p, ct, lc) => p.StopSessionMiningStatistics(accountId, lc), lockContext);
		}

		public Task<(MiningStatisticSessionAPI session, MiningStatisticAggregateAPI aggregate)> QueryMiningStatistics(AccountId miningAccountId, Enums.MiningTiers miningTiers, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.QueryMiningStatistics(miningAccountId, miningTiers, lc), lockContext);
		}
		
		public Task<Dictionary<AccountId, int>> ClearTimedOutTransactions(LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.ClearTimedOutTransactions(lc), lockContext);
		}

		public Task<bool> ResetTimedOutWalletEntries(LockContext lockContext, List<(string accountCode, string name)> forcedKeys = null) {
			return this.ScheduleKeyedWrite((p, lc) => p.ResetTimedOutWalletEntries(lc, forcedKeys), lockContext);
		}

		public Task<bool> ResetAllTimedOut(LockContext lockContext, List<(string accountCode, string name)> forcedKeys = null) {
			return this.ScheduleKeyedWrite((p, lc) => p.ResetAllTimedOut(lc, forcedKeys), lockContext);
		}

		public Task<List<WalletTransactionHistoryHeaderAPI>> APIQueryWalletTransactionHistory(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletTransactionHistory(accountCode, lc), lockContext);
		}

		public Task<WalletTransactionHistoryDetailsAPI> APIQueryWalletTransactionHistoryDetails(string accountCode, string transactionId, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletTransactionHistoryDetails(accountCode, transactionId, lc), lockContext);
		}

		public Task<WalletInfoAPI> APIQueryWalletInfoAPI(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletInfoAPI(lc), lockContext);
		}

		public Task<List<WalletAccountAPI>> APIQueryWalletAccounts(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletAccounts(lc), lockContext);
		}

		public Task<WalletAccountDetailsAPI> APIQueryWalletAccountDetails(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletAccountDetails(accountCode, lc), lockContext);
		}

		public Task<WalletAccountAppointmentDetailsAPI> APIQueryWalletAccountAppointmentDetails(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletAccountAppointmentDetails(accountCode, lc), lockContext);
		}

		public Task<TransactionId> APIQueryWalletAccountPresentationTransactionId(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletAccountPresentationTransactionId(accountCode, lc), lockContext);
		}

		public Task<AccountAppointmentConfirmationResultAPI> APIQueryAppointmentConfirmationResult(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryAppointmentConfirmationResult(accountCode, lc), lockContext);
		}

		public Task<bool> ClearAppointment(string accountCode, LockContext lockContext, bool force = false) {
			return this.ScheduleKeyedWrite((p, lc) => p.ClearAppointment(accountCode, lc, force), lockContext);
		}

		public Task<AccountCanPublishAPI> APICanPublishAccount(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APICanPublishAccount(accountCode, lc), lockContext);
		}

		public Task<bool> SetSMSConfirmationCode(string accountCode, long confirmationCode, LockContext lockContext) {
			return this.ScheduleTransaction((p, ct, lc) => p.SetSMSConfirmationCode(accountCode, confirmationCode, lc), lockContext);
		}

		public Task<List<TransactionId>> GetElectionCacheTransactions(IWalletAccount account, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetElectionCacheTransactions(account, lc), lockContext);
		}

		public BlockId GetHighestCachedSynthesizedBlockId(LockContext lockContext) {
			return this.walletProvider.GetHighestCachedSynthesizedBlockId(lockContext);
		}

		public bool IsSynthesizedBlockCached(long blockId, LockContext lockContext) {
			return this.walletProvider.IsSynthesizedBlockCached(blockId, lockContext);
		}

		public SynthesizedBlock ExtractCachedSynthesizedBlock(long blockId) {
			return this.walletProvider.ExtractCachedSynthesizedBlock(blockId);
		}

		public List<SynthesizedBlock> GetCachedSynthesizedBlocks(long minimumBlockId, LockContext lockContext) {
			return this.walletProvider.GetCachedSynthesizedBlocks(minimumBlockId, lockContext);
		}

		public Task GenerateXmssKeyIndexNodeCache(string accountCode, byte ordinal, long index, LockContext lockContext = null) {
			return this.ScheduleTransaction((p, ct, lc) => p.GenerateXmssKeyIndexNodeCache(accountCode, ordinal, index, lc), lockContext, 60 * 5, lc => {
				// load wallet & key
				return this.walletProvider.EnsureWalletFileIsPresent(lc);

			});
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize =  null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			return this.walletProvider.CreateXmssKey(name, warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, int hashBits, WalletProvider.HashTypes HashType, int backupHashBits, WalletProvider.HashTypes backupHashType, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize =  null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			return this.walletProvider.CreateXmssKey(name, treeHeight, hashBits, HashType, backupHashBits, backupHashType, warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			return this.walletProvider.CreateXmssKey(name, progressCallback, cacheMode, cacheLevels, enableCache, prepare);
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, Enums.KeyHashType hashbits, Enums.KeyHashType backupHashbits, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize =  null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			return this.walletProvider.CreateXmssKey(name, treeHeight, hashbits, backupHashbits, warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}

		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null) {
			return this.walletProvider.CreateXmssmtKey(name, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}

		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize =  null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null) {
			return this.walletProvider.CreateXmssmtKey(name, warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}

		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, byte treeHeight, byte treeLayers, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize =  null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null) {
			return this.walletProvider.CreateXmssmtKey(name, treeHeight, treeLayers, hashType, backupHashType, warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}

		public Task<INTRUPrimeWalletKey> CreateNTRUPrimeKey(string name, NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes strength) {
			return this.walletProvider.CreateNTRUPrimeKey(name, strength);
		}
		
		public Task<SafeArrayHandle> CreateNTRUPrimeAppointmentRequestKey(LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNTRUPrimeAppointmentRequestKey(lc), lockContext);
		}

		public Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNewWalletStandardAccountSnapshot(account, lc), lockContext);
		}

		public Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNewWalletJointAccountSnapshot(account, lc), lockContext);
		}

		public Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNewWalletStandardAccountSnapshot(account, accountSnapshot, lc), lockContext);
		}

		public Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNewWalletJointAccountSnapshot(account, accountSnapshot, lc), lockContext);
		}

		public Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshotEntry(LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNewWalletStandardAccountSnapshotEntry(lc), lockContext);
		}

		public Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshotEntry(LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNewWalletJointAccountSnapshotEntry(lc), lockContext);
		}

		public Task<IWalletAccountSnapshot> GetWalletFileInfoAccountSnapshot(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletFileInfoAccountSnapshot(accountCode, lc), lockContext);
		}

		public Task<IWalletAccountSnapshot> GetAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAccountSnapshot(accountId, lc), lockContext);
		}

		public Task<DistilledAppointmentContext> GetDistilledAppointmentContextFile() {
			return this.walletProvider.GetDistilledAppointmentContextFile();
		}

		public Task Initialize(LockContext lockContext) {
			return this.ScheduleWrite((t, lc) => this.walletProvider.Initialize(lc), lockContext);
		}

		public Task ChangeAccountsCorrelation(ImmutableList<AccountId> enableAccounts, ImmutableList<AccountId> disableAccounts, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.ChangeAccountsCorrelation(enableAccounts, disableAccounts, lc), lockContext);
		}

		public Task CacheSynthesizedBlock(SynthesizedBlock synthesizedBlock, LockContext lockContext) {
			return this.walletProvider.CacheSynthesizedBlock(synthesizedBlock, lockContext);
		}

		public Task CleanSynthesizedBlockCache(LockContext lockContext) {
			return this.walletProvider.CleanSynthesizedBlockCache(lockContext);
		}

		public void ClearSynthesizedBlocksCache() {
			this.walletProvider.ClearSynthesizedBlocksCache();
		}

		public event Delegates.RequestCopyWalletFileDelegate CopyWalletRequest;
		public event Delegates.RequestPassphraseDelegate WalletPassphraseRequest;
		public event Delegates.RequestKeyPassphraseDelegate WalletKeyPassphraseRequest;
		public event Delegates.RequestCopyKeyFileDelegate WalletCopyKeyFileRequest;

		public Task CreateNewEmptyWallet(CorrelationContext correlationContext, bool encryptWallet, string passphrase, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, LockContext lockContext) {
			return this.ScheduleWrite((t, lc) => {
				return this.walletProvider.CreateNewEmptyWallet(correlationContext, encryptWallet, passphrase, walletCreationStepSet, lc);
			}, lockContext);
		}

		public Task<bool> AllAccountsHaveSyncStatus(SynthesizedBlock block, long previousSyncedBlockId, WalletAccountChainState.BlockSyncStatuses status, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.AllAccountsHaveSyncStatus(block, previousSyncedBlockId, status, lc);
			}, lockContext);
		}
		
		public Task<bool> AllAccountsHaveSyncStatus(BlockId blockId, long previousSyncedBlockId, WalletAccountChainState.BlockSyncStatuses status, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.AllAccountsHaveSyncStatus(blockId, previousSyncedBlockId, status, lc);
			}, lockContext);
		}

		public Task<bool> AllAccountsUpdatedWalletBlock(SynthesizedBlock block, long previousSyncedBlockId, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.AllAccountsUpdatedWalletBlock(block, previousSyncedBlockId, lc);
			}, lockContext);
		}

		public Task UpdateWalletBlock(SynthesizedBlock synthesizedBlock, long previousSyncedBlockId, Func<SynthesizedBlock, LockContext, Task> callback, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.UpdateWalletBlock(synthesizedBlock, previousSyncedBlockId, callback, lc), lockContext);
		}

		public Task<bool> AllAccountsWalletKeyLogSet(SynthesizedBlock block, long previousSyncedBlockId, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.AllAccountsWalletKeyLogSet(block, previousSyncedBlockId, lc);
			}, lockContext);
		}

		public Task<bool> SetActiveAccount(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.SetActiveAccount(accountCode, lc);
			}, lockContext);
		}

		public Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, string accountName, Enums.AccountTypes accountType, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null) {
			// this is a special case where we dont have a wallet, so no need to schedule anything. we will let the create make its own transactions
			return this.walletProvider.CreateNewCompleteWallet(correlationContext, accountName, accountType, encryptWallet, encryptKey, encryptKeysIndividually, passphrases, lockContext, accountCreatedCallback);
		}

		public Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, Enums.AccountTypes accountType, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null) {
			// this is a special case where we dont have a wallet, so no need to schedule anything. we will let the create make its own transactions

			return this.walletProvider.CreateNewCompleteWallet(correlationContext, accountType, encryptWallet, encryptKey, encryptKeysIndividually, passphrases, lockContext, accountCreatedCallback);

		}

		public Task UpdateWalletSnapshotFromDigest(IAccountSnapshotDigestChannelCard accountCard, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateWalletSnapshotFromDigest(accountCard, lc);
			}, lockContext);
		}

		public Task UpdateWalletSnapshotFromDigest(IStandardAccountSnapshotDigestChannelCard accountCard, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateWalletSnapshotFromDigest(accountCard, lc);
			}, lockContext);
		}

		public Task UpdateWalletSnapshotFromDigest(IJointAccountSnapshotDigestChannelCard accountCard, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateWalletSnapshotFromDigest(accountCard, lc);
			}, lockContext);
		}

		public Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateWalletSnapshot(accountSnapshot, lc);
			}, lockContext);
		}

		public Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateWalletSnapshot(accountSnapshot, accountCode, lc);
			}, lockContext);
		}

		public Task ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.ChangeWalletEncryption(correlationContext, encryptWallet, encryptKeys, encryptKeysIndividually, passphrases, lc);
			}, lockContext);
		}

		public Task SaveWallet(LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.SaveWallet(lc);
			}, lockContext);
		}

		public Task<IWalletAccount> CreateNewStandardAccount(string name, Enums.AccountTypes accountType, bool encryptKeys, bool encryptKeysIndividually, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, LockContext lockContext, bool setactive = false) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.CreateNewStandardAccount(name, accountType, encryptKeys, encryptKeysIndividually, correlationContext, walletCreationStepSet, accountCreationStepSet, lc, setactive);
			}, lockContext);
		}

		public Task<bool> CreateNewCompleteStandardAccount(CorrelationContext correlationContext, string accountName, Enums.AccountTypes accountType, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, Action<IWalletAccount> accountCreatedCallback = null) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.CreateNewCompleteStandardAccount(correlationContext, accountName, accountType, encryptKeys, encryptKeysIndividually, passphrases, lc, walletCreationStepSet, accountCreatedCallback);
			}, lockContext);
		}

		public Task<bool> CreateNewCompleteStandardAccount(CorrelationContext correlationContext, string accountName, Enums.AccountTypes accountType, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.CreateNewCompleteStandardAccount(correlationContext, accountName, accountType, encryptKeys, encryptKeysIndividually, passphrases, lc);
			}, lockContext);
		}

		public Task InsertKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, IdKeyUseIndexSet keyUseIndexSet, byte keyOrdinalId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertKeyLogTransactionEntry(account, transactionId, keyUseIndexSet, keyOrdinalId, lc);
			}, lockContext);
		}

		public Task InsertKeyLogBlockEntry(IWalletAccount account, BlockId blockId, byte keyOrdinalId, IdKeyUseIndexSet idKeyUseIndex, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertKeyLogBlockEntry(account, blockId, keyOrdinalId, idKeyUseIndex, lc);
			}, lockContext);
		}

		public Task InsertKeyLogDigestEntry(IWalletAccount account, int digestId, byte keyOrdinalId, IdKeyUseIndexSet idKeyUseIndex, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertKeyLogDigestEntry(account, digestId, keyOrdinalId, idKeyUseIndex, lc);
			}, lockContext);
		}

		public Task InsertKeyLogEntry(IWalletAccount account, string eventId, Enums.BlockchainEventTypes eventType, byte keyOrdinalId, IdKeyUseIndexSet idKeyUseIndex, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertKeyLogEntry(account, eventId, eventType, keyOrdinalId, idKeyUseIndex, lc);
			}, lockContext);
		}

		public Task ConfirmKeyLogBlockEntry(IWalletAccount account, BlockId blockId, long confirmationBlockId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.ConfirmKeyLogBlockEntry(account, blockId, confirmationBlockId, lc);
			}, lockContext);
		}

		public Task ConfirmKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, IdKeyUseIndexSet keyUseIndexSet, long confirmationBlockId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.ConfirmKeyLogTransactionEntry(account, transactionId, keyUseIndexSet, confirmationBlockId, lc);
			}, lockContext);
		}

		public Task<bool> KeyLogTransactionExists(IWalletAccount account, TransactionId transactionId, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.KeyLogTransactionExists(account, transactionId, lc);
			}, lockContext);
		}

		public IWalletKey CreateBasicKey(string name, CryptographicKeyType keyType) {
			return this.walletProvider.CreateBasicKey(name, keyType);
		}

		public T CreateBasicKey<T>(string name, CryptographicKeyType keyType)
			where T : IWalletKey {
			return this.walletProvider.CreateBasicKey<T>(name, keyType);
		}

		public void HashKey(IWalletKey key) {
			this.walletProvider.HashKey(key);
		}

		public Task SetChainStateHeight(string accountCode, long blockId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.SetChainStateHeight(accountCode, blockId, lc);
			}, lockContext);
		}

		public Task SetChainStateHeight(IWalletAccountChainState chainState, long blockId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.SetChainStateHeight(chainState, blockId, lc);
			}, lockContext);
		}

		public Task<long> GetChainStateHeight(string accountCode, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetChainStateHeight(accountCode, lc);
			}, lockContext);
		}

		public Task<IdKeyUseIndexSet> GetChainStateLastSyncedKeyHeight(IWalletKey key, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetChainStateLastSyncedKeyHeight(key, lc);
			}, lockContext);
		}

		public Task<WalletAccount.WalletAccountChainStateMiningCache> GetAccountMiningCache(AccountId accountId, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetAccountMiningCache(accountId, lc);
			}, lockContext);
		}

		public Task UpdateAccountMiningCache(AccountId accountId, WalletAccount.WalletAccountChainStateMiningCache miningCache, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateAccountMiningCache(accountId, miningCache, lc);
			}, lockContext);
		}

		public Task UpdateLocalChainStateKeyHeight(IWalletKey key, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateLocalChainStateKeyHeight(key, lc);
			}, lockContext);
		}

		public Task<IWalletElectionsHistory> InsertElectionsHistoryEntry(SynthesizedBlock.SynthesizedElectionResult electionResult, SynthesizedBlock synthesizedBlock, AccountId electedAccountId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertElectionsHistoryEntry(electionResult, synthesizedBlock, electedAccountId, lc);
			}, lockContext);
		}

		public Task InsertGenerationCacheEntry(IWalletGenerationCache entry, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertGenerationCacheEntry(entry, lc);
			}, lockContext);
		}

		public Task InsertTransactionHistoryEntry(ITransaction transaction, bool own, string note, BlockId blockId,  WalletTransactionHistory.TransactionStatuses status, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertTransactionHistoryEntry(transaction, own, note, blockId, status, lc);
			}, lockContext);
		}

		public Task UpdateGenerationCacheEntry(IWalletGenerationCache entry, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateGenerationCacheEntry(entry, lc);
			}, lockContext);
		}

		public Task<IWalletTransactionHistoryFileInfo> UpdateLocalTransactionHistoryEntry(ITransaction transaction, TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status, BlockId blockId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateLocalTransactionHistoryEntry(transaction, transactionId, status, blockId, lc);
			}, lockContext);
		}

		public Task<IWalletGenerationCache> GetGenerationCacheEntry(string key, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetGenerationCacheEntry(key, lc);
			}, lockContext);
		}

		public Task<IWalletGenerationCache> GetGenerationCacheEntry<T>(T key, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetGenerationCacheEntry(key, lc);
			}, lockContext);
		}

		public Task<IWalletGenerationCache> GetGenerationCacheEntry(WalletGenerationCache.DispatchEventTypes type, string subtype, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetGenerationCacheEntry(type, subtype, lc);
			}, lockContext);
		}

		public Task<List<IWalletGenerationCache>> GetRetryEntriesBase(LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetRetryEntriesBase( lc);
			}, lockContext);
		}

		public Task DeleteGenerationCacheEntry(string key, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.DeleteGenerationCacheEntry(key, lc);
			}, lockContext);
		}

		public Task DeleteGenerationCacheEntry<T>(T key, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.DeleteGenerationCacheEntry(key, lc);
			}, lockContext);
		}

		public Task CreateElectionCacheWalletFile(IWalletAccount account, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.CreateElectionCacheWalletFile(account, lc);
			}, lockContext);
		}

		public Task DeleteElectionCacheWalletFile(IWalletAccount account, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.DeleteElectionCacheWalletFile(account, lc);
			}, lockContext);
		}

		public Task InsertElectionCacheTransactions(List<TransactionId> transactionIds, long blockId, IWalletAccount account, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertElectionCacheTransactions(transactionIds, blockId, account, lc);
			}, lockContext);
		}

		public Task RemoveBlockElection(long blockId, IWalletAccount account, LockContext lockContext) {

			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.RemoveBlockElection(blockId, account, lc);
			}, lockContext);
		}

		public Task RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactionIds, IWalletAccount account, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.RemoveBlockElectionTransactions(blockId, transactionIds, account, lc);
			}, lockContext);
		}

		public Task AddAccountKey<KEY>(string accountCode, KEY key, ImmutableDictionary<int, string> passphrases, LockContext lockContext, KEY nextKey = null)
			where KEY : class, IWalletKey {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.AddAccountKey(accountCode, key, passphrases, lc, nextKey);
			}, lockContext);
		}

		public Task SetNextKey(string accountCode, IWalletKey nextKey, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.SetNextKey(accountCode, nextKey, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, nextKey.Name, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, nextKey.Name, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task SetNextKey(IWalletKey nextKey, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.SetNextKey(nextKey, lc), lockContext, lc => {
				
				if(string.IsNullOrWhiteSpace(nextKey.AccountCode)) {
					throw new ApplicationException("Key account code is not set");
				}
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(nextKey.AccountCode, nextKey.Name, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(nextKey.AccountCode, nextKey.Name, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task CreateNextXmssKey(string accountCode, string keyName, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNextXmssKey(accountCode, keyName, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task CreateNextXmssKey(string accountCode, byte ordinal, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNextXmssKey(accountCode, ordinal, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, ordinal, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<bool> IsKeyEncrypted(string accountCode, LockContext lockContext) {

			return this.ScheduleKeyedRead((p, lc) => p.IsKeyEncrypted(accountCode, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return Task.CompletedTask;
			});
		}

		public Task<bool> IsNextKeySet(string accountCode, string keyName, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.IsNextKeySet(accountCode, keyName, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadNextKey<T>(string accountCode, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			return this.ScheduleKeyedRead((p, lc) => p.LoadNextKey<T>(accountCode, keyName, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<IWalletKey> LoadNextKey(string accountCode, string keyName, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadNextKey(accountCode, keyName, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<IWalletKey> LoadKey(string accountCode, string keyName, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(accountCode, keyName, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<IWalletKey> LoadKey(string accountCode, byte ordinal, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(accountCode, ordinal, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, ordinal, 1, lc);

				return Task.CompletedTask;
			});

		}

		public Task<IWalletKey> LoadKey(string keyName, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(keyName, lc), lockContext, async lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(await this.GetAccountCode(lockContext).ConfigureAwait(false), keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(await this.GetAccountCode(lockContext).ConfigureAwait(false), keyName, 1, lc);
			});
		}

		public Task<IWalletKey> LoadKey(byte ordinal, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(ordinal, lc), lockContext, async lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(await this.GetAccountCode(lockContext).ConfigureAwait(false), ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(await this.GetAccountCode(lockContext).ConfigureAwait(false), ordinal, 1, lc);
			});
		}

		public Task<T> LoadKey<K, T>(Func<K, T> selector, string accountCode, string keyName, LockContext lockContext)
			where K : class, IWalletKey
			where T : class {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(selector, accountCode, keyName, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<K, T>(Func<K, T> selector, string accountCode, byte ordinal, LockContext lockContext)
			where K : class, IWalletKey
			where T : class {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(selector, accountCode, ordinal, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, ordinal, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(Func<T, T> selector, string accountCode, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(selector, accountCode, keyName, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(Func<T, T> selector, string accountCode, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(selector, accountCode, ordinal, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, ordinal, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(string accountCode, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(accountCode, keyName, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(string accountCode, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {

			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(accountCode, ordinal, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, ordinal, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(string keyName, LockContext lockContext)
			where T : class, IWalletKey {

			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(keyName, lc), lockContext, async lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(await this.GetAccountCode(lockContext).ConfigureAwait(false), keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(await this.GetAccountCode(lockContext).ConfigureAwait(false), keyName, 1, lc);
			});
		}

		public Task<T> LoadKey<T>(byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {

			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(ordinal, lc), lockContext, async lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(await this.GetAccountCode(lockContext).ConfigureAwait(false), ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(await this.GetAccountCode(lockContext).ConfigureAwait(false), ordinal, 1, lc);
			});
		}

		public Task UpdateKey(IWalletKey key, LockContext lockContext) {

			return this.ScheduleKeyedWrite((p, lc) => p.UpdateKey(key, lc), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(key.AccountCode, key.Name, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(key.AccountCode, key.Name, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task SwapNextKey(IWalletKey key, LockContext lockContext, bool storeHistory = true) {

			return this.ScheduleKeyedWrite((p, lc) => p.SwapNextKey(key, lc, storeHistory), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(key.AccountCode, key.Name, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(key.AccountCode, key.Name, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task SwapNextKey(string accountCode, string keyName, LockContext lockContext, bool storeHistory = true) {
			return this.ScheduleKeyedWrite((p, lc) => p.SwapNextKey(accountCode, keyName, lc, storeHistory), lockContext, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountCode, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountCode, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task EnsureWalletLoaded(LockContext lockContext) {

			return this.ScheduleKeyedWrite((p, lc) => p.EnsureWalletLoaded(lc), lockContext, async lc => {
				// load wallet & key
				await this.walletProvider.EnsureWalletFileIsPresent(lc).ConfigureAwait(false);
				await this.walletProvider.EnsureWalletPassphrase(lc).ConfigureAwait(false);
			});

		}

		public Task SetExternalPassphraseHandlers(Delegates.RequestPassphraseDelegate requestPassphraseDelegate, Delegates.RequestKeyPassphraseDelegate requestKeyPassphraseDelegate, Delegates.RequestCopyKeyFileDelegate requestKeyCopyFileDelegate, Delegates.RequestCopyWalletFileDelegate copyWalletDelegate, LockContext lockContext) {
			return this.ScheduleWrite((t, lc) => {
				return this.walletProvider.SetExternalPassphraseHandlers(requestPassphraseDelegate, requestKeyPassphraseDelegate, requestKeyCopyFileDelegate, copyWalletDelegate, lc);

			}, lockContext);
		}

		public Task SetConsolePassphraseHandlers(LockContext lockContext) {
			return this.ScheduleWrite((t, lc) => {
				return this.walletProvider.SetConsolePassphraseHandlers(lc);

			}, lockContext);
		}

		public Task<(SecureString passphrase, bool keysToo)> RequestWalletPassphraseByConsole(LockContext lockContext, int maxTryCount = 10) {
			return this.ScheduleWrite((t, lc) => {
				return this.walletProvider.RequestWalletPassphraseByConsole(lc, maxTryCount);
			}, lockContext);
		}

		public Task<SecureString> RequestKeysPassphraseByConsole(string accountCode, string keyName, LockContext lockContext, int maxTryCount = 10) {
			return this.ScheduleWrite((t, lc) => {
				return this.walletProvider.RequestKeysPassphraseByConsole(accountCode, keyName, lc, maxTryCount);
			}, lockContext);
		}

		public Task<(SecureString passphrase, bool keysToo)> RequestPassphraseByConsole(LockContext lockContext, string passphraseType = "wallet", int maxTryCount = 10) {
			return this.ScheduleWrite((t, lc) => {
				return this.walletProvider.RequestPassphraseByConsole(lc, passphraseType, maxTryCount);
			}, lockContext);
		}

		public Task<SafeArrayHandle> PerformCryptographicSignature(string accountCode, string keyName, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleWrite((t, lc) => this.walletProvider.PerformCryptographicSignature(accountCode, keyName, message, lc, allowPassKeyLimit), lockContext);
		}

		public Task<SafeArrayHandle> PerformCryptographicSignature(IWalletKey key, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleWrite((t, lc) => this.walletProvider.PerformCryptographicSignature(key, message, lc, allowPassKeyLimit), lockContext);
		}

		public Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssmtCryptographicSignature(IXmssMTWalletKey xmssMTWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.walletProvider.PerformXmssmtCryptographicSignature(xmssMTWalletKey, message, lockContext, allowPassKeyLimit);
		}

		public Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssCryptographicSignature(IXmssWalletKey keyxmssWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null, SafeArrayHandle extraNodeCache = null, Action<XMSSProvider> callback = null, Func<int, int ,int, Task> progressCallback = null) {
			return this.walletProvider.PerformXmssCryptographicSignature(keyxmssWalletKey, message, lockContext, allowPassKeyLimit, buildOptimizedSignature, xmssSignaturePathCache, extraNodeCache, callback, progressCallback);

		}
		

		public Task<IWalletStandardAccountSnapshot> GetStandardAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => p.GetStandardAccountSnapshot(accountId, lc), lockContext);
		}

		public Task<IWalletJointAccountSnapshot> GetJointAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => p.GetJointAccountSnapshot(accountId, lc), lockContext);
		}

		public Task<(string path, string passphrase, string salt, string nonce, int iterations)> BackupWallet(WalletProvider.BackupTypes backupType, LockContext lockContext) {

			return this.ScheduleTransaction((p, ct, lc) => p.BackupWallet(backupType, lc), lockContext, 60 * 5, lc => {
				// load wallet & key
				return this.walletProvider.EnsureWalletFileIsPresent(lc);

			});
		}

		public Task<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, string nonce, int iterations, LockContext lockContext) {
			return this.ScheduleWrite((t, lc) => this.walletProvider.RestoreWalletFromBackup(backupsPath, passphrase, salt, nonce, iterations, lockContext), lockContext, 60 * 5);
		}
		
		public Task<bool> AttemptWalletRescue(LockContext lockContext) {
			return this.ScheduleWrite((t, lc) => this.walletProvider.AttemptWalletRescue(lc), lockContext, 60 * 5);
		}

		public Task UpdateWalletChainStateSyncStatus(string accountCode, long BlockId, WalletAccountChainState.BlockSyncStatuses blockSyncStatus, LockContext lockContext) {
			return this.ScheduleWrite((t, lc) => {

				return this.walletProvider.UpdateWalletChainStateSyncStatus(accountCode, BlockId, blockSyncStatus, lc);
			}, lockContext);
		}

		public Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleTransaction((p, ct, lc) => p.SignTransaction(transactionHash, keyName, lc, allowPassKeyLimit), lockContext, 20, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return Task.CompletedTask;
			});
		}

		public Task<SafeArrayHandle> SignTransactionXmss(SafeArrayHandle transactionHash, IXmssWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleTransaction((p, ct, lc) => p.SignTransactionXmss(transactionHash, key, lc, allowPassKeyLimit), lockContext, 20, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return this.walletProvider.EnsureWalletKeyIsReady(key.AccountCode, key.Name, lc);
			});
		}

		public Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleTransaction((p, ct, lc) => p.SignTransaction(transactionHash, key, lc, allowPassKeyLimit), lockContext, 20, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return this.walletProvider.EnsureWalletKeyIsReady(key.AccountCode, key.Name, lc);

			});
		}

		public Task<SafeArrayHandle> SignMessageXmss(string accountCode, SafeArrayHandle message, LockContext lockContext) {

			return this.ScheduleTransaction((p, ct, lc) => p.SignMessageXmss(accountCode, message, lc), lockContext, 20, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return this.walletProvider.EnsureWalletKeyIsReady(accountCode, GlobalsService.MESSAGE_KEY_NAME, lc);
			});
		}

		public Task<SafeArrayHandle> SignMessageXmss(SafeArrayHandle messageHash, IXmssWalletKey key, LockContext lockContext) {
			return this.ScheduleTransaction((p, ct, lc) => p.SignMessageXmss(messageHash, key, lc), lockContext, 20, lc => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return this.walletProvider.EnsureWalletKeyIsReady(key.AccountCode, key.Name, lc);

			});
		}
		

		public Task EnsureWalletKeyIsReady(string accountCode, string keyname, LockContext lockContext) {
			return this.ScheduleKeyedRead((t, lc) => {

				return this.walletProvider.EnsureWalletKeyIsReady(accountCode, keyname, lc);

			}, lockContext);
		}

		public Task EnsureWalletKeyIsReady(string accountCode, byte ordinal, LockContext lockContext) {
			return this.ScheduleKeyedRead((t, lc) => {

				return this.walletProvider.EnsureWalletKeyIsReady(accountCode, ordinal, lc);
			}, lockContext);
		}

		public Task<bool> LoadWallet(CorrelationContext correlationContext, LockContext lockContext, string passphrase = null) {

			return this.ScheduleKeyedWrite((t, lc) => this.walletProvider.LoadWallet(correlationContext, lc, passphrase), lockContext, async lc => {
				// load wallet & key
				await this.walletProvider.EnsureWalletFileIsPresent(lc).ConfigureAwait(false);
				await this.walletProvider.EnsureWalletPassphrase(lc, passphrase).ConfigureAwait(false);

			}, lc => {
				// we failed
				return this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletLoadingErrorEvent(), correlationContext);

			});
		}

		public Task Pause() {
			return this.walletProvider.Pause();
		}

		public Task Resume() {
			return this.walletProvider.Resume();
		}

		public bool TransactionInProgress(LockContext lockContext) {
			return this.walletProvider.TransactionInProgress(lockContext);
		}

		public IWalletProvider UnderlyingWalletProvider => this.walletProvider;

		public Task SetWalletPassphrase(string passphrase, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => {
				p.SetWalletPassphrase(passphrase, lc);

				return Task.CompletedTask;
			}, lockContext);
		}

		public Task SetWalletPassphrase(SecureString passphrase, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => {
				p.SetWalletPassphrase(passphrase, lc);

				return Task.CompletedTask;
			}, lockContext);
		}

		public Task SetKeysPassphrase(string accountCode, string keyname, string passphrase, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => {
				p.SetKeysPassphrase(accountCode, keyname, passphrase, lc);

				return Task.CompletedTask;
			}, lockContext);
		}

		public Task SetKeysPassphrase(string accountCode, string keyname, SecureString passphrase, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => {
				p.SetKeysPassphrase(accountCode, keyname, passphrase, lc);

				return Task.CompletedTask;
			}, lockContext);
		}

	#region wrappers

		public Task<(K result, bool completed)> ScheduleReadNoWait<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext) {
			return this.RecursiveResourceAccessScheduler.ScheduleReadSucceededNoWait(action, lockContext);
		}

		public Task<K> ScheduleRead<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, int timeout = 60) {

			return this.RecursiveResourceAccessScheduler.ScheduleRead(action, lockContext, timeout);
		}

		public Task<K> ScheduleRead<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, int timeout = 60) {

			return this.RecursiveResourceAccessScheduler.ScheduleRead((p, lc) => {
				K result = action(p, lc);

				return Task.FromResult(result);
			}, lockContext, timeout);
		}

		public Task ScheduleRead(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, int timeout = 60) {

			return this.RecursiveResourceAccessScheduler.ScheduleRead(async (p, lc) => {
				await action(p, lc).ConfigureAwait(false);

				return true;
			}, lockContext, timeout);
		}

		public Task<K> ScheduleWrite<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, int timeout = 60) {

			return this.RecursiveResourceAccessScheduler.ScheduleWrite(action, lockContext, timeout);
		}

		public Task<K> ScheduleWrite<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, int timeout = 60) {

			return this.RecursiveResourceAccessScheduler.ScheduleWrite((p, lc) => {
				K result = action(p, lc);

				return Task.FromResult(result);
			}, lockContext, timeout);
		}

		public Task ScheduleWrite(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, int timeout = 60) {

			return this.ScheduleWrite(async (p, lc) => {

				await action(p, lc).ConfigureAwait(false);

				return true;
			}, lockContext, timeout);
		}

		private async Task<K> KeyedAction<K>(Func<LockContext, Task<K>> action, LockContext lockContext, Func<LockContext, Task> preloadKeys = null, Func<LockContext, Task> failed = null) {

			ITaskStasher taskStasher = TaskContextRegistry.Instance.GetTaskRoutingTaskRoutingContext();
			CorrelationContext correlationContext = TaskContextRegistry.Instance.GetTaskRoutingCorrelationContext();

			BlockchainEventException walletEventException = null;

			bool initialized = false;

			int attempt = 0;
			int exceptionsCount = 0;

			void SetException(BlockchainEventException exception) {
				walletEventException = exception;
				exceptionsCount++;
			}

			do {

				if((attempt > 3) || (exceptionsCount > 3)) {

					if(failed != null) {
						await failed(lockContext).ConfigureAwait(false);
					}

					if(walletEventException != null) {
						throw walletEventException;
					}

					throw new ApplicationException("Failed keyed operation");
				}

				if(walletEventException != null) {
					// ok, we need to perform some event here. lets do it
					taskStasher?.Stash(lockContext);

					if(walletEventException is WalletFileMissingException walletFileMissingException) {
						await this.walletProvider.RequestCopyWallet(correlationContext, attempt, lockContext).ConfigureAwait(false);
					}

					if(walletEventException is WalletPassphraseMissingException walletPassphraseMissingException) {
						await this.walletProvider.CaptureWalletPassphrase(correlationContext, attempt, lockContext).ConfigureAwait(false);
					}

					if(walletEventException is WalletDecryptionException walletDecryptionException) {
						// decryption failed, lets reset the passphrase
						this.walletProvider.ClearWalletPassphrase();
						await this.walletProvider.CaptureWalletPassphrase(correlationContext, attempt, lockContext).ConfigureAwait(false);
					}

					if(walletEventException is KeyFileMissingException keyFileMissingException) {
						await this.walletProvider.RequestCopyKeyFile(correlationContext, keyFileMissingException.AccountCode, keyFileMissingException.KeyName, attempt, lockContext).ConfigureAwait(false);
					}

					if(walletEventException is KeyPassphraseMissingException keyPassphraseMissingException) {
						await this.walletProvider.CaptureKeyPassphrase(correlationContext, keyPassphraseMissingException.AccountCode, keyPassphraseMissingException.KeyName, attempt, lockContext).ConfigureAwait(false);
					}

					if(walletEventException is KeyDecryptionException keyDecryptionException) {
						// decryption failed, lets reset the passphrase
						this.walletProvider.ClearWalletKeyPassphrase(keyDecryptionException.AccountCode, keyDecryptionException.KeyName, lockContext);
						await this.walletProvider.CaptureKeyPassphrase(correlationContext, keyDecryptionException.AccountCode, keyDecryptionException.KeyName, attempt, lockContext).ConfigureAwait(false);
					}

					taskStasher?.CompleteStash();
				}

				walletEventException = null;

				try {
					if(!initialized) {
						if(preloadKeys != null) {
							await preloadKeys(lockContext).ConfigureAwait(false);
						}

						initialized = true;
					}

					attempt++;

					return await action(lockContext).ConfigureAwait(false);

				} catch(WalletFileMissingException ex) {
					SetException(ex);
				} catch(WalletPassphraseMissingException ex) {

					SetException(ex);
				} catch(WalletDecryptionException ex) {
					SetException(ex);
				} catch(KeyFileMissingException ex) {

					SetException(ex);
				} catch(KeyPassphraseMissingException ex) {

					SetException(ex);
				} catch(KeyDecryptionException ex) {
					SetException(ex);
				}

			} while(true);
		}

		public Task ScheduleKeyedRead(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.KeyedAction(lc => this.ScheduleRead(async (p, lc2) => {
				await action(p, lc2).ConfigureAwait(false);

				return true;
			}, lc), lockContext, prepareAction, failure);
		}

		public Task<K> ScheduleKeyedRead<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.KeyedAction(lc => this.ScheduleRead(action, lc), lockContext, prepareAction, failure);
		}

		public Task<K> ScheduleKeyedRead<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.KeyedAction(lc => this.ScheduleRead((p, lc2) => {

				K result = action(p, lc2);

				return Task.FromResult(result);
			}, lc), lockContext, prepareAction, failure);
		}

		public Task<K> ScheduleKeyedWrite<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.ScheduleKeyedWrite((p, lc) => {
				K result = action(p, lc);

				return Task.FromResult(result);
			}, lockContext, prepareAction, failure);
		}

		public Task<K> ScheduleKeyedWrite<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.KeyedAction(lc => this.ScheduleWrite(action, lc), lockContext, prepareAction, failure);
		}

		public Task ScheduleKeyedWrite(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {
			return this.KeyedAction(lc => this.ScheduleWrite(async (p, lc2) => {
				await action(p, lc2).ConfigureAwait(false);

				return true;
			}, lc), lockContext, prepareAction, failure);
		}

		public Task ScheduleKeyedWrite(Action<IWalletProviderInternal, LockContext> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {
			return this.KeyedAction(lc => this.ScheduleWrite(async (p, lc2) => {
				action(p, lc2);

				return true;
			}, lc), lockContext, prepareAction, failure);
		}

		public Task ScheduleTransaction(Func<IWalletProvider, CancellationToken, LockContext, Task> action, LockContext lockContext, int timeout = 60, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.ScheduleTransaction(async (p, ct, lc) => {

				await action(p, ct, lc).ConfigureAwait(false);

				return true;
			}, lockContext, timeout, prepareAction, failure);
		}

		public async Task<K> ScheduleTransaction<K>(Func<IWalletProvider, CancellationToken, LockContext, Task<K>> action, LockContext lockContext, int timeout = 60, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			using(CancellationTokenSource tokenSource = new CancellationTokenSource()) {

				Task<K> PerformContent(IWalletProvider prov, LockContext lc, CancellationToken ct) {
					return this.KeyedAction(lc2 => action(prov, ct, lc2), lc, prepareAction, failure);
				}

				if(this.TransactionInProgress(lockContext)) {
					// we are in a transaction, just go through
					return await PerformContent(this.walletProvider, lockContext, tokenSource.Token).ConfigureAwait(false);
				}

				// we must create a transaction first...
				try {
					return await this.ScheduleWrite((wp, lc) => {
						return wp.PerformWalletTransaction((prov, ct, lc2) => PerformContent(prov, lc2, ct), tokenSource.Token, lc, async (prov, a, ct, lc2) => {
							await a(prov, ct, lc2).ConfigureAwait(false);

							using(LockHandle handle = await this.transactionalLocker.LockAsync(lc).ConfigureAwait(false)) {
								if(this.transactionalSuccessActions.Any()) {
									await IndependentActionRunner.RunAsync(handle, this.transactionalSuccessActions.ToArray()).ConfigureAwait(false);
								}
							}
						}, (prov, b, ct, lc2) => b(prov, ct, lc2));

					}, lockContext, timeout).ConfigureAwait(false);

				} finally {
					using(await this.transactionalLocker.LockAsync(lockContext).ConfigureAwait(false)) {
						this.transactionalSuccessActions.Clear();
					}
				}
			}
		}

		private readonly RecursiveAsyncLock transactionalLocker = new RecursiveAsyncLock();
		private readonly ConcurrentBag<Func<LockContext, Task>> transactionalSuccessActions = new ConcurrentBag<Func<LockContext, Task>>();

		/// <summary>
		///     add a list of events to execute only when the current transactions successfully completes. if not in a transaction,
		///     it will execute right away
		/// </summary>
		/// <param name="actions"></param>
		public async Task AddTransactionSuccessActions(List<Func<LockContext, Task>> transactionalSuccessActions, LockContext lockContext) {

			if(this.TransactionInProgress(lockContext)) {
				using(await this.transactionalLocker.LockAsync(lockContext).ConfigureAwait(false)) {
					foreach(Func<LockContext, Task> entry in transactionalSuccessActions) {
						this.transactionalSuccessActions.Add(entry);
					}
				}
			} else {
				// the wallet trnasaction is a success. lets run the confirmation events
				await IndependentActionRunner.RunAsync(lockContext, transactionalSuccessActions.ToArray()).ConfigureAwait(false);
			}
		}

		public bool IsActiveTransactionThread(int threadId) {

			//TODO: this is from a different time and may not be useful anymore. either remove or refactor.
			return false;

			//return this.RecursiveResourceAccessScheduler.ThreadLockInProgress && this.RecursiveResourceAccessScheduler.IsActiveTransactionThread(threadId);
		}

		//TODO: this is from a different time and may not be useful anymore. either remove or refactor.
		public bool IsActiveTransaction => true;

	#endregion

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.walletProvider.Dispose();
				this.RecursiveResourceAccessScheduler.Dispose();
			}

			this.IsDisposed = true;
		}

		~WalletProviderProxy() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}