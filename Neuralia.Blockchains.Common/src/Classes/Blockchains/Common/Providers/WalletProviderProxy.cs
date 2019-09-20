using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IWalletProviderProxyTransactions {
		bool IsActiveTransaction { get; }

		K ScheduleTransaction<K>(Func<CancellationToken, K> action, int timeout = 60, Action prepareAction = null);
		K ScheduleTransaction<K>(Func<IWalletProvider, CancellationToken, K> action, int timeout = 60, Action prepareAction = null);
		void ScheduleTransaction(Action<CancellationToken> action, int timeout = 60, Action prepareAction = null);
		void ScheduleTransaction(Action<IWalletProvider, CancellationToken> action, int timeout = 60, Action prepareAction = null);
		void ScheduleChildTransactionalThread(Action action);
		bool IsActiveTransactionThread(int threadId);
		K ScheduleRead<K>(Func<K> action);
		K ScheduleRead<K>(Func<IWalletProvider, K> action);
		void ScheduleRead(Action action);
		void ScheduleRead(Action<IWalletProvider> action);
		K ScheduleWrite<K>(Func<K> action);
		K ScheduleWrite<K>(Func<IWalletProvider, K> action);
		void ScheduleWrite(Action<IWalletProvider> action);
		void ScheduleWrite(Action action);
		K ScheduleKeyedRead<K>(Func<K> action, Action prepareAction = null);
		K ScheduleKeyedRead<K>(Func<IWalletProvider, K> action, Action prepareAction = null);
		void ScheduleKeyedRead(Action action, Action prepareAction = null);
		void ScheduleKeyedRead(Action<IWalletProvider> action, Action prepareAction = null);
		K ScheduleKeyedWrite<K>(Func<K> action, Action prepareAction = null);
		K ScheduleKeyedWrite<K>(Func<IWalletProvider, K> action, Action prepareAction = null);
		void ScheduleKeyedWrite(Action action, Action prepareAction = null);
		void ScheduleKeyedWrite(Action<IWalletProvider> action, Action prepareAction = null);
		K ScheduleTransactionalKeyedRead<K>(Func<CancellationToken, K> action, Action prepareAction = null);
		K ScheduleTransactionalKeyedRead<K>(Func<IWalletProvider, CancellationToken, K> action, Action prepareAction = null);
		void RequestFriendlyAccess(Action action);
		void WhitelistFriendlyThread(int threadId);
		void RemoveFriendlyThread(int threadId);
	}

	public interface IWalletProviderProxy : IWalletProvider, IWalletProviderProxyTransactions, IDisposable2 {
		bool? SyncedNoWait { get; }
	}

	public interface IWalletProviderProxyInternal : IWalletProviderProxy {
		IWalletProvider UnderlyingWalletProvider { get; }
	}

	public abstract class WalletProviderProxy<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWalletProviderProxy, IWalletProviderProxyInternal
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		protected readonly ThreadExclusionResourceAccessScheduler resourceAccessScheduler;

		protected readonly IWalletProviderInternal walletProvider;

		public WalletProviderProxy(CENTRAL_COORDINATOR centralCoordinator, IWalletProvider walletProvider) {
			this.walletProvider = (IWalletProviderInternal) walletProvider;

			this.resourceAccessScheduler = new ThreadExclusionResourceAccessScheduler();
			this.resourceAccessScheduler.Start();
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

		public SynthesizedBlock ConvertApiSynthesizedBlock(SynthesizedBlockAPI synthesizedBlockApi) {
			return this.walletProvider.ConvertApiSynthesizedBlock(synthesizedBlockApi);
		}

		public bool IsWalletLoaded => this.walletProvider.IsWalletLoaded;
		public bool IsWalletEncrypted => this.ScheduleRead(() => this.walletProvider.IsWalletEncrypted);

		public bool IsWalletAccountLoaded => this.ScheduleRead(() => this.walletProvider.IsWalletAccountLoaded);
		public bool WalletFileExists => this.ScheduleRead(() => this.walletProvider.WalletFileExists);

		public void EnsureWalletIsLoaded() {
			this.walletProvider.EnsureWalletIsLoaded();
		}

		public void RemovePIDLock() {
			this.walletProvider.RemovePIDLock();
		}

		public long LowestAccountBlockSyncHeight => this.ScheduleRead(() => this.walletProvider.LowestAccountBlockSyncHeight);
		public bool Synced => this.ScheduleRead(() => this.walletProvider.Synced);

		public bool? SyncedNoWait {
			get {
				(bool result, bool completed) = this.ScheduleReadNoWait(provider => provider.Synced);

				return completed ? (bool?) result : null;
			}
		}

		public bool WalletContainsAccount(Guid accountUuid) {
			return this.ScheduleRead(() => this.walletProvider.WalletContainsAccount(accountUuid));
		}

		public List<IWalletAccount> GetWalletSyncableAccounts(long blockId) {
			return this.ScheduleRead(() => this.walletProvider.GetWalletSyncableAccounts(blockId));
		}

		public IAccountFileInfo GetAccountFileInfo(Guid accountUuid) {
			return this.ScheduleRead(() => this.walletProvider.GetAccountFileInfo(accountUuid));
		}

		public List<IWalletAccount> GetAccounts() {
			return this.ScheduleRead(() => this.walletProvider.GetAccounts());
		}

		public List<IWalletAccount> GetAllAccounts() {
			return this.ScheduleRead(() => this.walletProvider.GetAllAccounts());
		}

		public Guid GetAccountUuid() {
			return this.ScheduleRead(() => this.walletProvider.GetAccountUuid());
		}

		public AccountId GetPublicAccountId() {
			return this.ScheduleRead(() => this.walletProvider.GetPublicAccountId());
		}

		public AccountId GetAccountUuidHash() {
			return this.ScheduleRead(() => this.walletProvider.GetAccountUuidHash());
		}

		public IWalletAccount GetActiveAccount() {
			return this.ScheduleRead(() => this.walletProvider.GetActiveAccount());
		}

		public IWalletAccount GetWalletAccount(Guid id) {
			return this.ScheduleRead(() => this.walletProvider.GetWalletAccount(id));
		}

		public IWalletAccount GetWalletAccount(string name) {
			return this.ScheduleRead(() => this.walletProvider.GetWalletAccount(name));
		}

		public IWalletAccount GetWalletAccount(AccountId accountId) {
			return this.ScheduleRead(() => this.walletProvider.GetWalletAccount(accountId));
		}

		public List<WalletTransactionHistoryHeaderAPI> APIQueryWalletTransactionHistory(Guid accountUuid) {
			return this.ScheduleRead(() => this.walletProvider.APIQueryWalletTransactionHistory(accountUuid));
		}

		public WalletTransactionHistoryDetailsAPI APIQueryWalletTransationHistoryDetails(Guid accountUuid, string transactionId) {
			return this.ScheduleRead(() => this.walletProvider.APIQueryWalletTransationHistoryDetails(accountUuid, transactionId));
		}

		public List<WalletAccountAPI> APIQueryWalletAccounts() {
			return this.ScheduleRead(() => this.walletProvider.APIQueryWalletAccounts());
		}

		public WalletAccountDetailsAPI APIQueryWalletAccountDetails(Guid accountUuid) {
			return this.ScheduleRead(() => this.walletProvider.APIQueryWalletAccountDetails(accountUuid));
		}

		public TransactionId APIQueryWalletAccountPresentationTransactionId(Guid accountUuid) {
			return this.ScheduleRead(() => this.walletProvider.APIQueryWalletAccountPresentationTransactionId(accountUuid));
		}

		public List<TransactionId> GetElectionCacheTransactions(IWalletAccount account) {
			return this.ScheduleRead(() => this.walletProvider.GetElectionCacheTransactions(account));
		}

		public SynthesizedBlock ExtractCachedSynthesizedBlock(long blockId) {
			return this.walletProvider.ExtractCachedSynthesizedBlock(blockId);
		}

		public List<SynthesizedBlock> GetCachedSynthesizedBlocks(long minimumBlockId) {
			return this.walletProvider.GetCachedSynthesizedBlocks(minimumBlockId);
		}

		public IXmssWalletKey CreateXmssKey(string name, float warningLevel, float changeLevel, Action<int> progressCallback = null) {
			return this.walletProvider.CreateXmssKey(name, warningLevel, changeLevel, progressCallback);
		}

		public IXmssWalletKey CreateXmssKey(string name, int treeHeight, int hashBits, WalletProvider.HashTypes HashType, float warningLevel, float changeLevel, Action<int> progressCallback = null) {
			return this.walletProvider.CreateXmssKey(name, treeHeight, hashBits, HashType, warningLevel, changeLevel, progressCallback);
		}

		public IXmssWalletKey CreateXmssKey(string name, Action<int> progressCallback = null) {
			return this.walletProvider.CreateXmssKey(name, progressCallback);
		}

		public IXmssMTWalletKey CreateXmssmtKey(string name, float warningLevel, float changeLevel) {
			return this.walletProvider.CreateXmssmtKey(name, warningLevel, changeLevel);
		}

		public IXmssMTWalletKey CreateXmssmtKey(string name, int treeHeight, int treeLayers, Enums.KeyHashBits hashBits, float warningLevel, float changeLevel) {
			return this.walletProvider.CreateXmssmtKey(name, treeHeight, treeLayers, hashBits, warningLevel, changeLevel);
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

		public IWalletStandardAccountSnapshot CreateNewWalletStandardAccountSnapshot(IWalletAccount account) {
			return this.ScheduleRead(() => this.walletProvider.CreateNewWalletStandardAccountSnapshot(account));
		}

		public IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshot(IWalletAccount account) {
			return this.ScheduleRead(() => this.walletProvider.CreateNewWalletJointAccountSnapshot(account));
		}

		public IWalletStandardAccountSnapshot CreateNewWalletStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot) {
			return this.ScheduleRead(() => this.walletProvider.CreateNewWalletStandardAccountSnapshot(account, accountSnapshot));
		}

		public IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot) {
			return this.ScheduleRead(() => this.walletProvider.CreateNewWalletJointAccountSnapshot(account, accountSnapshot));
		}

		public IWalletStandardAccountSnapshot CreateNewWalletStandardAccountSnapshotEntry() {
			return this.ScheduleRead(() => this.walletProvider.CreateNewWalletStandardAccountSnapshotEntry());
		}

		public IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshotEntry() {
			return this.ScheduleRead(() => this.walletProvider.CreateNewWalletJointAccountSnapshotEntry());
		}

		public IWalletAccountSnapshot GetWalletFileInfoAccountSnapshot(Guid accountUuid) {
			return this.ScheduleRead(() => this.walletProvider.GetWalletFileInfoAccountSnapshot(accountUuid));
		}

		public IWalletAccountSnapshot GetAccountSnapshot(AccountId accountId) {
			return this.ScheduleRead(() => this.walletProvider.GetAccountSnapshot(accountId));
		}

		public void Initialize() {
			this.ScheduleWrite(() => {
				this.walletProvider.Initialize();
			});
		}

		public void CacheSynthesizedBlock(SynthesizedBlock synthesizedBlock) {
			this.walletProvider.CacheSynthesizedBlock(synthesizedBlock);
		}

		public void CleanSynthesizedBlockCache() {
			this.walletProvider.CleanSynthesizedBlockCache();
		}

		public event Delegates.RequestCopyWalletFileDelegate CopyWalletRequest;
		public event Delegates.RequestPassphraseDelegate WalletPassphraseRequest;
		public event Delegates.RequestKeyPassphraseDelegate WalletKeyPassphraseRequest;
		public event Delegates.RequestCopyKeyFileDelegate WalletCopyKeyFileRequest;

		public void CreateNewEmptyWallet(CorrelationContext correlationContext, bool encryptWallet, string passphrase, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet) {
			this.ScheduleWrite(() => {
				this.walletProvider.CreateNewEmptyWallet(correlationContext, encryptWallet, passphrase, walletCreationStepSet);
			});
		}

		public bool AllAccountsHaveSyncStatus(SynthesizedBlock block, WalletAccountChainState.BlockSyncStatuses status) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.AllAccountsHaveSyncStatus(block, status);
			});
		}

		public bool AllAccountsUpdatedWalletBlock(SynthesizedBlock block) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.AllAccountsUpdatedWalletBlock(block);
			});
		}

		public bool AllAccountsUpdatedWalletBlock(SynthesizedBlock block, long previousBlockId) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.AllAccountsUpdatedWalletBlock(block, previousBlockId);
			});
		}

		public void UpdateWalletBlock(SynthesizedBlock block) {
			this.walletProvider.UpdateWalletBlock(block);
		}

		public void UpdateWalletBlock(SynthesizedBlock block, long previousBlockId) {
			this.walletProvider.UpdateWalletBlock(block, previousBlockId);
		}

		public void UpdateWalletKeyLogs(SynthesizedBlock block) {
			this.ScheduleWrite(() => {
				this.walletProvider.UpdateWalletKeyLogs(block);
			});
		}

		public bool AllAccountsWalletKeyLogSet(SynthesizedBlock block) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.AllAccountsWalletKeyLogSet(block);
			});
		}

		public bool SetActiveAccount(string name) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.SetActiveAccount(name);
			});
		}

		public bool SetActiveAccount(Guid accountUuid) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.SetActiveAccount(accountUuid);
			});
		}

		public bool CreateNewCompleteWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, Action<IWalletAccount> accountCreatedCallback = null) {
			// this is a special case where we dont have a wallet, so no need to schedule anything. we will let the create make its own transactions
			return this.walletProvider.CreateNewCompleteWallet(correlationContext, accountName, encryptWallet, encryptKey, encryptKeysIndividually, passphrases, accountCreatedCallback);
		}

		public bool CreateNewCompleteWallet(CorrelationContext correlationContext, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, Action<IWalletAccount> accountCreatedCallback = null) {
			// this is a special case where we dont have a wallet, so no need to schedule anything. we will let the create make its own transactions

			return this.walletProvider.CreateNewCompleteWallet(correlationContext, encryptWallet, encryptKey, encryptKeysIndividually, passphrases, accountCreatedCallback);

		}

		public void UpdateWalletSnapshotFromDigest(IAccountSnapshotDigestChannelCard accountCard) {
			this.ScheduleWrite(() => {
				this.walletProvider.UpdateWalletSnapshotFromDigest(accountCard);
			});
		}

		public void UpdateWalletSnapshotFromDigest(IStandardAccountSnapshotDigestChannelCard accountCard) {
			this.ScheduleWrite(() => {
				this.walletProvider.UpdateWalletSnapshotFromDigest(accountCard);
			});
		}

		public void UpdateWalletSnapshotFromDigest(IJointAccountSnapshotDigestChannelCard accountCard) {
			this.ScheduleWrite(() => {
				this.walletProvider.UpdateWalletSnapshotFromDigest(accountCard);
			});
		}

		public void UpdateWalletSnapshot(IAccountSnapshot accountSnapshot) {
			this.ScheduleWrite(() => {
				this.walletProvider.UpdateWalletSnapshot(accountSnapshot);
			});
		}

		public void UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, Guid accountUuid) {
			this.ScheduleWrite(() => {
				this.walletProvider.UpdateWalletSnapshot(accountSnapshot, accountUuid);
			});
		}

		public void ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases) {
			this.ScheduleWrite(() => {
				this.walletProvider.ChangeWalletEncryption(correlationContext, encryptWallet, encryptKeys, encryptKeysIndividually, passphrases);
			});
		}

		public void SaveWallet() {
			this.ScheduleWrite(() => {
				this.walletProvider.SaveWallet();
			});
		}

		public IWalletAccount CreateNewAccount(string name, bool encryptKeys, bool encryptKeysIndividually, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, bool setactive = false) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.CreateNewAccount(name, encryptKeys, encryptKeysIndividually, correlationContext, walletCreationStepSet, accountCreationStepSet, setactive);
			});
		}

		public bool CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, Action<IWalletAccount> accountCreatedCallback = null) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.CreateNewCompleteAccount(correlationContext, accountName, encryptKeys, encryptKeysIndividually, passphrases, walletCreationStepSet, accountCreatedCallback);
			});
		}

		public bool CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.CreateNewCompleteAccount(correlationContext, accountName, encryptKeys, encryptKeysIndividually, passphrases);
			});
		}

		public void InsertKeyLogTransactionEntry(IWalletAccount account, TransactionIdExtended transactionId, byte keyOrdinalId) {
			this.ScheduleWrite(() => {
				this.walletProvider.InsertKeyLogTransactionEntry(account, transactionId, keyOrdinalId);
			});
		}

		public void InsertKeyLogBlockEntry(IWalletAccount account, BlockId blockId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex) {
			this.ScheduleWrite(() => {
				this.walletProvider.InsertKeyLogBlockEntry(account, blockId, keyOrdinalId, keyUseIndex);
			});
		}

		public void InsertKeyLogDigestEntry(IWalletAccount account, int digestId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex) {
			this.ScheduleWrite(() => {
				this.walletProvider.InsertKeyLogDigestEntry(account, digestId, keyOrdinalId, keyUseIndex);
			});
		}

		public void InsertKeyLogEntry(IWalletAccount account, string eventId, Enums.BlockchainEventTypes eventType, byte keyOrdinalId, KeyUseIndexSet keyUseIndex) {
			this.ScheduleWrite(() => {
				this.walletProvider.InsertKeyLogEntry(account, eventId, eventType, keyOrdinalId, keyUseIndex);
			});
		}

		public void ConfirmKeyLogBlockEntry(IWalletAccount account, BlockId blockId, long confirmationBlockId) {
			this.ScheduleWrite(() => {
				this.walletProvider.ConfirmKeyLogBlockEntry(account, blockId, confirmationBlockId);
			});
		}

		public void ConfirmKeyLogTransactionEntry(IWalletAccount account, TransactionIdExtended transactionId, long confirmationBlockId) {
			this.ScheduleWrite(() => {
				this.walletProvider.ConfirmKeyLogTransactionEntry(account, transactionId, confirmationBlockId);
			});
		}

		public bool KeyLogTransactionExists(IWalletAccount account, TransactionId transactionId) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.KeyLogTransactionExists(account, transactionId);
			});
		}

		public IWalletKey CreateBasicKey(string name, Enums.KeyTypes keyType) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.CreateBasicKey(name, keyType);
			});
		}

		public T CreateBasicKey<T>(string name, Enums.KeyTypes keyType)
			where T : IWalletKey {
			return this.ScheduleWrite(() => {
				return this.walletProvider.CreateBasicKey<T>(name, keyType);
			});
		}

		public void HashKey(IWalletKey key) {
			this.ScheduleWrite(() => {
				this.walletProvider.HashKey(key);
			});
		}

		public void SetChainStateHeight(Guid accountUuid, long blockId) {
			this.ScheduleWrite(() => {
				this.walletProvider.SetChainStateHeight(accountUuid, blockId);
			});
		}

		public long GetChainStateHeight(Guid accountUuid) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.GetChainStateHeight(accountUuid);
			});
		}

		public KeyUseIndexSet GetChainStateLastSyncedKeyHeight(IWalletKey key) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.GetChainStateLastSyncedKeyHeight(key);
			});
		}

		public void UpdateLocalChainStateKeyHeight(IWalletKey key) {
			this.ScheduleWrite(() => {
				this.walletProvider.UpdateLocalChainStateKeyHeight(key);
			});
		}

		public IWalletElectionsHistory InsertElectionsHistoryEntry(SynthesizedBlock.SynthesizedElectionResult electionResult, AccountId electedAccountId) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.InsertElectionsHistoryEntry(electionResult, electedAccountId);
			});
		}

		public void InsertLocalTransactionCacheEntry(ITransactionEnvelope transactionEnvelope) {
			this.ScheduleWrite(() => {
				this.walletProvider.InsertLocalTransactionCacheEntry(transactionEnvelope);
			});
		}

		public IWalletTransactionHistory InsertTransactionHistoryEntry(ITransaction transaction, AccountId targetAccountId, string note) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.InsertTransactionHistoryEntry(transaction, targetAccountId, note);
			});
		}

		public void UpdateLocalTransactionCacheEntry(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash) {
			this.ScheduleWrite(() => {
				this.walletProvider.UpdateLocalTransactionCacheEntry(transactionId, status, gossipMessageHash);
			});
		}

		public IWalletTransactionHistoryFileInfo UpdateLocalTransactionHistoryEntry(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.UpdateLocalTransactionHistoryEntry(transactionId, status);
			});
		}

		public IWalletTransactionCache GetLocalTransactionCacheEntry(TransactionId transactionId) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.GetLocalTransactionCacheEntry(transactionId);
			});
		}

		public void RemoveLocalTransactionCacheEntry(TransactionId transactionId) {
			this.ScheduleWrite(() => {
				this.walletProvider.RemoveLocalTransactionCacheEntry(transactionId);
			});
		}

		public void CreateElectionCacheWalletFile(IWalletAccount account) {
			this.ScheduleWrite(() => {
				this.walletProvider.CreateElectionCacheWalletFile(account);
			});
		}

		public void DeleteElectionCacheWalletFile(IWalletAccount account) {
			this.ScheduleWrite(() => {
				this.walletProvider.DeleteElectionCacheWalletFile(account);
			});
		}

		public void InsertElectionCacheTransactions(List<TransactionId> transactionIds, long blockId, IWalletAccount account) {
			this.ScheduleWrite(() => {
				this.walletProvider.InsertElectionCacheTransactions(transactionIds, blockId, account);
			});
		}

		public void RemoveBlockElection(long blockId, IWalletAccount account) {
			this.ScheduleWrite(() => {
				this.walletProvider.RemoveBlockElection(blockId, account);
			});
		}

		public void RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactionIds, IWalletAccount account) {
			this.ScheduleWrite(() => {
				this.walletProvider.RemoveBlockElectionTransactions(blockId, transactionIds, account);
			});
		}

		public void AddAccountKey<KEY>(Guid accountUuid, KEY key, ImmutableDictionary<int, string> passphrases)
			where KEY : IWalletKey {
			this.ScheduleWrite(() => {
				this.walletProvider.AddAccountKey(accountUuid, key, passphrases);
			});
		}

		public void SetNextKey(Guid accountUuid, IWalletKey nextKey) {
			this.ScheduleKeyedWrite(() => this.walletProvider.SetNextKey(accountUuid, nextKey), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, nextKey.Name, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, nextKey.Name, 1);
			});
		}

		public void UpdateNextKey(Guid accountUuid, IWalletKey nextKey) {
			this.ScheduleKeyedWrite(() => this.walletProvider.UpdateNextKey(accountUuid, nextKey), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, nextKey.Name, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, nextKey.Name, 1);
			});
		}

		public void CreateNextXmssKey(Guid accountUuid, string keyName) {
			this.ScheduleKeyedWrite(() => this.walletProvider.CreateNextXmssKey(accountUuid, keyName), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1);
			});
		}

		public void CreateNextXmssKey(Guid accountUuid, byte ordinal) {
			this.ScheduleKeyedWrite(() => this.walletProvider.CreateNextXmssKey(accountUuid, ordinal), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, ordinal, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, ordinal, 1);
			});
		}

		public bool IsNextKeySet(Guid accountUuid, string keyName) {
			return this.ScheduleKeyedRead(() => this.walletProvider.IsNextKeySet(accountUuid, keyName), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1);
			});
		}

		public IWalletKey LoadKey(Guid AccountUuid, string keyName) {
			return this.ScheduleKeyedRead(() => this.walletProvider.LoadKey(AccountUuid, keyName), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(AccountUuid, keyName, 1);
				this.walletProvider.EnsureKeyPassphrase(AccountUuid, keyName, 1);
			});
		}

		public IWalletKey LoadKey(Guid AccountUuid, byte ordinal) {
			return this.ScheduleKeyedRead(() => this.walletProvider.LoadKey(AccountUuid, ordinal), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(AccountUuid, ordinal, 1);
				this.walletProvider.EnsureKeyPassphrase(AccountUuid, ordinal, 1);
			});

		}

		public IWalletKey LoadKey(string keyName) {
			return this.ScheduleKeyedRead(() => this.walletProvider.LoadKey(keyName), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(this.GetAccountUuid(), keyName, 1);
				this.walletProvider.EnsureKeyPassphrase(this.GetAccountUuid(), keyName, 1);
			});
		}

		public IWalletKey LoadKey(byte ordinal) {
			return this.ScheduleTransaction(t => this.walletProvider.LoadKey(ordinal), 20, () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(this.GetAccountUuid(), ordinal, 1);
				this.walletProvider.EnsureKeyPassphrase(this.GetAccountUuid(), ordinal, 1);
			});
		}

		public T LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string keyName)
			where K : class, IWalletKey
			where T : class {
			return this.ScheduleTransactionalKeyedRead(t => this.walletProvider.LoadKey(selector, accountUuid, keyName), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1);
			});
		}

		public T LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, byte ordinal)
			where K : class, IWalletKey
			where T : class {
			return this.ScheduleTransactionalKeyedRead(t => this.walletProvider.LoadKey(selector, accountUuid, ordinal), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, ordinal, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, ordinal, 1);
			});
		}

		public T LoadKey<T>(Func<T, T> selector, Guid accountUuid, string keyName)
			where T : class, IWalletKey {
			return this.ScheduleTransactionalKeyedRead(t => this.walletProvider.LoadKey<T>(selector, accountUuid, keyName), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1);
			});
		}

		public T LoadKey<T>(Func<T, T> selector, Guid accountUuid, byte ordinal)
			where T : class, IWalletKey {
			return this.ScheduleTransactionalKeyedRead(t => this.walletProvider.LoadKey<T>(selector, accountUuid, ordinal), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, ordinal, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, ordinal, 1);
			});
		}

		public T LoadKey<T>(Guid accountUuid, string keyName)
			where T : class, IWalletKey {
			return this.ScheduleTransactionalKeyedRead(t => this.walletProvider.LoadKey<T>(accountUuid, keyName), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, keyName, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, keyName, 1);
			});
		}

		public T LoadKey<T>(Guid accountUuid, byte ordinal)
			where T : class, IWalletKey {

			return this.ScheduleTransactionalKeyedRead(t => this.walletProvider.LoadKey<T>(accountUuid, ordinal), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(accountUuid, ordinal, 1);
				this.walletProvider.EnsureKeyPassphrase(accountUuid, ordinal, 1);
			});
		}

		public T LoadKey<T>(string keyName)
			where T : class, IWalletKey {

			return this.ScheduleTransactionalKeyedRead(t => this.walletProvider.LoadKey<T>(keyName), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(this.GetAccountUuid(), keyName, 1);
				this.walletProvider.EnsureKeyPassphrase(this.GetAccountUuid(), keyName, 1);
			});
		}

		public T LoadKey<T>(byte ordinal)
			where T : class, IWalletKey {

			return this.ScheduleTransactionalKeyedRead(t => this.walletProvider.LoadKey<T>(ordinal), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(this.GetAccountUuid(), ordinal, 1);
				this.walletProvider.EnsureKeyPassphrase(this.GetAccountUuid(), ordinal, 1);
			});
		}

		public void UpdateKey(IWalletKey key) {

			this.ScheduleKeyedWrite(() => this.walletProvider.UpdateKey(key), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(key.AccountUuid, key.Name, 1);
				this.walletProvider.EnsureKeyPassphrase(key.AccountUuid, key.Name, 1);
			});
		}

		public void SwapNextKey(IWalletKey key, bool storeHistory = true) {

			this.ScheduleKeyedWrite(() => this.walletProvider.SwapNextKey(key, storeHistory), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureKeyFileIsPresent(key.AccountUuid, key.Name, 1);
				this.walletProvider.EnsureKeyPassphrase(key.AccountUuid, key.Name, 1);
			});
		}

		public void EnsureWalletLoaded() {

			this.ScheduleKeyedWrite(() => this.walletProvider.EnsureWalletLoaded(), () => {
				// load wallet & key
				this.walletProvider.EnsureWalletFileIsPresent();
				this.walletProvider.EnsureWalletPassphrase();
			});

		}

		public void SetExternalPassphraseHandlers(Delegates.RequestPassphraseDelegate requestPassphraseDelegate, Delegates.RequestKeyPassphraseDelegate requestKeyPassphraseDelegate, Delegates.RequestCopyKeyFileDelegate requestKeyCopyFileDelegate, Delegates.RequestCopyWalletFileDelegate copyWalletDelegate) {
			this.ScheduleWrite(() => {
				this.walletProvider.SetExternalPassphraseHandlers(requestPassphraseDelegate, requestKeyPassphraseDelegate, requestKeyCopyFileDelegate, copyWalletDelegate);
			});
		}

		public void SetConsolePassphraseHandlers() {
			this.ScheduleWrite(() => {
				this.walletProvider.SetConsolePassphraseHandlers();
			});
		}

		public SecureString RequestWalletPassphraseByConsole(int maxTryCount = 10) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.RequestWalletPassphraseByConsole(maxTryCount);
			});
		}

		public SecureString RequestKeysPassphraseByConsole(Guid accountUUid, string keyName, int maxTryCount = 10) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.RequestKeysPassphraseByConsole(accountUUid, keyName, maxTryCount);
			});
		}

		public SecureString RequestPassphraseByConsole(string passphraseType = "wallet", int maxTryCount = 10) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.RequestPassphraseByConsole(passphraseType, maxTryCount);
			});
		}

		public SafeArrayHandle PerformCryptographicSignature(Guid accountUuid, string keyName, SafeArrayHandle message) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.PerformCryptographicSignature(accountUuid, keyName, message);
			});
		}

		public SafeArrayHandle PerformCryptographicSignature(IWalletKey key, SafeArrayHandle message) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.PerformCryptographicSignature(key, message);
			});
		}

		public IWalletStandardAccountSnapshot GetStandardAccountSnapshot(AccountId accountId) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.GetStandardAccountSnapshot(accountId);
			});
		}

		public IWalletJointAccountSnapshot GetJointAccountSnapshot(AccountId accountId) {
			return this.ScheduleWrite(() => {
				return this.walletProvider.GetJointAccountSnapshot(accountId);
			});
		}

		public (string path, string passphrase, string salt, int iterations) BackupWallet() {

			return this.ScheduleTransaction(t => this.walletProvider.BackupWallet(), 60 * 5, () => {
				// load wallet & key
				this.walletProvider.EnsureWalletFileIsPresent();
			});
		}

		public void UpdateWalletChainStateSyncStatus(Guid accountUuid, long BlockId, WalletAccountChainState.BlockSyncStatuses blockSyncStatus) {
			this.ScheduleWrite(() => {

				this.walletProvider.UpdateWalletChainStateSyncStatus(accountUuid, BlockId, blockSyncStatus);
			});
		}

		public SafeArrayHandle SignTransaction(SafeArrayHandle transactionHash, string keyName) {
			return this.ScheduleTransaction(t => this.walletProvider.SignTransaction(transactionHash, keyName), 20, () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
			});
		}

		public SafeArrayHandle SignTransactionXmss(SafeArrayHandle transactionHash, IXmssWalletKey key) {
			return this.ScheduleTransaction(t => this.walletProvider.SignTransactionXmss(transactionHash, key), 20, () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureWalletKeyIsReady(key.AccountUuid, key.Name);
			});
		}

		public SafeArrayHandle SignTransaction(SafeArrayHandle transactionHash, IWalletKey key) {
			return this.ScheduleTransaction(t => this.walletProvider.SignTransaction(transactionHash, key), 20, () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureWalletKeyIsReady(key.AccountUuid, key.Name);
			});
		}

		public SafeArrayHandle SignMessageXmss(Guid accountUuid, SafeArrayHandle message) {

			return this.ScheduleTransaction(t => this.walletProvider.SignMessageXmss(accountUuid, message), 20, () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureWalletKeyIsReady(accountUuid, GlobalsService.MESSAGE_KEY_NAME);
			});
		}

		public SafeArrayHandle SignMessageXmss(SafeArrayHandle messageHash, IXmssWalletKey key) {
			return this.ScheduleTransaction(t => this.walletProvider.SignMessageXmss(messageHash, key), 20, () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureWalletKeyIsReady(key.AccountUuid, key.Name);
			});
		}

		public SafeArrayHandle SignMessage(SafeArrayHandle messageHash, IWalletKey key) {
			return this.ScheduleTransaction(t => this.walletProvider.SignMessage(messageHash, key), 20, () => {
				// load wallet & key
				this.walletProvider.EnsureWalletIsLoaded();
				this.walletProvider.EnsureWalletKeyIsReady(key.AccountUuid, key.Name);
			});
		}

		public bool LoadWallet(CorrelationContext correlationContext) {

			return this.ScheduleTransaction(t => this.walletProvider.LoadWallet(correlationContext), 60, () => {
				// load wallet & key
				this.walletProvider.EnsureWalletFileIsPresent();
				this.walletProvider.EnsureWalletPassphrase();
			});
		}

		public IWalletProvider UnderlyingWalletProvider => this.walletProvider;

		public void SetWalletPassphrase(string passphrase) {
			this.ScheduleWrite(() => {
				this.walletProvider.SetWalletPassphrase(passphrase);
			});
		}

		public void SetWalletPassphrase(SecureString passphrase) {
			this.ScheduleWrite(() => {
				this.walletProvider.SetWalletPassphrase(passphrase);
			});
		}

		public void SetKeysPassphrase(Guid accountUuid, string keyname, string passphrase) {
			this.ScheduleWrite(() => {
				this.walletProvider.SetKeysPassphrase(accountUuid, keyname, passphrase);
			});
		}

		public void SetKeysPassphrase(Guid accountUuid, string keyname, SecureString passphrase) {
			this.ScheduleWrite(() => {
				this.walletProvider.SetKeysPassphrase(accountUuid, keyname, passphrase);
			});
		}

	#region wrappers

		public K ScheduleRead<K>(Func<K> action) {
			return this.ScheduleRead(provider => action());
		}

		public (K result, bool completed) ScheduleReadNoWait<K>(Func<IWalletProvider, K> action) {
			return this.resourceAccessScheduler.ScheduleRead(this.walletProvider, action, false);
		}

		public K ScheduleRead<K>(Func<IWalletProvider, K> action) {
			return this.resourceAccessScheduler.ScheduleRead(this.walletProvider, action).result;
		}

		public bool ScheduleReadNoWait(Action<IWalletProvider> action) {
			return this.resourceAccessScheduler.ScheduleRead(this.walletProvider, action, false);
		}

		public void ScheduleRead(Action<IWalletProvider> action) {
			this.resourceAccessScheduler.ScheduleRead(this.walletProvider, action);
		}

		public bool ScheduleReadNoWait(Action action) {
			return this.ScheduleReadNoWait(provider => action());
		}

		public void ScheduleRead(Action action) {
			this.ScheduleRead(provider => action());
		}

		public K ScheduleWrite<K>(Func<K> action) {
			return this.ScheduleWrite(provider => action());
		}

		public K ScheduleWrite<K>(Func<IWalletProvider, K> action) {
			return this.resourceAccessScheduler.ScheduleWrite(this.walletProvider, action);
		}

		public void ScheduleWrite(Action<IWalletProvider> action) {
			this.resourceAccessScheduler.ScheduleWrite(this.walletProvider, action);
		}

		public void ScheduleWrite(Action action) {
			this.ScheduleWrite(provider => action());
		}

		private void KeyedAction(Action action, Action preloadKeys = null) {

			ITaskStasher taskStasher = TaskContextRegistry.Instance.GetTaskRoutingTaskRoutingContext();
			CorrelationContext correlationContext = TaskContextRegistry.Instance.GetTaskRoutingCorrelationContext();

			BlockchainEventException walletEventException = null;

			bool initialized = false;

			int attempt = 0;

			do {

				attempt++;

				if(attempt > 3) {
					if(walletEventException != null) {
						throw walletEventException;
					}

					throw new ApplicationException("Failed keyed operation");
				}

				if(walletEventException != null) {
					// ok, we need to perform some event here. lets do it
					taskStasher?.Stash();

					if(walletEventException is WalletFileMissingException walletFileMissingException) {
						this.walletProvider.RequestCopyWallet(correlationContext, attempt);
					}

					if(walletEventException is WalletPassphraseMissingException walletPassphraseMissingException) {
						this.walletProvider.CaptureWalletPassphrase(correlationContext, attempt);
					}

					if(walletEventException is WalletDecryptionException walletDecryptionException) {
						// decryption failed, lets reset the passphrase
						this.walletProvider.ClearWalletPassphrase();
					}

					if(walletEventException is KeyFileMissingException keyFileMissingException) {
						this.walletProvider.EnsureKeyFileIsPresent(keyFileMissingException.AccountUuid, keyFileMissingException.KeyName, attempt);
					}

					if(walletEventException is KeyPassphraseMissingException keyPassphraseMissingException) {
						this.walletProvider.CaptureKeyPassphrase(correlationContext, keyPassphraseMissingException.AccountUuid, keyPassphraseMissingException.KeyName, attempt);
					}

					if(walletEventException is KeyDecryptionException keyDecryptionException) {
						// decryption failed, lets reset the passphrase
						this.walletProvider.ClearWalletKeyPassphrase(keyDecryptionException.AccountUuid, keyDecryptionException.KeyName);
					}

					taskStasher?.CompleteStash();
				}

				walletEventException = null;

				try {
					if(!initialized) {
						preloadKeys?.Invoke();
						initialized = true;
					}

					action();

					break;

				} catch(WalletFileMissingException ex) {

					walletEventException = ex;
				} catch(WalletPassphraseMissingException ex) {

					walletEventException = ex;
				} catch(WalletDecryptionException ex) {

					walletEventException = ex;
				} catch(KeyFileMissingException ex) {

					walletEventException = ex;
				} catch(KeyPassphraseMissingException ex) {

					walletEventException = ex;
				} catch(KeyDecryptionException ex) {

					walletEventException = ex;
				}

			} while(true);
		}

		public K ScheduleKeyedRead<K>(Func<IWalletProvider, K> action, Action prepareAction = null) {
			K result = default;

			this.KeyedAction(() => {

				result = this.ScheduleRead(action);
			}, prepareAction);

			return result;
		}

		public K ScheduleKeyedRead<K>(Func<K> action, Action prepareAction = null) {
			return this.ScheduleKeyedRead(provider => action(), prepareAction);
		}

		public void ScheduleKeyedRead(Action<IWalletProvider> action, Action prepareAction = null) {
			this.KeyedAction(() => {

				this.ScheduleRead(action);
			}, prepareAction);
		}

		public void ScheduleKeyedRead(Action action, Action prepareAction = null) {

			this.ScheduleKeyedRead(provider => action(), prepareAction);
		}

		public K ScheduleKeyedWrite<K>(Func<IWalletProvider, K> action, Action prepareAction = null) {
			K result = default;

			this.KeyedAction(() => {

				result = this.ScheduleWrite(action);
			}, prepareAction);

			return result;
		}

		public K ScheduleKeyedWrite<K>(Func<K> action, Action prepareAction = null) {
			return this.ScheduleKeyedWrite(provider => action(), prepareAction);
		}

		public void ScheduleKeyedWrite(Action<IWalletProvider> action, Action prepareAction = null) {
			this.KeyedAction(() => {

				this.ScheduleWrite(action);
			}, prepareAction);
		}

		public void ScheduleKeyedWrite(Action action, Action prepareAction = null) {
			this.ScheduleKeyedWrite(provider => action(), prepareAction);
		}

		public K ScheduleTransactionalKeyedRead<K>(Func<IWalletProvider, CancellationToken, K> action, Action prepareAction = null) {
			K result = default;

			return this.ScheduleTransaction(action, 60, prepareAction);
		}

		public K ScheduleTransactionalKeyedRead<K>(Func<CancellationToken, K> action, Action prepareAction = null) {
			return this.ScheduleTransactionalKeyedRead((provider, token) => action(token), prepareAction);
		}

		public K ScheduleTransaction<K>(Func<IWalletProvider, CancellationToken, K> action, int timeout = 60, Action prepareAction = null) {
			K result = default;

			void RunTransaction(Func<IWalletProvider, CancellationToken, K> action2, CancellationToken token) {

				if(this.walletProvider.TransactionInProgress) {
					// we are in a transaction, just go through
					this.KeyedAction(() => {
						result = action2(this.walletProvider, token);
					}, prepareAction);
				} else {
					// we must create a transaction first...
					this.walletProvider.PerformWalletTransaction((prov, t) => {
						this.KeyedAction(() => {
							result = action2(this.walletProvider, t);
						}, prepareAction);
					}, token, (prov, a, t) => {
						this.resourceAccessScheduler.ScheduleWrite(prov, a);
					}, (prov, b, t) => {
						this.resourceAccessScheduler.ScheduleWrite(prov, b);
					});
				}
			}

			if(this.resourceAccessScheduler.ThreadLockInProgress && this.resourceAccessScheduler.TheadAllowed) {
				// we are already in a treadlock and we have access
				RunTransaction(action, this.resourceAccessScheduler.ActiveToken);
			} else {
				// we have to request access too
				this.resourceAccessScheduler.PerformThreadLock(this.walletProvider, (prov, t) => {
					RunTransaction(action, t);
				}, timeout);
			}

			return result;
		}

		public K ScheduleTransaction<K>(Func<CancellationToken, K> action, int timeout = 60, Action prepareAction = null) {
			return this.ScheduleTransaction((provider, token) => action(token), timeout, prepareAction);
		}

		public void ScheduleTransaction(Func<IWalletProvider, CancellationToken, Task> action, int timeout = 60, Action prepareAction = null) {

			async void RunTransaction(Func<IWalletProvider, CancellationToken, Task> action2, CancellationToken token) {
				if(this.walletProvider.TransactionInProgress) {
					// we are in a transaction, just go through
					this.KeyedAction(async () => {
						await action2(this.walletProvider, token);
					}, prepareAction);
				} else {
					// we must create a transaction first...
					this.KeyedAction(async () => {
						this.walletProvider.PerformWalletTransaction(action2, token, (prov, a, t) => {
							this.resourceAccessScheduler.ScheduleWrite(prov, a);
						}, (prov, b, t) => {
							this.resourceAccessScheduler.ScheduleWrite(prov, b);
						});
					}, prepareAction);
				}
			}

			if(this.resourceAccessScheduler.ThreadLockInProgress && this.resourceAccessScheduler.TheadAllowed) {
				// we are already in a treadlock and we have access
				RunTransaction(action, this.resourceAccessScheduler.ActiveToken);
			} else {
				// we have to request access too
				this.resourceAccessScheduler.PerformThreadLock(this.walletProvider, (prov, t) => {
					RunTransaction(action, t);
				}, timeout);
			}
		}

		public void ScheduleTransaction(Action<IWalletProvider, CancellationToken> action, int timeout = 60, Action prepareAction = null) {
			void RunTransaction(Action<IWalletProvider, CancellationToken> action2, CancellationToken token) {
				if(this.walletProvider.TransactionInProgress) {
					// we are in a transaction, just go through
					this.KeyedAction(() => {
						action2(this.walletProvider, token);
					}, prepareAction);
				} else {
					// we must create a transaction first...
					this.KeyedAction(() => {
						this.walletProvider.PerformWalletTransaction(action2, token, (prov, a, t) => {
							this.resourceAccessScheduler.ScheduleWrite(prov, a);
						}, (prov, b, t) => {
							this.resourceAccessScheduler.ScheduleWrite(prov, b);
						});
					}, prepareAction);
				}
			}

			if(this.resourceAccessScheduler.ThreadLockInProgress && this.resourceAccessScheduler.TheadAllowed) {
				// we are already in a treadlock and we have access
				RunTransaction(action, this.resourceAccessScheduler.ActiveToken);
			} else {
				// we have to request access too
				this.resourceAccessScheduler.PerformThreadLock(this.walletProvider, (prov, t) => {
					RunTransaction(action, t);
				}, timeout);
			}
		}

		public void ScheduleTransaction(Action<CancellationToken> action, int timeout = 60, Action prepareAction = null) {
			this.ScheduleTransaction((provider, token) => action(token), timeout, prepareAction);
		}

		public void ScheduleChildTransactionalThread(Action action) {
			this.resourceAccessScheduler.AllowChildThreadLock(action);
		}

		public bool IsActiveTransactionThread(int threadId) {
			return this.resourceAccessScheduler.ThreadLockInProgress && (this.resourceAccessScheduler.CurrentActiveThread == threadId);
		}

		/// <summary>
		///     Run the operation as a friend thread. If a transaction is active, the code will be allowed to run. use with
		///     caution!
		/// </summary>
		/// <param name="action"></param>
		public void RequestFriendlyAccess(Action action) {
			this.resourceAccessScheduler.RequestFriendlyAccess(action);
		}

		/// <summary>
		///     Explicitely whitelist a friendly thread in a transaction.
		/// </summary>
		/// <param name="threadId"></param>
		public void WhitelistFriendlyThread(int threadId) {
			this.resourceAccessScheduler.WhitelistFriendlyThread(threadId);
		}

		public void RemoveFriendlyThread(int threadId) {
			this.resourceAccessScheduler.RemoveFriendlyThread(threadId);
		}

		public bool IsActiveTransaction => this.resourceAccessScheduler.ThreadLockInProgress;

	#endregion

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				try {
					this.resourceAccessScheduler.Stop();
				} finally {
					this.IsDisposed = true;
				}
			}
		}

		~WalletProviderProxy() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}