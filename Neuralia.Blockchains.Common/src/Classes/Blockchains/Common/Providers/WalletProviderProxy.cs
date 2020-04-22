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
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;
using Nito.AsyncEx.Synchronous;
using Org.BouncyCastle.Utilities;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IWalletProviderProxyTransactions : IChainProvider {

		bool IsActiveTransaction { get; }
		bool IsActiveTransactionThread(int threadId);

		Task<(K result, bool completed)> ScheduleReadNoWait<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext);
		Task<K>                          ScheduleRead<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, int timeout = 60);
		Task<K>                          ScheduleRead<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, int timeout = 60);
		Task                             ScheduleRead(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, int timeout = 60);
		Task<K>                          ScheduleWrite<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, int timeout = 60);
		Task<K>                          ScheduleWrite<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, int timeout = 60);
		Task                             ScheduleWrite(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, int timeout = 60);
		Task                             ScheduleKeyedRead(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K>                          ScheduleKeyedRead<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K>                          ScheduleKeyedRead<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K>                          ScheduleKeyedWrite<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K>                          ScheduleKeyedWrite<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task                             ScheduleKeyedWrite(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task                             ScheduleKeyedWrite(Action<IWalletProviderInternal, LockContext> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task<K>                          ScheduleTransaction<K>(Func<IWalletProvider, CancellationToken, LockContext, Task<K>> action, LockContext lockContext, int timeout = 60, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);
		Task                             ScheduleTransaction(Func<IWalletProvider, CancellationToken, LockContext, Task> action, LockContext lockContext, int timeout = 60, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null);

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
		protected readonly RecursiveResourceAccessScheduler<IWalletProviderInternal> RecursiveResourceAccessScheduler;

		protected readonly IWalletProviderInternal walletProvider;

		protected readonly        CENTRAL_COORDINATOR centralCoordinator;
		protected static readonly TimeSpan            defaultTransactionTimeout = TimeSpan.FromMinutes(1);

		public WalletProviderProxy(CENTRAL_COORDINATOR centralCoordinator, IWalletProvider walletProvider) {
			this.walletProvider = (IWalletProviderInternal) walletProvider;

			this.centralCoordinator               = centralCoordinator;
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

		public bool       IsWalletLoaded                             => this.walletProvider.IsWalletLoaded;
		public Task<bool> IsWalletEncrypted(LockContext lockContext) => this.ScheduleRead((p, lc) => p.IsWalletEncrypted(lc), lockContext);

		public Task<bool> IsWalletAccountLoaded(LockContext lockContext) => this.ScheduleRead((p, lc) => p.IsWalletAccountLoaded(lc), lockContext);
		public Task<bool> WalletFileExists(LockContext lockContext)      => this.ScheduleRead((p, lc) => p.WalletFileExists(lc), lockContext);

		public void EnsureWalletIsLoaded() {
			this.walletProvider.EnsureWalletIsLoaded();
		}

		public Task RemovePIDLock() {
			return this.walletProvider.RemovePIDLock();
		}

		public Task<long?> LowestAccountBlockSyncHeight(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.LowestAccountBlockSyncHeight(lc), lockContext);
		}

		public Task<bool?> Synced(LockContext lockContext) {
			return this.ScheduleRead((p, lc) => p.Synced(lc), lockContext);
		}

		public async Task<bool?> SyncedNoWait(LockContext lockContext) {

			(bool? result, bool completed) = await this.ScheduleReadNoWait((p, lc) => p.Synced(lc), lockContext).ConfigureAwait(false);

			return completed ? (bool?) result : null;

		}

		public Task<bool> WalletContainsAccount(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.WalletContainsAccount(accountUuid, lc), lockContext);
		}

		public Task<List<IWalletAccount>> GetWalletSyncableAccounts(long blockId, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletSyncableAccounts(blockId, lc), lockContext);
		}

		public Task<IAccountFileInfo> GetAccountFileInfo(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAccountFileInfo(accountUuid, lc), lockContext);
		}

		public Task<List<IWalletAccount>> GetAccounts(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAccounts(lc), lockContext);
		}

		public Task<List<IWalletAccount>> GetAllAccounts(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAllAccounts(lc), lockContext);
		}

		public Task<Guid> GetAccountUuid(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAccountUuid(lc), lockContext);
		}

		public Task<AccountId> GetPublicAccountId(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetPublicAccountId(lc), lockContext);
		}

		public Task<AccountId> GetPublicAccountId(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetPublicAccountId(accountUuid, lc), lockContext);
		}

		public Task<AccountId> GetAccountUuidHash(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAccountUuidHash(lc), lockContext);
		}

		public Task<bool> IsDefaultAccountPublished(LockContext lockContext) => this.ScheduleRead((p, lc) => p.IsDefaultAccountPublished(lc), lockContext);

		public Task<bool> IsAccountPublished(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.IsAccountPublished(accountUuid, lc), lockContext);
		}

		public Task<IWalletAccount> GetActiveAccount(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetActiveAccount(lc), lockContext);
		}

		public Task<IWalletAccount> GetWalletAccount(Guid id, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletAccount(id, lc), lockContext);
		}

		public Task<IWalletAccount> GetWalletAccount(string name, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletAccount(name, lc), lockContext);
		}

		public Task<IWalletAccount> GetWalletAccount(AccountId accountId, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletAccount(accountId, lc), lockContext);
		}

		public Task<Dictionary<AccountId, int>> ClearTimedOutTransactions(LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.ClearTimedOutTransactions(lc), lockContext);
		}

		public Task<bool> ResetTimedOutWalletEntries(LockContext lockContext, List<(Guid accountUuid, string name)> forcedKeys = null) {
			return this.ScheduleKeyedWrite((p, lc) => p.ResetTimedOutWalletEntries(lc, forcedKeys), lockContext);
		}

		public Task<bool> ResetAllTimedOut(LockContext lockContext, List<(Guid accountUuid, string name)> forcedKeys = null) {
			return this.ScheduleKeyedWrite((p, lc) => p.ResetAllTimedOut(lc, forcedKeys), lockContext);
		}

		public Task<List<WalletTransactionHistoryHeaderAPI>> APIQueryWalletTransactionHistory(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletTransactionHistory(accountUuid, lc), lockContext);
		}

		public Task<WalletTransactionHistoryDetailsAPI> APIQueryWalletTransactionHistoryDetails(Guid accountUuid, string transactionId, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletTransactionHistoryDetails(accountUuid, transactionId, lc), lockContext);
		}

		public Task<WalletInfoAPI> APIQueryWalletInfoAPI(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletInfoAPI(lc), lockContext);
		}

		public Task<List<WalletAccountAPI>> APIQueryWalletAccounts(LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletAccounts(lc), lockContext);
		}

		public Task<WalletAccountDetailsAPI> APIQueryWalletAccountDetails(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletAccountDetails(accountUuid, lc), lockContext);
		}

		public Task<TransactionId> APIQueryWalletAccountPresentationTransactionId(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.APIQueryWalletAccountPresentationTransactionId(accountUuid, lc), lockContext);
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

		public Task<IXmssWalletKey> CreateXmssKey(string name, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null) {
			return this.walletProvider.CreateXmssKey(name, warningLevel, changeLevel, progressCallback);
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, int treeHeight, int hashBits, WalletProvider.HashTypes HashType, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null) {
			return this.walletProvider.CreateXmssKey(name, treeHeight, hashBits, HashType, warningLevel, changeLevel, progressCallback);
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, Func<int, Task> progressCallback = null) {
			return this.walletProvider.CreateXmssKey(name, progressCallback);
		}

		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, float warningLevel, float changeLevel, Func<int, int, int, Task> progressCallback = null) {
			return this.walletProvider.CreateXmssmtKey(name, warningLevel, changeLevel, progressCallback);
		}

		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, int treeHeight, int treeLayers, Enums.KeyHashBits hashBits, float warningLevel, float changeLevel, Func<int, int, int, Task> progressCallback = null) {
			return this.walletProvider.CreateXmssmtKey(name, treeHeight, treeLayers, hashBits, warningLevel, changeLevel, progressCallback);
		}

		public IQTeslaWalletKey CreatePresentationQTeslaKey(string name) {
			return this.walletProvider.CreatePresentationQTeslaKey(name);
		}

		public IQTeslaWalletKey CreateQTeslaKey(string name, QTESLASecurityCategory.SecurityCategories securityCategory) {
			return this.walletProvider.CreateQTeslaKey(name, securityCategory);
		}

		public void PrepareQTeslaKey<T>(T key, QTESLASecurityCategory.SecurityCategories securityCategory)
			where T : IQTeslaWalletKey {
			this.walletProvider.PrepareQTeslaKey(key, securityCategory);
		}

		public ISecretWalletKey CreateSecretKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null) {
			return this.walletProvider.CreateSecretKey(name, securityCategorySecret, previousKey);
		}

		public ISecretComboWalletKey CreateSecretComboKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null) {
			return this.walletProvider.CreateSecretComboKey(name, securityCategorySecret, previousKey);
		}

		public ISecretDoubleWalletKey CreateSecretDoubleKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, QTESLASecurityCategory.SecurityCategories securityCategorySecond, ISecretDoubleWalletKey previousKey = null) {
			return this.walletProvider.CreateSecretDoubleKey(name, securityCategorySecret, securityCategorySecond, previousKey);
		}

		public ISecretWalletKey CreateSuperKey() {
			return this.walletProvider.CreateSuperKey();
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

		public Task<IWalletAccountSnapshot> GetWalletFileInfoAccountSnapshot(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetWalletFileInfoAccountSnapshot(accountUuid, lc), lockContext);
		}

		public Task<IWalletAccountSnapshot> GetAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.GetAccountSnapshot(accountId, lc), lockContext);
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

		public event Delegates.RequestCopyWalletFileDelegate CopyWalletRequest;
		public event Delegates.RequestPassphraseDelegate     WalletPassphraseRequest;
		public event Delegates.RequestKeyPassphraseDelegate  WalletKeyPassphraseRequest;
		public event Delegates.RequestCopyKeyFileDelegate    WalletCopyKeyFileRequest;

		public Task CreateNewEmptyWallet(CorrelationContext correlationContext, bool encryptWallet, string passphrase, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, LockContext lockContext) {
			return this.ScheduleWrite((t, lc) => {
				return this.walletProvider.CreateNewEmptyWallet(correlationContext, encryptWallet, passphrase, walletCreationStepSet, lc);
			}, lockContext);
		}

		public Task<bool> AllAccountsHaveSyncStatus(SynthesizedBlock block, WalletAccountChainState.BlockSyncStatuses status, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.AllAccountsHaveSyncStatus(block, status, lc);
			}, lockContext);
		}

		public Task<bool> AllAccountsUpdatedWalletBlock(SynthesizedBlock block, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.AllAccountsUpdatedWalletBlock(block, lc);
			}, lockContext);
		}

		public Task<bool> AllAccountsUpdatedWalletBlock(SynthesizedBlock block, long previousBlockId, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.AllAccountsUpdatedWalletBlock(block, previousBlockId, lc);
			}, lockContext);
		}

		public Task UpdateWalletBlock(SynthesizedBlock synthesizedBlock, long previousSyncedBlockId, Func<SynthesizedBlock, LockContext, Task> callback, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.UpdateWalletBlock(synthesizedBlock, previousSyncedBlockId, callback, lc), lockContext);
		}

		public Task<bool> AllAccountsWalletKeyLogSet(SynthesizedBlock block, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.AllAccountsWalletKeyLogSet(block, lc);
			}, lockContext);
		}

		public Task<bool> SetActiveAccount(string name, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.SetActiveAccount(name, lc);
			}, lockContext);
		}

		public Task<bool> SetActiveAccount(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.SetActiveAccount(accountUuid, lc);
			}, lockContext);
		}

		public Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null) {
			// this is a special case where we dont have a wallet, so no need to schedule anything. we will let the create make its own transactions
			return this.walletProvider.CreateNewCompleteWallet(correlationContext, accountName, encryptWallet, encryptKey, encryptKeysIndividually, passphrases, lockContext, accountCreatedCallback);
		}

		public Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null) {
			// this is a special case where we dont have a wallet, so no need to schedule anything. we will let the create make its own transactions

			return this.walletProvider.CreateNewCompleteWallet(correlationContext, encryptWallet, encryptKey, encryptKeysIndividually, passphrases, lockContext, accountCreatedCallback);

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

		public Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateWalletSnapshot(accountSnapshot, accountUuid, lc);
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

		public Task<IWalletAccount> CreateNewAccount(string name, bool encryptKeys, bool encryptKeysIndividually, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, LockContext lockContext, bool setactive = false) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.CreateNewAccount(name, encryptKeys, encryptKeysIndividually, correlationContext, walletCreationStepSet, accountCreationStepSet, lc, setactive);
			}, lockContext);
		}

		public Task<bool> CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, Action<IWalletAccount> accountCreatedCallback = null) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.CreateNewCompleteAccount(correlationContext, accountName, encryptKeys, encryptKeysIndividually, passphrases, lc, walletCreationStepSet, accountCreatedCallback);
			}, lockContext);
		}

		public Task<bool> CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.CreateNewCompleteAccount(correlationContext, accountName, encryptKeys, encryptKeysIndividually, passphrases, lc);
			}, lockContext);
		}

		public Task InsertKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, KeyUseIndexSet keyUseIndexSet, byte keyOrdinalId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertKeyLogTransactionEntry(account, transactionId, keyUseIndexSet, keyOrdinalId, lc);
			}, lockContext);
		}

		public Task InsertKeyLogBlockEntry(IWalletAccount account, BlockId blockId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertKeyLogBlockEntry(account, blockId, keyOrdinalId, keyUseIndex, lc);
			}, lockContext);
		}

		public Task InsertKeyLogDigestEntry(IWalletAccount account, int digestId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertKeyLogDigestEntry(account, digestId, keyOrdinalId, keyUseIndex, lc);
			}, lockContext);
		}

		public Task InsertKeyLogEntry(IWalletAccount account, string eventId, Enums.BlockchainEventTypes eventType, byte keyOrdinalId, KeyUseIndexSet keyUseIndex, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertKeyLogEntry(account, eventId, eventType, keyOrdinalId, keyUseIndex, lc);
			}, lockContext);
		}

		public Task ConfirmKeyLogBlockEntry(IWalletAccount account, BlockId blockId, long confirmationBlockId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.ConfirmKeyLogBlockEntry(account, blockId, confirmationBlockId, lc);
			}, lockContext);
		}

		public Task ConfirmKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, KeyUseIndexSet keyUseIndexSet, long confirmationBlockId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.ConfirmKeyLogTransactionEntry(account, transactionId, keyUseIndexSet, confirmationBlockId, lc);
			}, lockContext);
		}

		public Task<bool> KeyLogTransactionExists(IWalletAccount account, TransactionId transactionId, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.KeyLogTransactionExists(account, transactionId, lc);
			}, lockContext);
		}

		public IWalletKey CreateBasicKey(string name, Enums.KeyTypes keyType) {
			return this.walletProvider.CreateBasicKey(name, keyType);
		}

		public T CreateBasicKey<T>(string name, Enums.KeyTypes keyType)
			where T : IWalletKey {
			return this.walletProvider.CreateBasicKey<T>(name, keyType);
		}

		public void HashKey(IWalletKey key) {
			this.walletProvider.HashKey(key);
		}

		public Task SetChainStateHeight(Guid accountUuid, long blockId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.SetChainStateHeight(accountUuid, blockId, lc);
			}, lockContext);
		}

		public Task SetChainStateHeight(IWalletAccountChainState chainState, long blockId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.SetChainStateHeight(chainState, blockId, lc);
			}, lockContext);
		}

		public Task<long> GetChainStateHeight(Guid accountUuid, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetChainStateHeight(accountUuid, lc);
			}, lockContext);
		}

		public Task<KeyUseIndexSet> GetChainStateLastSyncedKeyHeight(IWalletKey key, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetChainStateLastSyncedKeyHeight(key, lc);
			}, lockContext);
		}

		public Task UpdateLocalChainStateKeyHeight(IWalletKey key, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateLocalChainStateKeyHeight(key, lc);
			}, lockContext);
		}

		public Task<IWalletElectionsHistory> InsertElectionsHistoryEntry(SynthesizedBlock.SynthesizedElectionResult electionResult, AccountId electedAccountId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertElectionsHistoryEntry(electionResult, electedAccountId, lc);
			}, lockContext);
		}

		public Task InsertLocalTransactionCacheEntry(ITransactionEnvelope transactionEnvelope, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertLocalTransactionCacheEntry(transactionEnvelope, lc);
			}, lockContext);
		}

		public Task<List<IWalletTransactionHistory>> InsertTransactionHistoryEntry(ITransaction transaction, string note, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.InsertTransactionHistoryEntry(transaction, note, lc);
			}, lockContext);
		}

		public Task UpdateLocalTransactionCacheEntry(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateLocalTransactionCacheEntry(transactionId, status, gossipMessageHash, lc);
			}, lockContext);
		}

		public Task<IWalletTransactionHistoryFileInfo> UpdateLocalTransactionHistoryEntry(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.UpdateLocalTransactionHistoryEntry(transactionId, status, lc);
			}, lockContext);
		}

		public Task<IWalletTransactionCache> GetLocalTransactionCacheEntry(TransactionId transactionId, LockContext lockContext) {
			return this.ScheduleKeyedRead((prov, lc) => {
				return this.walletProvider.GetLocalTransactionCacheEntry(transactionId, lc);
			}, lockContext);
		}

		public Task RemoveLocalTransactionCacheEntry(TransactionId transactionId, LockContext lockContext) {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.RemoveLocalTransactionCacheEntry(transactionId, lc);
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

		public Task AddAccountKey<KEY>(Guid accountUuid, KEY key, ImmutableDictionary<int, string> passphrases, LockContext lockContext, KEY nextKey = null)
			where KEY : class, IWalletKey {
			return this.ScheduleKeyedWrite((prov, lc) => {
				return this.walletProvider.AddAccountKey(accountUuid, key, passphrases, lc, nextKey);
			}, lockContext);
		}

		public Task SetNextKey(Guid accountUuid, IWalletKey nextKey, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.SetNextKey(accountUuid, nextKey, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, nextKey.Name, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, nextKey.Name, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task UpdateNextKey(IWalletKey nextKey, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.UpdateNextKey(nextKey, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(nextKey.AccountUuid, nextKey.Name, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(nextKey.AccountUuid, nextKey.Name, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task CreateNextXmssKey(Guid accountUuid, string keyName, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNextXmssKey(accountUuid, keyName, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task CreateNextXmssKey(Guid accountUuid, byte ordinal, LockContext lockContext) {
			return this.ScheduleKeyedWrite((p, lc) => p.CreateNextXmssKey(accountUuid, ordinal, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, ordinal, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<bool> IsKeyEncrypted(Guid accountUuid, LockContext lockContext) {

			return this.ScheduleKeyedRead((p, lc) => p.IsKeyEncrypted(accountUuid, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return Task.CompletedTask;
			});
		}

		public Task<bool> IsNextKeySet(Guid accountUuid, string keyName, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.IsNextKeySet(accountUuid, keyName, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadNextKey<T>(Guid AccountUuid, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			return this.ScheduleKeyedRead((p, lc) => p.LoadNextKey<T>(AccountUuid, keyName, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(AccountUuid, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(AccountUuid, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<IWalletKey> LoadNextKey(Guid AccountUuid, string keyName, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadNextKey(AccountUuid, keyName, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(AccountUuid, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(AccountUuid, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<IWalletKey> LoadKey(Guid AccountUuid, string keyName, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(AccountUuid, keyName, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(AccountUuid, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(AccountUuid, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<IWalletKey> LoadKey(Guid AccountUuid, byte ordinal, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(AccountUuid, ordinal, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(AccountUuid, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(AccountUuid, ordinal, 1, lc);

				return Task.CompletedTask;
			});

		}

		public Task<IWalletKey> LoadKey(string keyName, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(keyName, lc), lockContext, async (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent((await this.GetAccountUuid(lockContext).ConfigureAwait(false)), keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase((await this.GetAccountUuid(lockContext).ConfigureAwait(false)), keyName, 1, lc);
			});
		}

		public Task<IWalletKey> LoadKey(byte ordinal, LockContext lockContext) {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(ordinal, lc), lockContext, async (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent((await this.GetAccountUuid(lockContext).ConfigureAwait(false)), ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase((await this.GetAccountUuid(lockContext).ConfigureAwait(false)), ordinal, 1, lc);
			});
		}

		public Task<T> LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string keyName, LockContext lockContext)
			where K : class, IWalletKey
			where T : class {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(selector, accountUuid, keyName, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, byte ordinal, LockContext lockContext)
			where K : class, IWalletKey
			where T : class {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey(selector, accountUuid, ordinal, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, ordinal, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(Func<T, T> selector, Guid accountUuid, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(selector, accountUuid, keyName, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(Func<T, T> selector, Guid accountUuid, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(selector, accountUuid, ordinal, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, ordinal, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(Guid accountUuid, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(accountUuid, keyName, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(Guid accountUuid, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {

			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(accountUuid, ordinal, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, ordinal, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task<T> LoadKey<T>(string keyName, LockContext lockContext)
			where T : class, IWalletKey {

			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(keyName, lc), lockContext, async (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent((await this.GetAccountUuid(lockContext).ConfigureAwait(false)), keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase((await this.GetAccountUuid(lockContext).ConfigureAwait(false)), keyName, 1, lc);
			});
		}

		public Task<T> LoadKey<T>(byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {

			return this.ScheduleKeyedRead((p, lc) => p.LoadKey<T>(ordinal, lc), lockContext, async (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent((await this.GetAccountUuid(lockContext).ConfigureAwait(false)), ordinal, 1, lc);
				this.walletProvider.EnsureKeyPassphrase((await this.GetAccountUuid(lockContext).ConfigureAwait(false)), ordinal, 1, lc);
			});
		}

		public Task UpdateKey(IWalletKey key, LockContext lockContext) {

			return this.ScheduleKeyedWrite((p, lc) => p.UpdateKey(key, lc), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(key.AccountUuid, key.Name, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(key.AccountUuid, key.Name, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task SwapNextKey(IWalletKey key, LockContext lockContext, bool storeHistory = true) {

			return this.ScheduleKeyedWrite((p, lc) => p.SwapNextKey(key, lc, storeHistory), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(key.AccountUuid, key.Name, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(key.AccountUuid, key.Name, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task SwapNextKey(Guid accountUUid, string keyName, LockContext lockContext, bool storeHistory = true) {
			return this.ScheduleKeyedWrite((p, lc) => p.SwapNextKey(accountUUid, keyName, lc, storeHistory), lockContext, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUUid, keyName, 1, lc);
				this.walletProvider.EnsureKeyPassphrase(accountUUid, keyName, 1, lc);

				return Task.CompletedTask;
			});
		}

		public Task EnsureWalletLoaded(LockContext lockContext) {

			return this.ScheduleKeyedWrite((p, lc) => p.EnsureWalletLoaded(lc), lockContext, async (lc) => {
				// load wallet & key
				await walletProvider.EnsureWalletFileIsPresent(lc).ConfigureAwait(false);
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

		public Task<SecureString> RequestKeysPassphraseByConsole(Guid accountUUid, string keyName, LockContext lockContext, int maxTryCount = 10) {
			return this.ScheduleWrite((t, lc) => {
				return this.walletProvider.RequestKeysPassphraseByConsole(accountUUid, keyName, lc, maxTryCount);
			}, lockContext);
		}

		public Task<(SecureString passphrase, bool keysToo)> RequestPassphraseByConsole(LockContext lockContext, string passphraseType = "wallet", int maxTryCount = 10) {
			return this.ScheduleWrite((t, lc) => {
				return this.walletProvider.RequestPassphraseByConsole(lc, passphraseType, maxTryCount);
			}, lockContext);
		}

		public Task<SafeArrayHandle> PerformCryptographicSignature(Guid accountUuid, string keyName, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleWrite((t, lc) => this.walletProvider.PerformCryptographicSignature(accountUuid, keyName, message, lc, allowPassKeyLimit), lockContext);
		}

		public Task<SafeArrayHandle> PerformCryptographicSignature(IWalletKey key, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleWrite((t, lc) => this.walletProvider.PerformCryptographicSignature(key, message, lc, allowPassKeyLimit), lockContext);
		}

		public Task<IWalletStandardAccountSnapshot> GetStandardAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => p.GetStandardAccountSnapshot(accountId, lc), lockContext);
		}

		public Task<IWalletJointAccountSnapshot> GetJointAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => p.GetJointAccountSnapshot(accountId, lc), lockContext);
		}

		public Task<(string path, string passphrase, string salt, int iterations)> BackupWallet(LockContext lockContext) {

			return this.ScheduleTransaction((t, ct, lc) => this.walletProvider.BackupWallet(lc), lockContext, 60 * 5, (lc) => {
				// load wallet & key
				return this.walletProvider.EnsureWalletFileIsPresent(lc);

			});
		}

		public Task<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, int iterations, LockContext lockContext)
		{
			return this.ScheduleWrite((t, lc) => this.walletProvider.RestoreWalletFromBackup(backupsPath, passphrase, salt, iterations, lockContext), lockContext, 60 * 5);
		}

		public Task UpdateWalletChainStateSyncStatus(Guid accountUuid, long BlockId, WalletAccountChainState.BlockSyncStatuses blockSyncStatus, LockContext lockContext) {
			return this.ScheduleWrite((t, lc) => {

				return this.walletProvider.UpdateWalletChainStateSyncStatus(accountUuid, BlockId, blockSyncStatus, lc);
			}, lockContext);
		}

		public Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleTransaction((p, ct, lc) => p.SignTransaction(transactionHash, keyName, lc, allowPassKeyLimit), lockContext, 20, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return Task.CompletedTask;
			});
		}

		public Task<SafeArrayHandle> SignTransactionXmss(SafeArrayHandle transactionHash, IXmssWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleTransaction((p, ct, lc) => p.SignTransactionXmss(transactionHash, key, lc, allowPassKeyLimit), lockContext, 20, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return this.walletProvider.EnsureWalletKeyIsReady(key.AccountUuid, key.Name, lc);
			});
		}

		public Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false) {
			return this.ScheduleTransaction((p, ct, lc) => p.SignTransaction(transactionHash, key, lc, allowPassKeyLimit), lockContext, 20, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return this.walletProvider.EnsureWalletKeyIsReady(key.AccountUuid, key.Name, lc);

			});
		}

		public Task<SafeArrayHandle> SignMessageXmss(Guid accountUuid, SafeArrayHandle message, LockContext lockContext) {

			return this.ScheduleTransaction((p, ct, lc) => p.SignMessageXmss(accountUuid, message, lc), lockContext, 20, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return this.walletProvider.EnsureWalletKeyIsReady(accountUuid, GlobalsService.MESSAGE_KEY_NAME, lc);
			});
		}

		public Task<SafeArrayHandle> SignMessageXmss(SafeArrayHandle messageHash, IXmssWalletKey key, LockContext lockContext) {
			return this.ScheduleTransaction((p, ct, lc) => p.SignMessageXmss(messageHash, key, lc), lockContext, 20, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return this.walletProvider.EnsureWalletKeyIsReady(key.AccountUuid, key.Name, lc);

			});
		}

		public Task<SafeArrayHandle> SignMessage(SafeArrayHandle messageHash, IWalletKey key, LockContext lockContext) {
			return this.ScheduleTransaction((p, ct, lc) => p.SignMessage(messageHash, key, lc), lockContext, 20, (lc) => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();

				return this.walletProvider.EnsureWalletKeyIsReady(key.AccountUuid, key.Name, lc);

			});
		}

		public Task EnsureWalletKeyIsReady(Guid accountUuid, string keyname, LockContext lockContext) {
			return this.ScheduleKeyedRead((t, lc) => {

				return this.walletProvider.EnsureWalletKeyIsReady(accountUuid, keyname, lc);

			}, lockContext);
		}

		public Task EnsureWalletKeyIsReady(Guid accountUuid, byte ordinal, LockContext lockContext) {
			return this.ScheduleKeyedRead((t, lc) => {

				return this.walletProvider.EnsureWalletKeyIsReady(accountUuid, ordinal, lc);
			}, lockContext);
		}

		public Task<bool> LoadWallet(CorrelationContext correlationContext, LockContext lockContext, string passphrase = null) {

			return this.ScheduleKeyedWrite((t, lc) => this.walletProvider.LoadWallet(correlationContext, lc, passphrase), lockContext, async (lc) => {
				// load wallet & key
				await walletProvider.EnsureWalletFileIsPresent(lc).ConfigureAwait(false);
				await walletProvider.EnsureWalletPassphrase(lc, passphrase).ConfigureAwait(false);

			}, lc => {
				// we failed
				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletLoadingErrorEvent(), correlationContext);

				return Task.CompletedTask;
			});
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

		public Task SetKeysPassphrase(Guid accountUuid, string keyname, string passphrase, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => {
				p.SetKeysPassphrase(accountUuid, keyname, passphrase, lc);

				return Task.CompletedTask;
			}, lockContext);
		}

		public Task SetKeysPassphrase(Guid accountUuid, string keyname, SecureString passphrase, LockContext lockContext) {
			return this.ScheduleWrite((p, lc) => {
				p.SetKeysPassphrase(accountUuid, keyname, passphrase, lc);

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
				var result = action(p, lc);

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
				var result = action(p, lc);

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

			ITaskStasher       taskStasher        = TaskContextRegistry.Instance.GetTaskRoutingTaskRoutingContext();
			CorrelationContext correlationContext = TaskContextRegistry.Instance.GetTaskRoutingCorrelationContext();

			BlockchainEventException walletEventException = null;

			bool initialized = false;

			int attempt         = 0;
			int exceptionsCount = 0;

			void SetException(BlockchainEventException exception) {
				walletEventException = exception;
				exceptionsCount++;
			}

			do {

				if(attempt > 3 || exceptionsCount > 3) {

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
						await this.walletProvider.RequestCopyKeyFile(correlationContext, keyFileMissingException.AccountUuid, keyFileMissingException.KeyName, attempt, lockContext).ConfigureAwait(false);
					}

					if(walletEventException is KeyPassphraseMissingException keyPassphraseMissingException) {
						await this.walletProvider.CaptureKeyPassphrase(correlationContext, keyPassphraseMissingException.AccountUuid, keyPassphraseMissingException.KeyName, attempt, lockContext).ConfigureAwait(false);
					}

					if(walletEventException is KeyDecryptionException keyDecryptionException) {
						// decryption failed, lets reset the passphrase
						this.walletProvider.ClearWalletKeyPassphrase(keyDecryptionException.AccountUuid, keyDecryptionException.KeyName, lockContext);
						await this.walletProvider.CaptureKeyPassphrase(correlationContext, keyDecryptionException.AccountUuid, keyDecryptionException.KeyName, attempt, lockContext).ConfigureAwait(false);
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

			return this.KeyedAction((lc) => this.ScheduleRead(async (p, lc2) => {
				await action(p, lc2).ConfigureAwait(false);

				return true;
			}, lc), lockContext, prepareAction, failure);
		}

		public Task<K> ScheduleKeyedRead<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.KeyedAction((lc) => this.ScheduleRead(action, lc), lockContext, prepareAction, failure);
		}

		public Task<K> ScheduleKeyedRead<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.KeyedAction((lc) => this.ScheduleRead((p, lc2) => {

				var result = action(p, lc2);

				return Task.FromResult(result);
			}, lc), lockContext, prepareAction, failure);
		}

		public Task<K> ScheduleKeyedWrite<K>(Func<IWalletProviderInternal, LockContext, K> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.ScheduleKeyedWrite((p, lc) => {
				var result = action(p, lc);

				return Task.FromResult(result);
			}, lockContext, prepareAction, failure);
		}

		public Task<K> ScheduleKeyedWrite<K>(Func<IWalletProviderInternal, LockContext, Task<K>> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {

			return this.KeyedAction((lc) => this.ScheduleWrite(action, lc), lockContext, prepareAction, failure);
		}

		public Task ScheduleKeyedWrite(Func<IWalletProviderInternal, LockContext, Task> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {
			return this.KeyedAction((lc) => this.ScheduleWrite(async (p, lc2) => {
				await action(p, lc2).ConfigureAwait(false);

				return true;
			}, lc), lockContext, prepareAction, failure);
		}

		public Task ScheduleKeyedWrite(Action<IWalletProviderInternal, LockContext> action, LockContext lockContext, Func<LockContext, Task> prepareAction = null, Func<LockContext, Task> failure = null) {
			return this.KeyedAction((lc) => this.ScheduleWrite(async (p, lc2) => {
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
					return this.KeyedAction((lc2) => action(prov, ct, lc2), lc, prepareAction, failure);
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

							using(var handle = await this.transactionalLocker.LockAsync(lc).ConfigureAwait(false)) {
								if(this.transactionalSuccessActions.Any()) {
									await IndependentActionRunner.RunAsync(handle, this.transactionalSuccessActions.ToArray()).ConfigureAwait(false);
								}
							}
						}, (prov, b, ct, lc2) => b(prov, ct, lc2));

					}, lockContext, timeout).ConfigureAwait(false);

				} finally {
					using(this.transactionalLocker.Lock(lockContext)) {
						this.transactionalSuccessActions.Clear();
					}
				}
			}
		}

		private readonly RecursiveAsyncLock                     transactionalLocker         = new RecursiveAsyncLock();
		private readonly ConcurrentBag<Func<LockContext, Task>> transactionalSuccessActions = new ConcurrentBag<Func<LockContext, Task>>();

		/// <summary>
		/// add a list of events to execute only when the current transactions successfully completes. if not in a transaction, it will execute right away
		/// </summary>
		/// <param name="actions"></param>
		public async Task AddTransactionSuccessActions(List<Func<LockContext, Task>> transactionalSuccessActions, LockContext lockContext) {

			if(this.TransactionInProgress(lockContext)) {
				using(this.transactionalLocker.Lock(lockContext)) {
					foreach(var entry in transactionalSuccessActions) {
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

		public Task Pause() {
			return this.walletProvider.Pause();
		}

		public Task Resume() {
			return this.walletProvider.Resume();
		}

		public bool TransactionInProgress(LockContext lockContext) => this.walletProvider.TransactionInProgress(lockContext);
	}
}