using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Extra;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Signatures.QTesla;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;
using Nito.AsyncEx.Synchronous;
using Serilog;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public static class WalletProvider {
		public enum HashTypes {
			Sha2,
			Sha3,
			Blake2
		}

		public const HashTypes TRANSACTION_KEY_HASH_TYPE = HashTypes.Sha2;
		public const HashTypes MESSAGE_KEY_HASH_TYPE = HashTypes.Sha2;

		public const int DEFAULT_KEY_HASH_BITS = 256;

		public const int TRANSACTION_KEY_HASH_BITS = 512;
		public const int MESSAGE_KEY_HASH_BITS = 256;
		public const int CHANGE_KEY_HASH_BITS = 512;

		public const int TRANSACTION_KEY_XMSS_TREE_HEIGHT = XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT;

		/// <summary>
		///     This one is usually used more often than the main key
		/// </summary>
		public const int MESSAGE_KEY_XMSS_TREE_HEIGHT = XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT;

		/// <summary>
		///     This one is usually used more often than the main key
		/// </summary>
		public const int CHANGE_KEY_XMSS_TREE_HEIGHT = XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT;

		public const int MINIMAL_XMSS_KEY_HEIGHT = 5;
	}

	public interface IUtilityWalletProvider : IDisposableExtended, IChainProvider {
		public bool IsWalletLoaded { get; }
		public string GetChainDirectoryPath();
		public string GetChainStorageFilesPath();
		public string GetSystemFilesDirectoryPath();
		SynthesizedBlockAPI DeserializeSynthesizedBlockAPI(string synthesizedBlock);
		Task<SynthesizedBlock> ConvertApiSynthesizedBlock(SynthesizedBlockAPI synthesizedBlockApi, LockContext lockContext);

		public void EnsureWalletIsLoaded();

		public Task RemovePIDLock();

		void HashKey(IWalletKey key);

		IWalletKey CreateBasicKey(string name, Enums.KeyTypes keyType);

		T CreateBasicKey<T>(string name, Enums.KeyTypes keyType)
			where T : IWalletKey;

		Task<IXmssWalletKey> CreateXmssKey(string name, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, int treeHeight, int hashBits, WalletProvider.HashTypes HashType, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, Func<int, Task> progressCallback = null);
		Task<IXmssMTWalletKey> CreateXmssmtKey(string name, float warningLevel, float changeLevel, Func<int, int, int, Task> progressCallback = null);
		Task<IXmssMTWalletKey> CreateXmssmtKey(string name, int treeHeight, int treeLayers, Enums.KeyHashBits hashBits, float warningLevel, float changeLevel, Func<int, int, int, Task> progressCallback = null);
		IQTeslaWalletKey CreatePresentationQTeslaKey(string name);
		IQTeslaWalletKey CreateQTeslaKey(string name, QTESLASecurityCategory.SecurityCategories securityCategory);

		public void PrepareQTeslaKey<T>(T key, QTESLASecurityCategory.SecurityCategories securityCategory)
			where T : IQTeslaWalletKey;

		public ISecretWalletKey CreateSuperKey();
		public ISecretWalletKey CreateSecretKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null);
		public ISecretComboWalletKey CreateSecretComboKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null);
		public ISecretDoubleWalletKey CreateSecretDoubleKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, QTESLASecurityCategory.SecurityCategories securityCategorySecond, ISecretDoubleWalletKey previousKey = null);
	}

	public interface IReadonlyWalletProvider {
		Task<bool> IsWalletEncrypted(LockContext lockContext);
		Task<bool> IsWalletAccountLoaded(LockContext lockContext);
		Task<bool> WalletFileExists(LockContext lockContext);
		Task<long?> LowestAccountBlockSyncHeight(LockContext lockContext);
		Task<bool?> Synced(LockContext lockContext);
		Task<bool> WalletContainsAccount(Guid accountUuid, LockContext lockContext);
		Task<List<IWalletAccount>> GetWalletSyncableAccounts(long blockId, LockContext lockContext);
		Task<IAccountFileInfo> GetAccountFileInfo(Guid accountUuid, LockContext lockContext);
		Task<List<IWalletAccount>> GetAccounts(LockContext lockContext);
		Task<List<IWalletAccount>> GetAllAccounts(LockContext lockContext);
		Task<Guid> GetAccountUuid(LockContext lockContext);
		Task<AccountId> GetPublicAccountId(LockContext lockContext);
		Task<AccountId> GetPublicAccountId(Guid accountUuid, LockContext lockContext);
		Task<AccountId> GetAccountUuidHash(LockContext lockContext);
		Task<bool> IsDefaultAccountPublished(LockContext lockContext);
		Task<bool> IsAccountPublished(Guid accountUuid, LockContext lockContext);
		Task<IWalletAccount> GetActiveAccount(LockContext lockContext);
		Task<IWalletAccount> GetWalletAccount(Guid id, LockContext lockContext);
		Task<IWalletAccount> GetWalletAccount(string name, LockContext lockContext);
		Task<IWalletAccount> GetWalletAccount(AccountId accountId, LockContext lockContext);

		Task<List<WalletTransactionHistoryHeaderAPI>> APIQueryWalletTransactionHistory(Guid accountUuid, LockContext lockContext);
		Task<WalletTransactionHistoryDetailsAPI> APIQueryWalletTransactionHistoryDetails(Guid accountUuid, string transactionId, LockContext lockContext);
		Task<WalletInfoAPI> APIQueryWalletInfoAPI(LockContext lockContext);
		Task<List<WalletAccountAPI>> APIQueryWalletAccounts(LockContext lockContext);
		Task<WalletAccountDetailsAPI> APIQueryWalletAccountDetails(Guid accountUuid, LockContext lockContext);
		Task<TransactionId> APIQueryWalletAccountPresentationTransactionId(Guid accountUuid, LockContext lockContext);
		Task<List<TransactionId>> GetElectionCacheTransactions(IWalletAccount account, LockContext lockContext);

		BlockId GetHighestCachedSynthesizedBlockId(LockContext lockContext);
		bool IsSynthesizedBlockCached(long blockId, LockContext lockContext);
		public SynthesizedBlock ExtractCachedSynthesizedBlock(long blockId);
		public List<SynthesizedBlock> GetCachedSynthesizedBlocks(long minimumBlockId, LockContext lockContext);

		Task<IWalletAccountSnapshot> GetWalletFileInfoAccountSnapshot(Guid accountUuid, LockContext lockContext);

		Task<IWalletAccountSnapshot> GetAccountSnapshot(AccountId accountId, LockContext lockContext);
	}

	public interface IWalletProviderWrite : IChainProvider {

		Task<Dictionary<AccountId, int>> ClearTimedOutTransactions(LockContext lockContext);
		Task<bool> ResetTimedOutWalletEntries(LockContext lockContext, List<(Guid accountUuid, string name)> forcedKeys = null);
		Task<bool> ResetAllTimedOut(LockContext lockContext, List<(Guid accountUuid, string name)> forcedKeys = null);

		Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, LockContext lockContext);
		Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, LockContext lockContext);
		Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot, LockContext lockContext);
		Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot, LockContext lockContext);
		Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshotEntry(LockContext lockContext);
		Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshotEntry(LockContext lockContext);
		Task ChangeAccountsCorrelation(ImmutableList<AccountId> enableAccounts, ImmutableList<AccountId> disableAccounts, LockContext lockContext);
		Task CacheSynthesizedBlock(SynthesizedBlock synthesizedBlock, LockContext lockContext);
		Task CleanSynthesizedBlockCache(LockContext lockContext);
		event Delegates.RequestCopyWalletFileDelegate CopyWalletRequest;
		event Delegates.RequestPassphraseDelegate WalletPassphraseRequest;
		event Delegates.RequestKeyPassphraseDelegate WalletKeyPassphraseRequest;
		event Delegates.RequestCopyKeyFileDelegate WalletCopyKeyFileRequest;
		Task CreateNewEmptyWallet(CorrelationContext correlationContext, bool encryptWallet, string passphrase, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, LockContext lockContext);
		Task<bool> AllAccountsHaveSyncStatus(SynthesizedBlock block, WalletAccountChainState.BlockSyncStatuses status, LockContext lockContext);
		Task<bool> AllAccountsUpdatedWalletBlock(SynthesizedBlock block, LockContext lockContext);
		Task<bool> AllAccountsUpdatedWalletBlock(SynthesizedBlock block, long previousBlockId, LockContext lockContext);
		Task UpdateWalletBlock(SynthesizedBlock synthesizedBlock, long previousSyncedBlockId, Func<SynthesizedBlock, LockContext, Task> callback, LockContext lockContext);
		Task<bool> AllAccountsWalletKeyLogSet(SynthesizedBlock block, LockContext lockContext);
		Task<bool> SetActiveAccount(string name, LockContext lockContext);
		Task<bool> SetActiveAccount(Guid accountUuid, LockContext lockContext);
		Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null);
		Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null);
		Task UpdateWalletSnapshotFromDigest(IAccountSnapshotDigestChannelCard accountCard, LockContext lockContext);
		Task UpdateWalletSnapshotFromDigest(IStandardAccountSnapshotDigestChannelCard accountCard, LockContext lockContext);
		Task UpdateWalletSnapshotFromDigest(IJointAccountSnapshotDigestChannelCard accountCard, LockContext lockContext);
		Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, LockContext lockContext);
		Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, Guid accountUuid, LockContext lockContext);
		Task ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext);
		Task SaveWallet(LockContext lockContext);
		Task<IWalletAccount> CreateNewAccount(string name, bool encryptKeys, bool encryptKeysIndividually, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, LockContext lockContext, bool setactive = false);
		Task<bool> CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, Action<IWalletAccount> accountCreatedCallback = null);
		Task<bool> CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext);
		Task InsertKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, KeyUseIndexSet keyUseIndexSet, byte keyOrdinalId, LockContext lockContext);
		Task InsertKeyLogBlockEntry(IWalletAccount account, BlockId blockId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex, LockContext lockContext);
		Task InsertKeyLogDigestEntry(IWalletAccount account, int digestId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex, LockContext lockContext);
		Task InsertKeyLogEntry(IWalletAccount account, string eventId, Enums.BlockchainEventTypes eventType, byte keyOrdinalId, KeyUseIndexSet keyUseIndex, LockContext lockContext);
		Task ConfirmKeyLogBlockEntry(IWalletAccount account, BlockId blockId, long confirmationBlockId, LockContext lockContext);
		Task ConfirmKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, KeyUseIndexSet keyUseIndexSet, long confirmationBlockId, LockContext lockContext);
		Task<bool> KeyLogTransactionExists(IWalletAccount account, TransactionId transactionId, LockContext lockContext);

		Task SetChainStateHeight(Guid accountUuid, long blockId, LockContext lockContext);
		Task SetChainStateHeight(IWalletAccountChainState chainState, long blockId, LockContext lockContext);

		Task<long> GetChainStateHeight(Guid accountUuid, LockContext lockContext);
		Task<KeyUseIndexSet> GetChainStateLastSyncedKeyHeight(IWalletKey key, LockContext lockContext);
		Task UpdateLocalChainStateKeyHeight(IWalletKey key, LockContext lockContext);
		Task<IWalletElectionsHistory> InsertElectionsHistoryEntry(SynthesizedBlock.SynthesizedElectionResult electionResult, AccountId electedAccountId, LockContext lockContext);
		Task InsertLocalTransactionCacheEntry(ITransactionEnvelope transactionEnvelope, LockContext lockContext);
		Task<List<IWalletTransactionHistory>> InsertTransactionHistoryEntry(ITransaction transaction, string note, LockContext lockContext);
		Task UpdateLocalTransactionCacheEntry(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash, LockContext lockContext);
		Task<IWalletTransactionHistoryFileInfo> UpdateLocalTransactionHistoryEntry(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status, LockContext lockContext);
		Task<IWalletTransactionCache> GetLocalTransactionCacheEntry(TransactionId transactionId, LockContext lockContext);
		Task RemoveLocalTransactionCacheEntry(TransactionId transactionId, LockContext lockContext);
		Task CreateElectionCacheWalletFile(IWalletAccount account, LockContext lockContext);
		Task DeleteElectionCacheWalletFile(IWalletAccount account, LockContext lockContext);
		Task InsertElectionCacheTransactions(List<TransactionId> transactionIds, long blockId, IWalletAccount account, LockContext lockContext);
		Task RemoveBlockElection(long blockId, IWalletAccount account, LockContext lockContext);
		Task RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactionIds, IWalletAccount account, LockContext lockContext);

		Task AddAccountKey<KEY>(Guid accountUuid, KEY key, ImmutableDictionary<int, string> passphrases, LockContext lockContext, KEY nextKey = null)
			where KEY : class, IWalletKey;

		Task SetNextKey(Guid accountUuid, IWalletKey nextKey, LockContext lockContext);
		Task UpdateNextKey(IWalletKey nextKey, LockContext lockContext);
		Task CreateNextXmssKey(Guid accountUuid, string keyName, LockContext lockContext);
		Task CreateNextXmssKey(Guid accountUuid, byte ordinal, LockContext lockContext);
		Task<bool> IsKeyEncrypted(Guid accountUuid, LockContext lockContext);
		Task<bool> IsNextKeySet(Guid accountUuid, string keyName, LockContext lockContext);

		Task<T> LoadNextKey<T>(Guid AccountUuid, string keyName, LockContext lockContext)
			where T : class, IWalletKey;

		Task<IWalletKey> LoadNextKey(Guid AccountUuid, string keyName, LockContext lockContext);
		Task<IWalletKey> LoadKey(Guid AccountUuid, string keyName, LockContext lockContext);
		Task<IWalletKey> LoadKey(Guid AccountUuid, byte ordinal, LockContext lockContext);
		Task<IWalletKey> LoadKey(string keyName, LockContext lockContext);
		Task<IWalletKey> LoadKey(byte ordinal, LockContext lockContext);

		Task<T> LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string keyName, LockContext lockContext)
			where K : class, IWalletKey
			where T : class;

		Task<T> LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, byte ordinal, LockContext lockContext)
			where K : class, IWalletKey
			where T : class;

		Task<T> LoadKey<T>(Func<T, T> selector, Guid accountUuid, string keyName, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(Func<T, T> selector, Guid accountUuid, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(Guid accountUuid, string keyName, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(Guid accountUuid, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(string keyName, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(byte ordinal, LockContext lockContext)
			where T : class, IWalletKey;

		Task UpdateKey(IWalletKey key, LockContext lockContext);
		Task SwapNextKey(IWalletKey key, LockContext lockContext, bool storeHistory = true);
		Task SwapNextKey(Guid accountUUid, string keyName, LockContext lockContext, bool storeHistory = true);
		Task EnsureWalletLoaded(LockContext lockContext);
		Task SetExternalPassphraseHandlers(Delegates.RequestPassphraseDelegate requestPassphraseDelegate, Delegates.RequestKeyPassphraseDelegate requestKeyPassphraseDelegate, Delegates.RequestCopyKeyFileDelegate requestKeyCopyFileDelegate, Delegates.RequestCopyWalletFileDelegate copyWalletDelegate, LockContext lockContext);
		Task SetConsolePassphraseHandlers(LockContext lockContext);
		Task<(SecureString passphrase, bool keysToo)> RequestWalletPassphraseByConsole(LockContext lockContext, int maxTryCount = 10);
		Task<SecureString> RequestKeysPassphraseByConsole(Guid accountUUid, string keyName, LockContext lockContext, int maxTryCount = 10);
		Task<(SecureString passphrase, bool keysToo)> RequestPassphraseByConsole(LockContext lockContext, string passphraseType = "wallet", int maxTryCount = 10);
		Task<SafeArrayHandle> PerformCryptographicSignature(Guid accountUuid, string keyName, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> PerformCryptographicSignature(IWalletKey key, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<IWalletStandardAccountSnapshot> GetStandardAccountSnapshot(AccountId accountId, LockContext lockContext);
		Task<IWalletJointAccountSnapshot> GetJointAccountSnapshot(AccountId accountId, LockContext lockContext);
		Task<(string path, string passphrase, string salt, int iterations)> BackupWallet(LockContext lockContext);
		Task<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, int iterations, LockContext lockContext);
		Task UpdateWalletChainStateSyncStatus(Guid accountUuid, long BlockId, WalletAccountChainState.BlockSyncStatuses blockSyncStatus, LockContext lockContext);
		Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignTransactionXmss(SafeArrayHandle transactionHash, IXmssWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignMessageXmss(Guid accountUuid, SafeArrayHandle message, LockContext lockContext);
		Task<SafeArrayHandle> SignMessageXmss(SafeArrayHandle messageHash, IXmssWalletKey key, LockContext lockContext);
		Task<SafeArrayHandle> SignMessage(SafeArrayHandle messageHash, IWalletKey key, LockContext lockContext);
		Task EnsureWalletKeyIsReady(Guid accountUuid, string keyname, LockContext lockContext);
		Task EnsureWalletKeyIsReady(Guid accountUuid, byte ordinal, LockContext lockContext);
		Task<bool> LoadWallet(CorrelationContext correlationContext, LockContext lockContext, string passphrase = null);

		Task Pause();
		Task Resume();
	}

	public interface IWalletProvider : IWalletProviderWrite, IReadonlyWalletProvider, IUtilityWalletProvider {

		public bool TransactionInProgress(LockContext lockContext);

	}

	public interface IWalletProviderInternal : IWalletProvider {

		public IUserWalletFileInfo WalletFileInfo { get; }
		public Task<IUserWallet> WalletBase(LockContext lockContext);
		public IWalletSerialisationFal SerialisationFal { get; }

		Task<K> PerformWalletTransaction<K>(Func<IWalletProvider, CancellationToken, LockContext, Task<K>> transactionAction, CancellationToken token, LockContext lockContext, Func<IWalletProvider, Func<IWalletProvider, CancellationToken, LockContext, Task>, CancellationToken, LockContext, Task> commitWrapper = null, Func<IWalletProvider, Func<IWalletProvider, CancellationToken, LockContext, Task>, CancellationToken, LockContext, Task> rollbackWrapper = null);
		Task WaitTransactionCompleted();
		Task RequestCopyWallet(CorrelationContext correlationContext, int attempt, LockContext lockContext);
		Task CaptureWalletPassphrase(CorrelationContext correlationContext, int attempt, LockContext lockContext);
		public void EnsureKeyFileIsPresent(Guid accountUuid, string keyName, int attempt, LockContext lockContext);
		public bool IsKeyFileIsPresent(Guid accountUuid, string keyName, int attempt, LockContext lockContext);
		public void EnsureKeyFileIsPresent(Guid accountUuid, byte ordinal, int attempt, LockContext lockContext);
		public void ClearWalletPassphrase();
		Task RequestCopyKeyFile(CorrelationContext correlationContext, Guid accountUuid, string keyName, int attempt, LockContext lockContext);
		Task CaptureKeyPassphrase(CorrelationContext correlationContext, Guid accountUuid, string keyName, int attempt, LockContext lockContext);
		public void EnsureKeyPassphrase(Guid accountUuid, string keyName, int attempt, LockContext lockContext);
		public void EnsureKeyPassphrase(Guid accountUuid, byte ordinal, int attempt, LockContext lockContext);
		public bool IsKeyPassphraseValid(Guid accountUuid, string keyName, int attempt, LockContext lockContext);
		public void SetKeysPassphrase(Guid accountUuid, string keyname, string passphrase, LockContext lockContext);
		public void SetKeysPassphrase(Guid accountUuid, string keyname, SecureString passphrase, LockContext lockContext);
		public void SetWalletPassphrase(string passphrase, LockContext lockContext);
		public void SetWalletPassphrase(SecureString passphrase, LockContext lockContext);
		public void ClearWalletKeyPassphrase(Guid accountUuid, string keyName, LockContext lockContext);

		public Task EnsureWalletFileIsPresent(LockContext lockContext);
		Task EnsureWalletPassphrase(LockContext lockContext, string passphrase = null);

	}

	public abstract class WalletProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IWalletProviderInternal
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		/// <summary>
		///     The macimum number of blocks we can keep in memory for our wallet sync
		/// </summary>
		public const int MAXIMUM_SYNC_BLOCK_CACHE_COUNT = 100;

		/// <summary>
		///     if the difference between the chain block height and the wallet height is less than this number, we will do an
		///     extra effort and cache blocks that are incoming via sync.
		///     this will minimise the amount of reads we will do to the disk
		/// </summary>
		public const int MAXIMUM_EXTRA_BLOCK_CACHE_AMOUNT = 1000;

		public const string PID_LOCK_FILE = ".lock";

		public event Func<Task> WalletIsLoaded;

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		protected readonly string chainPath;

		protected readonly FileSystemWrapper fileSystem;

		protected readonly IGlobalsService globalsService;

		protected readonly RecursiveAsyncLock locker = new RecursiveAsyncLock();

		protected readonly BlockchainServiceSet serviceSet;

		/// <summary>
		/// if paused, it is not safe to run wallet operations and transactions
		/// </summary>
		private bool paused = false;

		/// <summary>
		///     the synthetized blocks to know which transactions concern us
		/// </summary>
		private readonly ConcurrentDictionary<long, SynthesizedBlock> syncBlockCache = new ConcurrentDictionary<long, SynthesizedBlock>();

		protected IWalletSerializationTransactionExtension currentTransaction;

		protected bool shutdownRequest = false;

		public WalletProvider(string chainPath, CENTRAL_COORDINATOR centralCoordinator) {
			this.chainPath = chainPath;
			this.centralCoordinator = centralCoordinator;

			this.globalsService = centralCoordinator.BlockchainServiceSet.GlobalsService;

			this.fileSystem = centralCoordinator.FileSystem;

			this.serviceSet = centralCoordinator.BlockchainServiceSet;
			centralCoordinator.ShutdownRequested += this.CentralCoordinatorOnShutdownRequested;
		}

		protected abstract ICardUtils CardUtils { get; }

		public Task<IUserWallet> WalletBase(LockContext lockContext) => this.WalletFileInfo.WalletBase(lockContext);

		public IWalletSerialisationFal SerialisationFal { get; private set; }

		public IUserWalletFileInfo WalletFileInfo { get; private set; }

		public bool IsWalletLoaded => this.WalletFileInfo?.IsLoaded ?? false;

		public Task<bool> IsWalletEncrypted(LockContext lockContext) => Task.FromResult(this.WalletFileInfo.WalletSecurityDetails.EncryptWallet);

		public async Task<bool> IsWalletAccountLoaded(LockContext lockContext) => this.IsWalletLoaded && (await this.WalletBase(lockContext).ConfigureAwait(false)).GetActiveAccount() != null;

		public Task<bool> WalletFileExists(LockContext lockContext) => Task.FromResult(this.WalletFileInfo.FileExists);

		public bool TransactionInProgress(LockContext lockContext) {

			using(this.locker.Lock(lockContext)) {
				return this.currentTransaction != null;
			}
		}

		/// <summary>
		/// Wait for any transaction in progress to complete if any is underway. it will return ONLY when the transaction is completed safely
		/// </summary>
		public Task WaitTransactionCompleted() {
			LockContext innerLockContext = null;

			if(this.TransactionInProgress(innerLockContext)) {

				while(true) {
					if(!this.TransactionInProgress(innerLockContext)) {
						// we are ready to go
						break;
					}

					// we have to wait a little more
					Thread.Sleep(500);
				}
			}

			return Task.CompletedTask;
		}

		private void CentralCoordinatorOnShutdownRequested(ConcurrentBag<Task> beacons) {
			this.shutdownRequest = true;

			// ok, if this happens while we are syncing, we ask for a grace period until we are ready to clean exit
			beacons.Add(this.WaitTransactionCompleted());
		}

		public async Task<K> PerformWalletTransaction<K>(Func<IWalletProvider, CancellationToken, LockContext, Task<K>> transactionAction, CancellationToken token, LockContext lockContext, Func<IWalletProvider, Func<IWalletProvider, CancellationToken, LockContext, Task>, CancellationToken, LockContext, Task> commitWrapper = null, Func<IWalletProvider, Func<IWalletProvider, CancellationToken, LockContext, Task>, CancellationToken, LockContext, Task> rollbackWrapper = null) {

			if(transactionAction == null) {
				return default;
			}

			if(this.shutdownRequest) {
				throw new OperationCanceledException();
			}

			token.ThrowIfCancellationRequested();

			try {
				LockContext innerLockContext = null;

				using(var handleLocker = this.locker.Lock(innerLockContext)) {
					if(this.TransactionInProgress(handleLocker.Context)) {
						throw new NotReadyForProcessingException();
					}

					if(this.paused) {
						throw new NotReadyForProcessingException();
					}

					this.currentTransaction = this.SerialisationFal.BeginTransaction();
				}

				// let's make sure we catch implicit disposes that we did not call for through disposing
				this.currentTransaction.Disposed += async sessionId => {
					if(this.currentTransaction != null && this.currentTransaction.SessionId == sessionId) {
						// ok, thats us, our session is now disposed.

						using(var handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
							this.currentTransaction = null;

							// reset the files, since the underlying files have probably changed
							await Repeater.RepeatAsync(() => {
								return this.WalletFileInfo.ReloadFileBytes(handle);

							}, 2).ConfigureAwait(false);
						}
					}
				};

				token.ThrowIfCancellationRequested();

				K result = await transactionAction(this, token, lockContext).ConfigureAwait(false);

				token.ThrowIfCancellationRequested();

				async Task Commit() {
					await this.SaveWallet(lockContext).ConfigureAwait(false);

					this.currentTransaction.CommitTransaction();
				}

				if(commitWrapper != null) {
					await commitWrapper(this, (prov, ct, lc) => Commit(), token, lockContext).ConfigureAwait(false);
				} else {
					await Commit().ConfigureAwait(false);
				}

				return result;
			} catch {
				if(rollbackWrapper != null) {
					await rollbackWrapper(this, async (prov, ct, lc) => {
						this.currentTransaction?.RollbackTransaction();
					}, token, lockContext).ConfigureAwait(false);
				} else {
					this.currentTransaction?.RollbackTransaction();
				}

				// just end here
				throw;
			} finally {
				using(this.locker.Lock()) {
					this.currentTransaction = null;
				}
			}

		}

		/// <summary>
		///     This is the lowest common denominator between the wallet accounts syncing block height
		/// </summary>
		public async Task<long?> LowestAccountBlockSyncHeight(LockContext lockContext) {

			if(!this.WalletFileInfo.IsLoaded) {
				return null;
			}

			if(this.WalletFileInfo.Accounts.Any()) {
				return (await this.WalletFileInfo.Accounts.Values.SelectAsync(async a => (await a.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false)).LastBlockSynced).ConfigureAwait(false)).Min();
			}

			return 0;
		}

		public async Task<bool?> Synced(LockContext lockContext) {

			var lowestAccountBlockSyncHeight = await this.LowestAccountBlockSyncHeight(lockContext).ConfigureAwait(false);

			if(!lowestAccountBlockSyncHeight.HasValue) {
				return null;
			}

			return this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight <= lowestAccountBlockSyncHeight.Value;

		}

		/// <summary>
		///     since this provider is created before the central coordinator is fully created, we must be initialized after.
		/// </summary>
		public override async Task Initialize(LockContext lockContext) {
			await base.Initialize(lockContext).ConfigureAwait(false);

			this.SerialisationFal = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainDalCreationFactoryBase.CreateWalletSerialisationFal(this.centralCoordinator, this.GetChainDirectoryPath(), this.fileSystem);

			this.WalletFileInfo = this.SerialisationFal.CreateWalletFileInfo();

			//this.WalletFileInfo.WalletSecurityDetails.EncryptWallet = this.encryptWallet;
			//this.WalletFileInfo.WalletSecurityDetails.EncryptWalletKeys = this.encryptKeys;
			//this.WalletFileInfo.WalletSecurityDetails.EncryptWalletKeysIndividually = this.;

			// decide by which method we will request the wallet passphrases
			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().PassphraseCaptureMethod == AppSettingsBase.PassphraseQueryMethod.Console) {
				await this.SetConsolePassphraseHandlers(lockContext).ConfigureAwait(false);
			}
		}

		public event Delegates.RequestCopyWalletFileDelegate CopyWalletRequest;
		public event Delegates.RequestPassphraseDelegate WalletPassphraseRequest;
		public event Delegates.RequestKeyPassphraseDelegate WalletKeyPassphraseRequest;
		public event Delegates.RequestCopyKeyFileDelegate WalletCopyKeyFileRequest;

		public string GetChainStorageFilesPath() {
			return this.SerialisationFal.GetChainStorageFilesPath();
		}

		public string GetChainDirectoryPath() {
			return Path.Combine(this.GetSystemFilesDirectoryPath(), this.chainPath);
		}

		public async Task<bool> WalletContainsAccount(Guid accountUuid, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).Accounts.ContainsKey(accountUuid);

		}

		public Task<IAccountFileInfo> GetAccountFileInfo(Guid accountUuid, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return Task.FromResult(this.WalletFileInfo.Accounts[accountUuid]);

		}

		/// <summary>
		///     gets the list of accounts that can be synced since they match the provided block id
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public async Task<List<IWalletAccount>> GetWalletSyncableAccounts(long blockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var keys = (await this.WalletFileInfo.Accounts.WhereAsync(async a => {

					           var chainState = (await a.Value.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false));

					           return chainState.LastBlockSynced;
				           }, e => e == blockId - 1).ConfigureAwait(false)).Select(a => a.Key).ToList();

			return (await this.GetAccounts(lockContext).ConfigureAwait(false)).Where(a => keys.Contains(a.AccountUuid)).ToList();

		}

		/// <summary>
		///     get all accounts, including rejected ones
		/// </summary>
		/// <returns></returns>
		public async Task<List<IWalletAccount>> GetAllAccounts(LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccounts();

		}

		/// <summary>
		///     get all active acounts
		/// </summary>
		/// <returns></returns>
		public async Task<List<IWalletAccount>> GetAccounts(LockContext lockContext) {
			return (await this.GetAllAccounts(lockContext).ConfigureAwait(false)).Where(a => a.Status != Enums.PublicationStatus.Rejected).ToList();

		}

		public async Task<Guid> GetAccountUuid(LockContext lockContext) {
			return (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).AccountUuid;
		}

		public async Task<AccountId> GetPublicAccountId(LockContext lockContext) {
			var account = (await this.GetActiveAccount(lockContext).ConfigureAwait(false));

			return await this.GetPublicAccountId(account.AccountUuid, lockContext).ConfigureAwait(false);
		}

		public async Task<AccountId> GetPublicAccountId(Guid accountUuid, LockContext lockContext) {

			var account = await this.GetWalletAccount(accountUuid, lockContext).ConfigureAwait(false);

			if(account == null || !(await this.IsAccountPublished(account.AccountUuid, lockContext).ConfigureAwait(false))) {
				return null;
			}

			return account.PublicAccountId;
		}

		public async Task<AccountId> GetAccountUuidHash(LockContext lockContext) {
			return (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).AccountUuidHash;
		}

		public async Task<bool> IsDefaultAccountPublished(LockContext lockContext) => (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).Status == Enums.PublicationStatus.Published;

		public async Task<bool> IsAccountPublished(Guid accountUuid, LockContext lockContext) {
			var account = await this.GetWalletAccount(accountUuid, lockContext).ConfigureAwait(false);

			return account != null && account.Status == Enums.PublicationStatus.Published;
		}

		/// <summary>
		///     Return the base wallet directory, not scopped by chain
		/// </summary>
		/// <returns></returns>
		public string GetSystemFilesDirectoryPath() {

			return GlobalsService.GetGeneralSystemFilesDirectoryPath();
		}

		public async Task<IWalletAccount> GetActiveAccount(LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).GetActiveAccount();

		}

		public async Task<bool> SetActiveAccount(string name, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).SetActiveAccount(name);

		}

		public async Task<bool> SetActiveAccount(Guid accountUuid, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).SetActiveAccount(accountUuid);

		}

		public async Task<IWalletAccount> GetWalletAccount(Guid id, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(id);

		}

		public async Task<IWalletAccount> GetWalletAccount(string name, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(name);

		}

		public async Task<IWalletAccount> GetWalletAccount(AccountId accountId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountId);
		}

		public virtual Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null) {
			return this.CreateNewCompleteWallet(correlationContext, "", encryptWallet, encryptKey, encryptKeysIndividually, passphrases, lockContext, accountCreatedCallback);
		}

		public async Task<IWalletStandardAccountSnapshot> GetStandardAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return (await this.GetAccountSnapshot(accountId, lockContext).ConfigureAwait(false)) as IWalletStandardAccountSnapshot;
		}

		public async Task<IWalletJointAccountSnapshot> GetJointAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return (await this.GetAccountSnapshot(accountId, lockContext).ConfigureAwait(false)) as IWalletJointAccountSnapshot;
		}

		public async Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount localAccount = await this.GetWalletAccount(accountSnapshot.AccountId.ToAccountId(), lockContext).ConfigureAwait(false);

			if(localAccount == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			await this.UpdateWalletSnapshot(accountSnapshot, localAccount.AccountUuid, lockContext).ConfigureAwait(false);
		}

		public async Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, Guid AccountUuid, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			IAccountFileInfo walletAccountInfo = null;

			if(this.WalletFileInfo.Accounts.ContainsKey(AccountUuid)) {
				walletAccountInfo = this.WalletFileInfo.Accounts[AccountUuid];
			}

			var snapshot = await (walletAccountInfo?.WalletSnapshotInfo.WalletAccountSnapshot(lockContext)).ConfigureAwait(false);

			if(snapshot == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			this.CardUtils.Copy(accountSnapshot, snapshot);

			walletAccountInfo?.WalletSnapshotInfo.SetWalletAccountSnapshot(snapshot);
		}

		public Task UpdateWalletSnapshotFromDigest(IAccountSnapshotDigestChannelCard accountCard, LockContext lockContext) {
			if(accountCard is IStandardAccountSnapshotDigestChannelCard simpleAccountSnapshot) {
				return this.UpdateWalletSnapshotFromDigest(simpleAccountSnapshot, lockContext);
			} else if(accountCard is IJointAccountSnapshotDigestChannelCard jointAccountSnapshot) {
				return this.UpdateWalletSnapshotFromDigest(jointAccountSnapshot, lockContext);
			} else {
				throw new InvalidCastException();
			}
		}

		/// <summary>
		///     ok, we update our wallet snapshot from the digest
		/// </summary>
		/// <param name="accountCard"></param>
		public async Task UpdateWalletSnapshotFromDigest(IStandardAccountSnapshotDigestChannelCard accountCard, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount localAccount = await this.GetWalletAccount(accountCard.AccountId.ToAccountId(), lockContext).ConfigureAwait(false);

			if(localAccount == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			IAccountFileInfo walletAccountInfo = null;

			if(!this.WalletFileInfo.Accounts.ContainsKey(localAccount.AccountUuid)) {
				await this.CreateNewWalletStandardAccountSnapshot(localAccount, lockContext).ConfigureAwait(false);
			}

			walletAccountInfo = this.WalletFileInfo.Accounts[localAccount.AccountUuid];

			var snapshot = await (walletAccountInfo?.WalletSnapshotInfo.WalletAccountSnapshot(lockContext)).ConfigureAwait(false);

			if(snapshot == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			this.CardUtils.Copy(accountCard, snapshot);

			walletAccountInfo?.WalletSnapshotInfo.SetWalletAccountSnapshot(snapshot);
		}

		/// <summary>
		///     ok, we update our wallet snapshot from the digest
		/// </summary>
		/// <param name="accountCard"></param>
		public async Task UpdateWalletSnapshotFromDigest(IJointAccountSnapshotDigestChannelCard accountCard, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount localAccount = (await this.GetAccounts(lockContext).ConfigureAwait(false)).SingleOrDefault(a => a.GetAccountId() == accountCard.AccountId.ToAccountId());

			if(localAccount == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			IAccountFileInfo walletAccountInfo = null;

			if(!this.WalletFileInfo.Accounts.ContainsKey(localAccount.AccountUuid)) {
				await this.CreateNewWalletJointAccountSnapshot(localAccount, lockContext).ConfigureAwait(false);
			}

			walletAccountInfo = this.WalletFileInfo.Accounts[localAccount.AccountUuid];

			var snapshot = await (walletAccountInfo?.WalletSnapshotInfo.WalletAccountSnapshot(lockContext)).ConfigureAwait(false);

			if(snapshot == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			this.CardUtils.Copy(accountCard, snapshot);

			walletAccountInfo?.WalletSnapshotInfo.SetWalletAccountSnapshot(snapshot);
		}

		public async Task UpdateWalletChainStateSyncStatus(Guid accountUuid, long BlockId, WalletAccountChainState.BlockSyncStatuses blockSyncStatus, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var chainState = await this.WalletFileInfo.Accounts[accountUuid].WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);
			chainState.LastBlockSynced = BlockId;
			chainState.BlockSyncStatus = (int) blockSyncStatus;
		}

		public virtual async Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null) {

			try {
				SystemEventGenerator.WalletCreationStepSet walletCreationStepSet = new SystemEventGenerator.WalletCreationStepSet();

				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletCreationStartedEvent(), correlationContext);
				Log.Information("Creating a new wallet");

				string walletPassphrase = null;

				if(passphrases?.ContainsKey(0) ?? false) {
					walletPassphrase = passphrases[0];
				}

				await this.CreateNewEmptyWallet(correlationContext, encryptWallet, walletPassphrase, walletCreationStepSet, lockContext).ConfigureAwait(false);

				await this.CreateNewCompleteAccount(correlationContext, accountName, encryptKey, encryptKeysIndividually, passphrases, lockContext, walletCreationStepSet, accountCreatedCallback).ConfigureAwait(false);

				await this.SaveWallet(lockContext).ConfigureAwait(false);

				Log.Information("WalletBase successfully created and loaded");
				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletCreationEndedEvent(), correlationContext);

				await this.centralCoordinator.RequestWalletSync().ConfigureAwait(false);

				return true;
			} catch(Exception ex) {
				try {
					// delete the folder
					if(Directory.Exists(this.WalletFileInfo.WalletPath)) {
						Directory.Delete(this.WalletFileInfo.WalletPath, true);
					}
				} catch(Exception ex2) {
					Log.Error("Failed to delete faulty wallet files.");
				}

				throw new ApplicationException($"Failed to create wallet", ex);
			}

		}

		public virtual async Task<bool> CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, Action<IWalletAccount> accountCreatedCallback = null) {

			try {
				SystemEventGenerator.AccountCreationStepSet accountCreationStepSet = new SystemEventGenerator.AccountCreationStepSet();
				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.AccountCreationStartedEvent(), correlationContext);
				this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.AccountCreationStartedStep, correlationContext);

				IWalletAccount account = await this.CreateNewAccount(accountName, encryptKeys, encryptKeysIndividually, correlationContext, walletCreationStepSet, accountCreationStepSet, lockContext, true).ConfigureAwait(false);

				Log.Information($"Your new default account Uuid is '{account.AccountUuid}'");

				if(accountCreatedCallback != null) {
					accountCreatedCallback(account);
				}

				// now create the keys
				await this.CreateStandardAccountKeys(account.AccountUuid, passphrases, correlationContext, walletCreationStepSet, accountCreationStepSet, lockContext).ConfigureAwait(false);

				this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.AccountCreationEndedStep, correlationContext);
				this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.AccountCreationEndedEvent(account.AccountUuid), correlationContext);

				return true;
			} catch(Exception ex) {
				throw new ApplicationException($"Failed to create account.", ex);
			}
		}

		public virtual Task<bool> CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext) {

			return this.CreateNewCompleteAccount(correlationContext, accountName, encryptKeys, encryptKeysIndividually, passphrases, lockContext, null);
		}

		/// <summary>
		///     This method will create a brand new empty wallet
		/// </summary>
		/// <param name="encrypt"></param>
		/// <exception cref="ApplicationException"></exception>
		public async Task CreateNewEmptyWallet(CorrelationContext correlationContext, bool encryptWallet, string passphrase, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, LockContext lockContext) {
			if(this.IsWalletLoaded) {
				throw new ApplicationException("Wallet is already created");
			}

			if(await this.WalletFileExists(lockContext).ConfigureAwait(false)) {
				throw new ApplicationException("A wallet file already exists. we can not overwrite an existing file. delete it and try again");
			}

			this.WalletFileInfo.WalletSecurityDetails.EncryptWallet = encryptWallet;

			if(encryptWallet) {
				if(string.IsNullOrWhiteSpace(passphrase)) {
					throw new InvalidOperationException();
				}

				this.SetWalletPassphrase(passphrase, lockContext);
			}

			IUserWallet wallet = this.CreateNewWalletEntry(lockContext);

			// set the wallet version

			wallet.Major = GlobalSettings.SoftwareVersion.Major;
			wallet.Minor = GlobalSettings.SoftwareVersion.Minor;
			wallet.Revision = GlobalSettings.SoftwareVersion.Revision;

			wallet.NetworkId = GlobalSettings.Instance.NetworkId;
			wallet.ChainId = this.centralCoordinator.ChainId.Value;

			await this.WalletFileInfo.CreateEmptyFileBase(wallet, lockContext).ConfigureAwait(false);
		}

		public async Task<(string path, string passphrase, string salt, int iterations)> BackupWallet(LockContext lockContext) {

			// first, let's generate a passphrase

			await this.EnsureWalletFileIsPresent(lockContext).ConfigureAwait(false);

			string passphrase = string.Join(" ", WordsGenerator.GenerateRandomWords(4));

			(string path, string salt, int iterations) results = this.SerialisationFal.BackupWallet(passphrase);

			return (results.path, passphrase, results.salt, results.iterations);
		}

		public async Task<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, int iterations, LockContext lockContext) {
			return this.SerialisationFal.RestoreWalletFromBackup(backupsPath, passphrase, salt, iterations);
		}

		/// <summary>
		///     Load the wallet
		/// </summary>
		public async Task<bool> LoadWallet(CorrelationContext correlationContext, LockContext lockContext, string passphrase = null) {
			if(this.IsWalletLoaded) {
				Log.Warning("Wallet already loaded");

				return false;
			}

			Log.Warning("Ensuring PID protection");

			await this.EnsurePIDLock().ConfigureAwait(false);

			Log.Warning("Loading wallet");

			if(!this.WalletFileInfo.FileExists) {
				Log.Warning("Failed to load wallet, no wallet file exists");

				return false;
			}

			this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletLoadingStartedEvent(), correlationContext);

			await WalletFileInfo.LoadFileSecurityDetails(lockContext).ConfigureAwait(false);

			try {

				await this.EnsureWalletFileIsPresent(lockContext).ConfigureAwait(false);
				await this.EnsureWalletPassphrase(lockContext, passphrase).ConfigureAwait(false);

				await this.WalletFileInfo.Load(lockContext).ConfigureAwait(false);

				var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);

				if(walletbase == null) {
					throw new ApplicationException("The wallet is corrupt. please recreate or fix.");
				}

				if(walletbase.ChainId != this.centralCoordinator.ChainId) {

					throw new ApplicationException("The wallet was created for a different blockchain");
				}

				if(walletbase.NetworkId != GlobalSettings.Instance.NetworkId) {

					throw new ApplicationException("The wallet was created for a different network Id. Can not be used");
				}

				// now restore the skeleton of the unloaded file infos for each accounts
				foreach(IWalletAccount account in walletbase.GetAccounts()) {

					this.WalletFileInfo.Accounts.Add(account.AccountUuid, this.CreateNewAccountFileInfo(account, lockContext));
				}

			} catch(FileNotFoundException e) {

				await this.WalletFileInfo.Reset(lockContext).ConfigureAwait(false);
				this.WalletFileInfo = this.SerialisationFal.CreateWalletFileInfo();

				Log.Warning("Failed to load wallet, no wallet file exists");

				// for a missing file, we simply return false, so we can create it
				return false;
			} catch(Exception e) {

				await this.WalletFileInfo.Reset(lockContext).ConfigureAwait(false);
				this.WalletFileInfo = this.SerialisationFal.CreateWalletFileInfo();

				Log.Error(e, "Failed to load wallet");

				throw;
			}

			Log.Warning("Wallet successfully loaded");

			if(this.WalletIsLoaded != null) {
				await this.WalletIsLoaded().ConfigureAwait(false);
			}

			this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletLoadingEndedEvent(), correlationContext);

			return true;

		}

		public Task RemovePIDLock() {
			try {
				string pidfile = this.GetPIDFilePath();

				if(this.fileSystem.FileExists(pidfile)) {

					this.fileSystem.DeleteFile(pidfile);
				}
			} catch {
				// do nothing
			}

			return Task.CompletedTask;
		}

		/// <summary>
		///     Change the wallet encryption
		/// </summary>
		/// <param name="encryptWallet"></param>
		/// <param name="encryptKeys"></param>
		public async Task ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext) {

			await this.WalletFileInfo.LoadFileSecurityDetails(lockContext).ConfigureAwait(false);

			bool walletEncryptionChange = this.WalletFileInfo.WalletSecurityDetails.EncryptWallet != encryptWallet;

			try {
				if(encryptWallet && walletEncryptionChange) {
					await this.EnsureWalletFileIsPresent(lockContext).ConfigureAwait(false);
					await this.EnsureWalletPassphrase(lockContext).ConfigureAwait(false);
				}

				var chaningAccounts = new List<IWalletAccount>();

				foreach(IWalletAccount account in (await this.WalletBase(lockContext).ConfigureAwait(false)).Accounts.Values) {

					AccountPassphraseDetails accountSecurityDetails = this.WalletFileInfo.Accounts[account.AccountUuid].AccountSecurityDetails;
					bool keysEncryptionChange = accountSecurityDetails.EncryptWalletKeys != encryptKeys;

					if(keysEncryptionChange) {

						chaningAccounts.Add(account);

						// ensure key files are present
						foreach(KeyInfo keyInfo in account.Keys) {
							this.EnsureKeyFileIsPresent(account.AccountUuid, keyInfo, 1, lockContext);
						}
					}
				}

				if(!walletEncryptionChange && !chaningAccounts.Any()) {
					Log.Information("No encryption changes for the wallet. Nothing to do.");

					return;
				}

				// load the complete structures that will be changed
				await this.WalletFileInfo.LoadComplete(walletEncryptionChange, chaningAccounts.Any(), lockContext).ConfigureAwait(false);

				if(walletEncryptionChange) {
					this.WalletFileInfo.WalletSecurityDetails.EncryptWallet = encryptWallet;

					// now we ensure accounts are set up correctly
					foreach(IWalletAccount account in (await this.WalletBase(lockContext).ConfigureAwait(false)).Accounts.Values) {
						if(this.WalletFileInfo.WalletSecurityDetails.EncryptWallet) {

							account.InitializeNewEncryptionParameters(this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration);

						} else {

							account.ClearEncryptionParameters();

						}
					}

					await this.WalletFileInfo.ChangeEncryption(lockContext).ConfigureAwait(false);
				}

				// now we ensure accounts are set up correctly
				foreach(IWalletAccount account in chaningAccounts) {

					AccountPassphraseDetails accountSecurityDetails = this.WalletFileInfo.Accounts[account.AccountUuid].AccountSecurityDetails;

					accountSecurityDetails.EncryptWalletKeys = encryptKeys;
					accountSecurityDetails.EncryptWalletKeysIndividually = encryptKeysIndividually;

					foreach(KeyInfo keyInfo in account.Keys) {
						keyInfo.EncryptionParameters = null;

						if(accountSecurityDetails.EncryptWalletKeys) {

							keyInfo.EncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration);
						}
					}
				}

				await this.WalletFileInfo.ChangeKeysEncryption(lockContext).ConfigureAwait(false);

			} catch(Exception e) {
				Log.Verbose("error occured", e);

				//TODO: what to do here?
				throw;
			}

		}

		public async Task SaveWallet(LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			await this.EnsureWalletPassphrase(lockContext).ConfigureAwait(false);

			await this.WalletFileInfo.Save(lockContext).ConfigureAwait(false);

		}

		/// <summary>
		///     Create a new account and keys
		/// </summary>
		public virtual async Task<IWalletAccount> CreateNewAccount(string name, bool encryptKeys, bool encryptKeysIndividually, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, LockContext lockContext, bool setactive = false) {

			this.EnsureWalletIsLoaded();

			var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);

			if(walletbase.GetAccount(name) != null) {
				throw new ApplicationException("Account with name already exists");
			}

			IWalletAccount account = this.CreateNewWalletAccountEntry(lockContext);

			if(string.IsNullOrEmpty(name)) {
				name = UserWallet.DEFAULT_ACCOUNT;
			}

			this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.CreatingFiles, correlationContext);
			this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingFiles, correlationContext);

			account.InitializeNew(name, this.centralCoordinator.BlockchainServiceSet, Enums.AccountTypes.Standard);

			account.KeysEncrypted = encryptKeys;
			account.KeysEncryptedIndividually = encryptKeysIndividually;

			if(this.WalletFileInfo.WalletSecurityDetails.EncryptWallet) {
				// generate encryption parameters
				account.InitializeNewEncryptionParameters(this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration);
			}

			// make it active

			if(setactive || walletbase.Accounts.Count == 0) {
				walletbase.ActiveAccount = account.AccountUuid;
			}

			walletbase.Accounts.Add(account.AccountUuid, account);

			// ensure the key holder is created
			IAccountFileInfo accountFileInfo = this.CreateNewAccountFileInfo(account, lockContext);

			// now create the file connection entry to map the new account
			this.WalletFileInfo.Accounts.Add(account.AccountUuid, accountFileInfo);

			// and now create the keylog
			await accountFileInfo.WalletKeyLogsInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and now create the key history
			await accountFileInfo.WalletKeyHistoryInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and now create the chainState

			WalletAccountChainState chainState = this.CreateNewWalletAccountChainStateEntry(lockContext);
			chainState.AccountUuid = account.AccountUuid;

			// its a brand new account, there is nothing to sync until right now.
			chainState.LastBlockSynced = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;

			await accountFileInfo.WalletChainStatesInfo.CreateEmptyFile(chainState, lockContext).ConfigureAwait(false);

			await this.PrepareAccountInfos(accountFileInfo, lockContext).ConfigureAwait(false);

			this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.SavingWallet, correlationContext);

			return account;
		}

		public async Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, LockContext lockContext) {

			return await this.CreateNewWalletStandardAccountSnapshot(account, await this.CreateNewWalletStandardAccountSnapshotEntry(lockContext).ConfigureAwait(false), lockContext).ConfigureAwait(false);
		}

		public async Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);

			if(!walletbase.Accounts.ContainsKey(account.AccountUuid)) {
				//TODO: what to do here?
				throw new ApplicationException("Newly confirmed account is not in the wallet");
			}

			// lets fill the data from our wallet
			this.FillStandardAccountSnapshot(account, accountSnapshot);

			IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid];
			await accountFileInfo.WalletSnapshotInfo.InsertNewSnapshotBase(accountSnapshot, lockContext).ConfigureAwait(false);

			return accountSnapshot;
		}

		public async Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, LockContext lockContext) {

			return await this.CreateNewWalletJointAccountSnapshot(account, await this.CreateNewWalletJointAccountSnapshotEntry(lockContext).ConfigureAwait(false), lockContext).ConfigureAwait(false);
		}

		public async Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);

			if(!walletbase.Accounts.ContainsKey(account.AccountUuid)) {
				//TODO: what to do here?
				throw new ApplicationException("Newly confirmed account is not in the wallet");
			}

			// lets fill the data from our wallet
			this.FillJointAccountSnapshot(account, accountSnapshot);

			IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid];
			await accountFileInfo.WalletSnapshotInfo.InsertNewJointSnapshotBase(accountSnapshot, lockContext).ConfigureAwait(false);

			return accountSnapshot;
		}

		public Task InsertKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, KeyUseIndexSet keyUseIndexSet, byte keyOrdinalId, LockContext lockContext) {
			return this.InsertKeyLogEntry(account, transactionId.ToString(), Enums.BlockchainEventTypes.Transaction, keyOrdinalId, keyUseIndexSet, lockContext);
		}

		public Task InsertKeyLogBlockEntry(IWalletAccount account, BlockId blockId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex, LockContext lockContext) {
			return this.InsertKeyLogEntry(account, blockId.ToString(), Enums.BlockchainEventTypes.Block, keyOrdinalId, keyUseIndex, lockContext);
		}

		public Task InsertKeyLogDigestEntry(IWalletAccount account, int digestId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex, LockContext lockContext) {
			return this.InsertKeyLogEntry(account, digestId.ToString(), Enums.BlockchainEventTypes.Digest, keyOrdinalId, keyUseIndex, lockContext);
		}

		public async Task InsertKeyLogEntry(IWalletAccount account, string eventId, Enums.BlockchainEventTypes eventType, byte keyOrdinalId, KeyUseIndexSet keyUseIndex, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletAccountKeyLog walletAccountKeyLog = this.CreateNewWalletAccountKeyLogEntry(lockContext);

				walletAccountKeyLog.Timestamp = DateTime.UtcNow;
				walletAccountKeyLog.EventId = eventId;
				walletAccountKeyLog.EventType = (byte) eventType;
				walletAccountKeyLog.KeyOrdinalId = keyOrdinalId;
				walletAccountKeyLog.KeyUseIndex = keyUseIndex;

				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletKeyLogsInfo;

				await keyLogFileInfo.InsertKeyLogEntry(walletAccountKeyLog, lockContext).ConfigureAwait(false);
			}
		}

		public async Task ConfirmKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, KeyUseIndexSet keyUseIndexSet, long confirmationBlockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletKeyLogsInfo;
				await keyLogFileInfo.ConfirmKeyLogTransactionEntry(transactionId, keyUseIndexSet, confirmationBlockId, lockContext).ConfigureAwait(false);

			}
		}

		public async Task ConfirmKeyLogBlockEntry(IWalletAccount account, BlockId blockId, long confirmationBlockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletKeyLogsInfo;
				await keyLogFileInfo.ConfirmKeyLogBlockEntry(confirmationBlockId, lockContext).ConfigureAwait(false);

			}
		}

		public Task<bool> KeyLogTransactionExists(IWalletAccount account, TransactionId transactionId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletKeyLogsInfo;

				return keyLogFileInfo.KeyLogTransactionExists(transactionId, lockContext);

			}

			throw new ApplicationException("Keylog is not enabled.");
		}

		public IWalletKey CreateBasicKey(string name, Enums.KeyTypes keyType) {
			IWalletKey key = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletKey(keyType);

			key.Id = Guid.NewGuid();
			key.Name = name;
			key.CreatedTime = DateTime.UtcNow.Ticks;

			return key;

		}

		public T CreateBasicKey<T>(string name, Enums.KeyTypes keyType)
			where T : IWalletKey {

			T key = (T) this.CreateBasicKey(name, keyType);

			return key;
		}

		public void HashKey(IWalletKey key) {

			// lets generate the hash of this key. this hash can be used as a unique key in public uses. Still, data must be encrypted!

			using HashNodeList nodeList = new HashNodeList();

			// lets add three random nonces
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());

			nodeList.Add(key.GetStructuresArray());

			// lets add three random nonces
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());

			key.Hash = HashingUtils.HashxxTree(nodeList);

		}

		public Task<bool> AllAccountsWalletKeyLogSet(SynthesizedBlock block, LockContext lockContext) {
			return this.AllAccountsHaveSyncStatus(block, WalletAccountChainState.BlockSyncStatuses.KeyLogSynced, lockContext);
		}

		public Task<IWalletAccountSnapshot> GetWalletFileInfoAccountSnapshot(Guid accountUuid, LockContext lockContext) {
			if(!this.WalletFileInfo.Accounts.ContainsKey(accountUuid)) {
				return Task.FromResult((IWalletAccountSnapshot) null);
			}

			return this.WalletFileInfo.Accounts[accountUuid].WalletSnapshotInfo.WalletAccountSnapshot(lockContext);
		}

		public Task<bool> AllAccountsUpdatedWalletBlock(SynthesizedBlock block, LockContext lockContext) {
			return this.AllAccountsUpdatedWalletBlock(block, block.BlockId - 1, lockContext);
		}

		public async Task<bool> AllAccountsUpdatedWalletBlock(SynthesizedBlock block, long previousBlockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			// all accounts have been synced for previous block and if any at current, they have been set for this block
			return !(await this.GetWalletSyncableAccounts(previousBlockId, lockContext).ConfigureAwait(false)).Any() && (!(await this.GetWalletSyncableAccounts(block.BlockId, lockContext).ConfigureAwait(false)).Any() || (await AllAccountsHaveSyncStatus(block, WalletAccountChainState.BlockSyncStatuses.BlockHeightUpdated, lockContext).ConfigureAwait(false)));

		}

		public async Task<bool> AllAccountsHaveSyncStatus(SynthesizedBlock block, WalletAccountChainState.BlockSyncStatuses status, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var syncableAccounts = await this.GetWalletSyncableAccounts(block.BlockId, lockContext).ConfigureAwait(false);

			if(!syncableAccounts.Any()) {
				return false;
			}

			foreach(var a in syncableAccounts) {
				var chainState = await this.WalletFileInfo.Accounts[a.AccountUuid].WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

				if(!((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(status)) {
					return false;
				}
			}

			return true;
		}

		/// <summary>
		///     now we have a block we must interpret and update our wallet
		/// </summary>
		/// <param name="block"></param>
		public virtual async Task UpdateWalletBlock(SynthesizedBlock synthesizedBlock, long previousSyncedBlockId, Func<SynthesizedBlock, LockContext, Task> callback, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			Log.Verbose($"updating wallet blocks for block {synthesizedBlock.BlockId}...");

			// this is where the wallet update happens...  any previous account that is fully synced can be upgraded no
			var availableAccounts = (await (await this.GetWalletSyncableAccounts(previousSyncedBlockId + 1, lockContext).ConfigureAwait(false)).WhereAsync(async a => {

					                        var chainState = (await this.WalletFileInfo.Accounts[a.AccountUuid].WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false));

					                        return new {chainState.BlockSyncStatus, chainState.LastBlockSynced};
				                        }, e => e.BlockSyncStatus == (int) WalletAccountChainState.BlockSyncStatuses.FullySynced).ConfigureAwait(false)).ToList();

			Dictionary<Guid, WalletAccountChainState> chainStates = new Dictionary<Guid, WalletAccountChainState>();

			foreach(IWalletAccount account in availableAccounts) {

				AccountId publicAccountId = account.GetAccountId();
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid];
				WalletAccountChainState chainState = await accountFileInfo.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);
				chainState.BlockSyncStatus = (int) WalletAccountChainState.BlockSyncStatuses.Blank;

				chainStates.Add(account.AccountUuid, chainState);

				if(synthesizedBlock.AccountScopped.ContainsKey(publicAccountId)) {
					// get the highest key use in the block for this account

					var transactionIds = synthesizedBlock.AccountScopped[publicAccountId].ConfirmedLocalTransactions.Where(t => t.Value.KeyUseIndex != null).ToList();

					foreach(var group in transactionIds.GroupBy(t => t.Value.KeyUseIndex.Ordinal)) {
						KeyUseIndexSet highestKeyUse = null;

						if(group.Any()) {
							highestKeyUse = group.Max(t => t.Value.KeyUseIndex);
						}

						if(highestKeyUse != null) {
							await this.UpdateLocalChainStateTransactionKeyLatestSyncHeight(account.AccountUuid, highestKeyUse, lockContext).ConfigureAwait(false);
						}
					}
				}

				// now the wallet key logs
				if(await this.UpdateWalletKeyLog(accountFileInfo, account, synthesizedBlock, lockContext).ConfigureAwait(false)) {
				}
			}

			// now anything else we want to add
			if(callback != null) {
				await callback(synthesizedBlock, lockContext).ConfigureAwait(false);
			}

			foreach(IWalletAccount account in availableAccounts) {
				WalletAccountChainState chainState = chainStates[account.AccountUuid];

				await this.SetChainStateHeight(chainState, synthesizedBlock.BlockId, lockContext).ConfigureAwait(false);

				// has been fully synced
				if(chainState.BlockSyncStatus != (int) WalletAccountChainState.BlockSyncStatuses.FullySynced) {
					throw new ApplicationException($"Wallet sync was incomplete for block id {synthesizedBlock.BlockId} and accountId {account.AccountUuid}");
				}
			}
		}

		public async Task ChangeAccountsCorrelation(ImmutableList<AccountId> enableAccounts, ImmutableList<AccountId> disableAccounts, LockContext lockContext) {

			foreach(var account in enableAccounts) {
				var walletAccount = await this.GetWalletAccount(account, lockContext).ConfigureAwait(false);

				if(walletAccount != null) {
					walletAccount.Correlated = true;
				}
			}

			foreach(var account in disableAccounts) {
				var walletAccount = await this.GetWalletAccount(account, lockContext).ConfigureAwait(false);

				if(walletAccount != null) {
					walletAccount.Correlated = false;
				}
			}
		}

		public Task CacheSynthesizedBlock(SynthesizedBlock synthesizedBlock, LockContext lockContext) {
			if(!this.syncBlockCache.ContainsKey(synthesizedBlock.BlockId)) {
				this.syncBlockCache.AddSafe(synthesizedBlock.BlockId, synthesizedBlock);
			}

			return this.CleanSynthesizedBlockCache(lockContext);
		}

		public async Task CleanSynthesizedBlockCache(LockContext lockContext) {
			
			var lowestBlockId = await this.LowestAccountBlockSyncHeight(lockContext).ConfigureAwait(false);
			
			foreach(long entry in this.syncBlockCache.Keys.ToArray().Where(b => b < lowestBlockId - 3)) {
				this.syncBlockCache.RemoveSafe(entry);
			}
		}

		/// <summary>
		///     if a block entry exists in the synthesized cache, pull it out (and remove it) and return it
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public SynthesizedBlock ExtractCachedSynthesizedBlock(long blockId) {
			SynthesizedBlock entry = null;

			if(this.syncBlockCache.ContainsKey(blockId)) {
				entry = this.syncBlockCache[blockId];
				this.syncBlockCache.RemoveSafe(blockId);
			}

			return entry;

		}

		public BlockId GetHighestCachedSynthesizedBlockId(LockContext lockContext) {
			if(this.syncBlockCache.IsEmpty) {
				return null;
			}

			return this.syncBlockCache.Keys.Max();
		}

		public bool IsSynthesizedBlockCached(long blockId, LockContext lockContext) {
			return this.syncBlockCache.ContainsKey(blockId);
		}

		/// <summary>
		///     obtain all cached block ids above or equal to the request id
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public List<SynthesizedBlock> GetCachedSynthesizedBlocks(long minimumBlockId, LockContext lockContext) {

			return this.syncBlockCache.Where(e => e.Key >= minimumBlockId).Select(e => e.Value).OrderBy(e => e.BlockId).ToList();

		}

		public async Task<IWalletAccountSnapshot> GetAccountSnapshot(AccountId accountId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount localAccount = await this.GetWalletAccount(accountId, lockContext).ConfigureAwait(false);

			if(localAccount == null) {
				return null;
			}

			IAccountFileInfo walletAccountInfo = this.WalletFileInfo.Accounts[localAccount.AccountUuid];

			return await walletAccountInfo.WalletSnapshotInfo.WalletAccountSnapshot(lockContext).ConfigureAwait(false);
		}

		public string GetPIDFilePath() {
			return Path.Combine(this.GetChainDirectoryPath(), PID_LOCK_FILE);
		}

		protected virtual async Task EnsurePIDLock() {

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType == AppSettingsBase.SerializationTypes.Feeder || GlobalSettings.ApplicationSettings.MobileMode) {
				// feeders and mobiles dont need to worry about this
				return;
			}

			string directory = this.GetChainDirectoryPath();

			FileExtensions.EnsureDirectoryStructure(directory, this.fileSystem);

			string pidfile = this.GetPIDFilePath();

			int currentPid = Process.GetCurrentProcess().Id;

			if(this.fileSystem.FileExists(pidfile)) {
				try {
					SafeArrayHandle pidBytes = FileExtensions.ReadAllBytes(pidfile, this.fileSystem);

					int lockPid = 0;

					if(pidBytes?.HasData ?? false) {
						TypeSerializer.Deserialize(pidBytes.Span, out lockPid);
					}

					if(lockPid == currentPid) {
						return;
					}

					try {
						Process process = Process.GetProcesses().SingleOrDefault(p => p.Id == lockPid);

						if(process?.Id != 0 && !(process?.HasExited ?? true)) {
							// ok, this other process has the lock, we fail here
							throw new ApplicationException("The wallet is already reserved by another tunning process. we allow only one process at a time.");

						}
					} catch(ArgumentException ex) {
						// thats fine, process id probably not running
					}
				} catch(Exception ex) {
					// do nothing
					await GlobalsService.AppRemote.Shutdown().ConfigureAwait(false);

					throw new ApplicationException("Failed to read pid lock file. invalid contents. shutting down.", ex);
				}

				this.fileSystem.DeleteFile(pidfile);
			}

			var bytes = new byte[sizeof(int)];
			TypeSerializer.Serialize(currentPid, bytes);

			FileExtensions.WriteAllBytes(pidfile, ByteArray.WrapAndOwn(bytes), this.fileSystem);
		}

		protected virtual async Task PrepareAccountInfos(IAccountFileInfo accountFileInfo, LockContext lockContext) {

			// and the wallet snapshot
			await accountFileInfo.WalletSnapshotInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and the transaction cache
			await accountFileInfo.WalletTransactionCacheInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and the transaction history
			await accountFileInfo.WalletTransactionHistoryInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and the elections history
			await accountFileInfo.WalletElectionsHistoryInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);
		}

		public async Task<bool> CreateStandardAccountKeys(Guid accountUuid, ImmutableDictionary<int, string> passphrases, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			IWalletAccount account = await this.GetWalletAccount(accountUuid, lockContext).ConfigureAwait(false);

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			// create keys

			IXmssWalletKey mainKey = null;
			IXmssWalletKey messageKey = null;
			IXmssWalletKey changeKey = null;
			ISecretWalletKey superKey = null;

			try {

				// the keys are often heavy on the network, lets pause it
				this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PauseNetwork();

				this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.CreatingAccountKeys, correlationContext);
				this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingTransactionKey, correlationContext);

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.TRANSACTION_KEY_NAME, 1, 4}, correlationContext);

				int lastTenth = -1;

				mainKey = await this.CreateXmssKey(GlobalsService.TRANSACTION_KEY_NAME, async (percentage) => {
					int tenth = percentage / 10;

					bool info = false;
					string message = $"Generation {percentage}% completed for key {GlobalsService.TRANSACTION_KEY_NAME}.";

					if(lastTenth != tenth) {
						lastTenth = tenth;
						Log.Information(message);
						info = true;
					}

					this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.KeyGenerationPercentageEvent(GlobalsService.TRANSACTION_KEY_NAME, percentage), correlationContext);

					if(!info) {
						Log.Verbose(message);
					}
				}).ConfigureAwait(false);

				GC.Collect();
				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.TRANSACTION_KEY_NAME, 1, 4}, correlationContext);

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.MESSAGE_KEY_NAME, 2, 4}, correlationContext);

				this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingMessageKey, correlationContext);

				lastTenth = -1;

				messageKey = await this.CreateXmssKey(GlobalsService.MESSAGE_KEY_NAME, async (percentage) => {
					int tenth = percentage / 10;

					bool info = false;
					string message = $"Generation {percentage}% completed for key {GlobalsService.MESSAGE_KEY_NAME}.";

					if(lastTenth != tenth) {
						lastTenth = tenth;
						Log.Information(message);
						info = true;
					}

					this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.KeyGenerationPercentageEvent(GlobalsService.MESSAGE_KEY_NAME, percentage), correlationContext);

					if(!info) {
						Log.Verbose(message);
					}
				}).ConfigureAwait(false);

				GC.Collect();

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.MESSAGE_KEY_NAME, 2, 4}, correlationContext);

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.CHANGE_KEY_NAME, 3, 4}, correlationContext);

				this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingChangeKey, correlationContext);

				lastTenth = -1;

				changeKey = await this.CreateXmssKey(GlobalsService.CHANGE_KEY_NAME, async (percentage) => {
					int tenth = percentage / 10;

					bool info = false;
					string message = $"Generation {percentage}% completed for key {GlobalsService.CHANGE_KEY_NAME}.";

					if(lastTenth != tenth) {
						lastTenth = tenth;
						Log.Information(message);
						info = true;
					}

					this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.KeyGenerationPercentageEvent(GlobalsService.CHANGE_KEY_NAME, percentage), correlationContext);

					if(!info) {
						Log.Verbose(message);
					}
				}).ConfigureAwait(false);

				GC.Collect();

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.CHANGE_KEY_NAME, 3, 4}, correlationContext);

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.SUPER_KEY_NAME, 4, 4}, correlationContext);

				this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingSuperKey, correlationContext);

				superKey = this.CreateSuperKey();

				this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.SUPER_KEY_NAME, 4, 4}, correlationContext);

				this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.KeysCreated, correlationContext);
				this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.AccountKeysCreated, correlationContext);

				await Repeater.RepeatAsync(() => {
					return this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(async (provider, token, lc) => {

						await this.AddAccountKey(account.AccountUuid, mainKey, passphrases, lc).ConfigureAwait(false);
						await this.AddAccountKey(account.AccountUuid, messageKey, passphrases, lc).ConfigureAwait(false);
						await this.AddAccountKey(account.AccountUuid, changeKey, passphrases, lc).ConfigureAwait(false);
						await this.AddAccountKey(account.AccountUuid, superKey, passphrases, lc).ConfigureAwait(false);
					}, lockContext);
				}).ConfigureAwait(false);

				// let's verify and confirm the keys are there
				using(var key = await this.LoadKey(GlobalsService.TRANSACTION_KEY_NAME, lockContext).ConfigureAwait(false)) {
					if(key == null) {
						throw new ApplicationException($"Failed to generate and load key {GlobalsService.TRANSACTION_KEY_NAME}.");
					}
				}

				using(var key = await this.LoadKey(GlobalsService.MESSAGE_KEY_NAME, lockContext).ConfigureAwait(false)) {
					if(key == null) {
						throw new ApplicationException($"Failed to generate and load key {GlobalsService.MESSAGE_KEY_NAME}.");
					}
				}

				using(var key = await this.LoadKey(GlobalsService.CHANGE_KEY_NAME, lockContext).ConfigureAwait(false)) {
					if(key == null) {
						throw new ApplicationException($"Failed to generate and load key {GlobalsService.CHANGE_KEY_NAME}.");
					}
				}

				using(var key = await this.LoadKey(GlobalsService.SUPER_KEY_NAME, lockContext).ConfigureAwait(false)) {
					if(key == null) {
						throw new ApplicationException($"Failed to generate and load key {GlobalsService.SUPER_KEY_NAME}.");
					}
				}

				GC.Collect();

			} catch(Exception ex) {
				throw new ApplicationException($"Failed to generate wallet keys. this is serious and the wallet remains invalid.", ex);
			} finally {
				try {
					mainKey?.Dispose();
				} catch {

				}

				try {
					messageKey?.Dispose();
				} catch {

				}

				try {
					changeKey?.Dispose();
				} catch {

				}

				try {
					superKey?.Dispose();
				} catch {

				}

				// back in business
				this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RestoreNetwork();
			}

			return true;
		}

		protected virtual void FillStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot) {

			accountSnapshot.AccountId = account.PublicAccountId.ToLongRepresentation();
			accountSnapshot.InceptionBlockId = account.ConfirmationBlockId;
			accountSnapshot.Correlated = account.Correlated;
		}

		protected virtual void FillJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot) {

			accountSnapshot.AccountId = account.PublicAccountId.ToLongRepresentation();
			accountSnapshot.InceptionBlockId = account.ConfirmationBlockId;
			accountSnapshot.Correlated = account.Correlated;
		}

		protected virtual IAccountFileInfo CreateNewAccountFileInfo(IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IAccountFileInfo accountFileInfo = this.CreateNewAccountFileInfo(new AccountPassphraseDetails(account.KeysEncrypted, account.KeysEncryptedIndividually, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout));

			this.CreateNewAccountInfoContents(accountFileInfo, account);

			// now create the keys
			this.AddNewAccountFileInfoKeys(accountFileInfo, account, lockContext);

			// and the snapshot
			accountFileInfo.WalletSnapshotInfo = this.SerialisationFal.CreateWalletSnapshotFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			return accountFileInfo;

		}

		protected virtual void CreateNewAccountInfoContents(IAccountFileInfo accountFileInfo, IWalletAccount account) {

			// and now create the keylog
			accountFileInfo.WalletKeyLogsInfo = this.SerialisationFal.CreateWalletKeyLogFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			// and now create the chainState
			accountFileInfo.WalletChainStatesInfo = this.SerialisationFal.CreateWalletChainStateFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			// and now create the transaction cache
			accountFileInfo.WalletTransactionCacheInfo = this.SerialisationFal.CreateWalletTransactionCacheFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			// and now create the transaction history
			accountFileInfo.WalletTransactionHistoryInfo = this.SerialisationFal.CreateWalletTransactionHistoryFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			// and now create the transaction history
			accountFileInfo.WalletElectionsHistoryInfo = this.SerialisationFal.CreateWalletElectionsHistoryFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			// and now create the key history
			accountFileInfo.WalletKeyHistoryInfo = this.SerialisationFal.CreateWalletKeyHistoryFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

		}

		protected abstract IAccountFileInfo CreateNewAccountFileInfo(AccountPassphraseDetails accountSecurityDetails);

		/// <summary>
		///     install the expected keys into a new file connection skeleton
		/// </summary>
		/// <param name="accountFileInfo"></param>
		/// <param name="account"></param>
		/// <param name="lockContext"></param>
		protected virtual void AddNewAccountFileInfoKeys(IAccountFileInfo accountFileInfo, IWalletAccount account, LockContext lockContext) {

			accountFileInfo.WalletKeysFileInfo.Add(GlobalsService.TRANSACTION_KEY_NAME, this.SerialisationFal.CreateWalletKeysFileInfo<IXmssWalletKey>(account, GlobalsService.TRANSACTION_KEY_NAME, GlobalsService.TRANSACTION_KEY_ORDINAL_ID, this.WalletFileInfo.WalletSecurityDetails, accountFileInfo.AccountSecurityDetails));
			accountFileInfo.WalletKeysFileInfo.Add(GlobalsService.MESSAGE_KEY_NAME, this.SerialisationFal.CreateWalletKeysFileInfo<IXmssWalletKey>(account, GlobalsService.MESSAGE_KEY_NAME, GlobalsService.MESSAGE_KEY_ORDINAL_ID, this.WalletFileInfo.WalletSecurityDetails, accountFileInfo.AccountSecurityDetails));
			accountFileInfo.WalletKeysFileInfo.Add(GlobalsService.CHANGE_KEY_NAME, this.SerialisationFal.CreateWalletKeysFileInfo<IXmssWalletKey>(account, GlobalsService.CHANGE_KEY_NAME, GlobalsService.CHANGE_KEY_ORDINAL_ID, this.WalletFileInfo.WalletSecurityDetails, accountFileInfo.AccountSecurityDetails));
			accountFileInfo.WalletKeysFileInfo.Add(GlobalsService.SUPER_KEY_NAME, this.SerialisationFal.CreateWalletKeysFileInfo<ISecretWalletKey>(account, GlobalsService.SUPER_KEY_NAME, GlobalsService.SUPER_KEY_ORDINAL_ID, this.WalletFileInfo.WalletSecurityDetails, accountFileInfo.AccountSecurityDetails));

		}

		/// <summary>
		///     Find the account and connection or a key in the wallet
		/// </summary>
		/// <param name="accountUuid"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		private async Task<(KeyInfo keyInfo, IWalletAccount account)> GetKeyInfo(Guid accountUuid, string name, LockContext lockContext) {

			IWalletAccount account = await this.GetWalletAccount(accountUuid, lockContext).ConfigureAwait(false);

			KeyInfo keyInfo = account.Keys.SingleOrDefault(k => k.Name == name);

			return (keyInfo, account);
		}

		private async Task<(KeyInfo keyInfo, IWalletAccount account)> GetKeyInfo(Guid accountUuid, byte ordinal, LockContext lockContext) {
			IWalletAccount account = await this.GetWalletAccount(accountUuid, lockContext).ConfigureAwait(false);

			KeyInfo keyInfo = account.Keys.SingleOrDefault(k => k.Ordinal == ordinal);

			return (keyInfo, account);
		}

		protected virtual async Task<bool> UpdateWalletKeyLog(IAccountFileInfo accountFile, IWalletAccount account, SynthesizedBlock synthesizedBlock, LockContext lockContext) {
			bool changed = false;

			Log.Verbose($"Update Wallet Key Logs for block {synthesizedBlock.BlockId} and accountId {account.AccountUuid}...");

			var chainState = await accountFile.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);
			bool keyLogSynced = ((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.KeyLogSynced);

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog && !keyLogSynced) {
				AccountId accountId = account.GetAccountId();

				if(synthesizedBlock.AccountScopped.ContainsKey(accountId)) {
					SynthesizedBlock.SynthesizedBlockAccountSet scoppedSynthesizedBlock = synthesizedBlock.AccountScopped[accountId];

					foreach(var transactionId in scoppedSynthesizedBlock.ConfirmedLocalTransactions) {

						ITransaction transaction = transactionId.Value;

						if(transaction.Version.Type == TransactionTypes.Instance.SIMPLE_PRESENTATION) {
							// the presentation trnasaction is a special case, which we never sign with a ey in our wallet, so we just ignore it
							continue;
						}

						KeyUseIndexSet keyUseIndexSet = transaction.KeyUseIndex;

						if(transaction is IJointTransaction joinTransaction) {
							// ok, we need to check if we are not the main sender but still a cosinger

						}

						if(!await accountFile.WalletKeyLogsInfo.ConfirmKeyLogTransactionEntry(transaction.TransactionId, transaction.KeyUseIndex, synthesizedBlock.BlockId, lockContext).ConfigureAwait(false)) {
							// ok, this transction was not in our key log. this means we might have a bad wallet. this is very serious adn we alert the user
							//TODO: what to do with this?
							throw new ApplicationException($"Block {synthesizedBlock.BlockId} has our transaction {transaction} which belongs to us but is NOT in our keylog. We might have an old wallet.");
						}

					}
				}
			}

			if(!keyLogSynced) {
				(await accountFile.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false)).BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.KeyLogSynced;
				changed = true;
			}

			return changed;
		}

	#region Physical key management

		public void EnsureWalletIsLoaded() {
			if(!this.IsWalletLoaded) {
				throw new WalletNotLoadedException();
			}
		}

		public async Task EnsureWalletFileIsPresent(LockContext lockContext) {
			if(!await this.WalletFileExists(lockContext).ConfigureAwait(false)) {

				throw new WalletFileMissingException();
			}
		}

		public async Task EnsureWalletPassphrase(LockContext lockContext, string passphrase = null) {

			await this.WalletFileInfo.LoadFileSecurityDetails(lockContext).ConfigureAwait(false);

			if(!string.IsNullOrWhiteSpace(passphrase)) {
				this.SetWalletPassphrase(passphrase, lockContext);
			}

			if(await this.IsWalletEncrypted(lockContext).ConfigureAwait(false) && !this.WalletFileInfo.WalletSecurityDetails.WalletPassphraseValid) {

				throw new WalletPassphraseMissingException();
			}
		}

		public async Task RequestCopyWallet(CorrelationContext correlationContext, int attempt, LockContext lockContext) {
			if(!await this.IsWalletEncrypted(lockContext).ConfigureAwait(false) && this.CopyWalletRequest != null) {
				await this.CopyWalletRequest(correlationContext, attempt, lockContext).ConfigureAwait(false);
			}

		}

		public async Task CaptureWalletPassphrase(CorrelationContext correlationContext, int attempt, LockContext lockContext) {

			await this.WalletFileInfo.LoadFileSecurityDetails(lockContext).ConfigureAwait(false);

			if(await this.IsWalletEncrypted(lockContext).ConfigureAwait(false) && !this.WalletFileInfo.WalletSecurityDetails.WalletPassphraseValid) {

				if(this.WalletPassphraseRequest == null) {
					throw new ApplicationException("No passphrase handling callback provided");
				}

				(SecureString passphrase, bool keysToo) = await this.WalletPassphraseRequest(correlationContext, attempt, lockContext).ConfigureAwait(false);

				if(passphrase == null) {
					throw new InvalidOperationException("null passphrase provided. Invalid");
				}

				string copyPassphrase = null;

				if(keysToo) {
					copyPassphrase = passphrase.ConvertToUnsecureString();
				}

				this.SetWalletPassphrase(passphrase, lockContext);

				if(keysToo) {

					async Task SetKeysPassphraseCallback() {

						try {
							foreach(var account in await this.GetAccounts(lockContext).ConfigureAwait(false)) {
								this.SetAllKeysPassphrase(account.AccountUuid, copyPassphrase, lockContext);
							}
						} finally {
							this.WalletIsLoaded -= SetKeysPassphraseCallback;
						}
					}

					this.WalletIsLoaded += SetKeysPassphraseCallback;
				}
			}
		}

		public void SetWalletPassphrase(string passphrase, LockContext lockContext) {
			this.WalletFileInfo.WalletSecurityDetails.SetWalletPassphrase(passphrase);

		}

		public void SetWalletPassphrase(SecureString passphrase, LockContext lockContext) {
			this.WalletFileInfo.WalletSecurityDetails.SetWalletPassphrase(passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultWalletPassphraseTimeout);

		}

		public void ClearWalletPassphrase() {

			this.WalletFileInfo.WalletSecurityDetails.ClearWalletPassphrase();
		}

		public Task EnsureWalletKeyIsReady(Guid accountUuid, byte ordinal, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			if(accountUuid != Guid.Empty) {
				string keyName = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo.Single(k => k.Value.OrdinalId == ordinal).Key;

				return this.EnsureWalletKeyIsReady(accountUuid, keyName, lockContext);
			}

			return Task.CompletedTask;
		}

		public Task EnsureWalletKeyIsReady(Guid accountUuid, KeyInfo keyInfo, LockContext lockContext) {
			return this.EnsureWalletKeyIsReady(accountUuid, keyInfo.Name, lockContext);
		}

		public Task EnsureWalletKeyIsReady(Guid accountUuid, string keyName, LockContext lockContext) {
			this.EnsureKeyFileIsPresent(accountUuid, keyName, 1, lockContext);
			this.EnsureKeyPassphrase(accountUuid, keyName, 1, lockContext);

			return Task.CompletedTask;

		}

		public void EnsureKeyFileIsPresent(Guid accountUuid, byte ordinal, int attempt, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			if(accountUuid != Guid.Empty) {
				string keyName = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo.Single(k => k.Value.OrdinalId == ordinal).Key;

				this.EnsureKeyFileIsPresent(accountUuid, keyName, attempt, lockContext);
			}
		}

		public void EnsureKeyFileIsPresent(Guid accountUuid, KeyInfo keyInfo, int attempt, LockContext lockContext) {
			this.EnsureKeyFileIsPresent(accountUuid, keyInfo.Name, attempt, lockContext);
		}

		public bool IsKeyFileIsPresent(Guid accountUuid, string keyName, int attempt, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				walletKeyFileInfo.RefreshFile();

				// first, ensure the key is physically present
				return walletKeyFileInfo.FileExists;
			}

			return true;
		}

		public void EnsureKeyFileIsPresent(Guid accountUuid, string keyName, int attempt, LockContext lockContext) {

			// first, ensure the key is physically present
			if(!this.IsKeyFileIsPresent(accountUuid, keyName, attempt, lockContext)) {

				throw new KeyFileMissingException(accountUuid, keyName, attempt);
			}
		}

		public void EnsureKeyPassphrase(Guid accountUuid, byte ordinal, int attempt, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				this.EnsureWalletIsLoaded();
				string keyName = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo.Single(k => k.Value.OrdinalId == ordinal).Key;

				this.EnsureKeyPassphrase(accountUuid, keyName, attempt, lockContext);
			}
		}

		public void EnsureKeyPassphrase(Guid accountUuid, KeyInfo keyInfo, int attempt, LockContext lockContext) {
			this.EnsureKeyPassphrase(accountUuid, keyInfo.Name, attempt, lockContext);

		}

		public bool IsKeyPassphraseValid(Guid accountUuid, string keyName, int attempt, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				this.EnsureWalletIsLoaded();

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				// now the passphrase
				return !accountFileInfo.AccountSecurityDetails.EncryptWalletKeys || accountFileInfo.AccountSecurityDetails.EncryptWalletKeys && accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountUuid, keyName);
			}

			return true;
		}

		public void EnsureKeyPassphrase(Guid accountUuid, string keyName, int attempt, LockContext lockContext) {
			if(!this.IsKeyPassphraseValid(accountUuid, keyName, attempt, lockContext)) {

				throw new KeyPassphraseMissingException(accountUuid, keyName, attempt);
			}
		}

		public async Task RequestCopyKeyFile(CorrelationContext correlationContext, Guid accountUuid, string keyName, int attempt, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				walletKeyFileInfo.RefreshFile();

				// first, ensure the key is physically present
				if(!walletKeyFileInfo.FileExists) {
					if(this.WalletCopyKeyFileRequest != null) {
						await WalletCopyKeyFileRequest(correlationContext, accountUuid, keyName, attempt, lockContext).ConfigureAwait(false);
					}
				}
			}
		}

		public Task CaptureKeyPassphrase(CorrelationContext correlationContext, Guid accountUuid, KeyInfo keyInfo, int attempt, LockContext lockContext) {
			return this.CaptureKeyPassphrase(correlationContext, accountUuid, keyInfo.Name, attempt, lockContext);

		}

		public async Task CaptureKeyPassphrase(CorrelationContext correlationContext, Guid accountUuid, string keyName, int attempt, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				this.EnsureWalletIsLoaded();

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				// now the passphrase
				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeys && !accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountUuid, keyName)) {

					if(this.WalletKeyPassphraseRequest == null) {
						throw new ApplicationException("No key passphrase handling callback provided");
					}

					SecureString passphrase = await this.WalletKeyPassphraseRequest(correlationContext, accountUuid, keyName, attempt, lockContext).ConfigureAwait(false);

					if(passphrase == null) {
						throw new InvalidOperationException("null passphrase provided. Invalid");
					}

					this.SetKeysPassphrase(accountUuid, keyName, passphrase, lockContext);
				}
			}
		}

		/// <summary>
		///     Apply the same passphrase to all keys
		/// </summary>
		/// <param name="correlationContext"></param>
		/// <param name="accountUuid"></param>
		/// <param name="attempt"></param>
		/// <param name="lockContext"></param>
		/// <param name="taskStasher"></param>
		/// <exception cref="ApplicationException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public async Task CaptureAllKeysPassphrase(CorrelationContext correlationContext, Guid accountUuid, int attempt, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {

				if(this.WalletKeyPassphraseRequest == null) {
					throw new ApplicationException("The request keys passphrase callback can not be null");

				}

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];

				if(!accountFileInfo.AccountSecurityDetails.EncryptWalletKeys) {
					return;
				}

				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeysIndividually) {
					throw new ApplicationException("Keys are set to be encrypted individually, yet we are about to set them all with the same passphrase");
				}

				if(accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountUuid)) {
					return;
				}

				SecureString passphrase = await this.WalletKeyPassphraseRequest(correlationContext, accountUuid, "All Keys", attempt, lockContext).ConfigureAwait(false);

				if(this.WalletKeyPassphraseRequest == null) {
					throw new ApplicationException("No key passphrase handling callback provided");
				}

				if(passphrase == null) {
					throw new InvalidOperationException("null passphrase provided. Invalid");
				}

				this.SetAllKeysPassphrase(accountUuid, passphrase, lockContext);
			}
		}

		public void SetAllKeysPassphrase(Guid accountUuid, string passphrase, LockContext lockContext) {

			this.SetAllKeysPassphrase(accountUuid, passphrase.ConvertToSecureString(), lockContext);
		}

		public void SetAllKeysPassphrase(Guid accountUuid, SecureString passphrase, LockContext lockContext) {

			if(accountUuid != Guid.Empty) {

				if(passphrase == null) {
					throw new InvalidOperationException("null passphrase provided. Invalid");
				}

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];

				if(!accountFileInfo.AccountSecurityDetails.EncryptWalletKeys) {
					return;
				}

				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeysIndividually) {
					throw new ApplicationException("Keys are set to be encrypted individually, yet we are about to set them all with the same passphrase");
				}

				if(accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountUuid)) {
					return;
				}

				// set the default key for all keys
				this.SetKeysPassphrase(accountUuid, passphrase, lockContext);
			}
		}

		public void SetKeysPassphrase(Guid accountUuid, string passphrase, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];

				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeysIndividually) {
					throw new ApplicationException("Keys are set to be encrypted individually, yet we are about to set them all with the same passphrase");
				}

				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountUuid, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void SetKeysPassphrase(Guid accountUuid, SecureString passphrase, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];

				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeysIndividually) {
					throw new ApplicationException("Keys are set to be encrypted individually, yet we are about to set them all with the same passphrase");
				}

				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountUuid, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void SetKeysPassphrase(Guid accountUuid, string keyname, string passphrase, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountUuid, keyname, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void SetKeysPassphrase(Guid accountUuid, string keyname, SecureString passphrase, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountUuid, keyname, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void ClearWalletKeyPassphrase(Guid accountUuid, string keyName, LockContext lockContext) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				accountFileInfo.AccountSecurityDetails.ClearKeysPassphrase();
			}
		}

	#endregion

	#region Chain State

		public async Task SetChainStateHeight(Guid accountUuid, long blockId, LockContext lockContext) {
			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			await SetChainStateHeight(chainState, blockId, lockContext).ConfigureAwait(false);
		}

		public async Task SetChainStateHeight(IWalletAccountChainState chainState, long blockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(blockId < chainState.LastBlockSynced) {
				throw new ApplicationException("The new chain state height can not be lower than the existing value");
			}

			if(!GlobalSettings.ApplicationSettings.SynclessMode && blockId > chainState.LastBlockSynced + 1) {
				Log.Warning($"The new chain state height ({blockId}) is higher than the next block id for current chain state height ({chainState.LastBlockSynced}).");
			}

			chainState.LastBlockSynced = blockId;
			chainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.BlockHeightUpdated;
		}

		public async Task<long> GetChainStateHeight(Guid accountUuid, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			return chainState.LastBlockSynced;
		}

		public async Task<KeyUseIndexSet> GetChainStateLastSyncedKeyHeight(IWalletKey key, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(key.AccountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			IWalletAccountChainStateKey keyChainState = chainState.Keys[key.KeyAddress.OrdinalId];

			return keyChainState.LatestBlockSyncKeyUse;

		}

		public async Task UpdateLocalChainStateKeyHeight(IWalletKey key, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(key.AccountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			IWalletAccountChainStateKey keyChainState = chainState.Keys[key.KeyAddress.OrdinalId];

			if(key.KeySequenceId < keyChainState.LocalKeyUse?.KeyUseSequenceId.Value) {
				throw new ApplicationException("The key sequence is lower than the one we have in the chain state");
			}

			if(key.KeySequenceId < keyChainState.LatestBlockSyncKeyUse?.KeyUseSequenceId.Value) {
				throw new ApplicationException("The key sequence is lower than the lasy synced block value");
			}

			if(key is IXmssWalletKey xmssWalletKey) {

				if(keyChainState.LocalKeyUse.IsSet && new KeyUseIndexSet(key.KeySequenceId, xmssWalletKey.KeyUseIndex, key.KeyAddress.OrdinalId) < keyChainState.LocalKeyUse) {
					throw new ApplicationException("The key sequence is lower than the one we have in the chain state");
				}

				if(keyChainState.LatestBlockSyncKeyUse.IsSet && new KeyUseIndexSet(key.KeySequenceId, xmssWalletKey.KeyUseIndex, key.KeyAddress.OrdinalId) < keyChainState.LatestBlockSyncKeyUse) {
					throw new ApplicationException("The key sequence is lower than the lasy synced block value");
				}

				keyChainState.LocalKeyUse.KeyUseIndex = xmssWalletKey.KeyUseIndex;
			}

			keyChainState.LocalKeyUse.KeyUseSequenceId = key.KeySequenceId;
		}

		/// <summary>
		///     update the key chain state with the highest key use we have found in the block.
		/// </summary>
		/// <param name="accountUuid"></param>
		/// <param name="highestKeyUse"></param>
		/// <exception cref="ApplicationException"></exception>
		protected async Task UpdateLocalChainStateTransactionKeyLatestSyncHeight(Guid accountUuid, KeyUseIndexSet highestKeyUse, LockContext lockContext) {

			if(!this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.UseKeyLog) {
				return;
			}

			this.EnsureWalletIsLoaded();
			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			IWalletAccountChainStateKey keyChainState = chainState.Keys[highestKeyUse.Ordinal];

			if(keyChainState.LatestBlockSyncKeyUse.IsSet && highestKeyUse < keyChainState.LatestBlockSyncKeyUse) {
				throw new ApplicationException("The last synced block transaction key sequence is lower than the value in our wallet. We may have a corrupt wallet and can not use it safely.");
			}

			if(keyChainState.LocalKeyUse.IsSet && highestKeyUse > keyChainState.LocalKeyUse) {
				throw new ApplicationException("The last synced block transaction key sequence is higher than the value in our wallet. We may have an out of date wallet and can not use it safely.");
			}

			keyChainState.LatestBlockSyncKeyUse = highestKeyUse;

		}

	#endregion

	#region Elections History

		public virtual async Task<IWalletElectionsHistory> InsertElectionsHistoryEntry(SynthesizedBlock.SynthesizedElectionResult electionResult, AccountId electedAccountId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
			IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => a.PublicAccountId != null && a.PublicAccountId.Equals(electedAccountId));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = walletbase.Accounts.Values.SingleOrDefault(a => a.AccountUuidHash != null && a.AccountUuidHash.Equals(electedAccountId));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletElectionsHistory walletElectionsHistory = this.CreateNewWalletElectionsHistoryEntry(lockContext);

			this.FillWalletElectionsHistoryEntry(walletElectionsHistory, electionResult, electedAccountId);

			IWalletElectionsHistoryFileInfo electionsHistoryInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionsHistoryInfo;

			await electionsHistoryInfo.InsertElectionsHistoryEntry(walletElectionsHistory, lockContext).ConfigureAwait(false);

			return walletElectionsHistory;

		}

		protected virtual void FillWalletElectionsHistoryEntry(IWalletElectionsHistory walletElectionsHistory, SynthesizedBlock.SynthesizedElectionResult electionResult, AccountId electedAccountId) {

			walletElectionsHistory.BlockId = electionResult.BlockId;
			walletElectionsHistory.Timestamp = electionResult.Timestamp;
			walletElectionsHistory.DelegateAccount = electionResult.ElectedAccounts[electedAccountId].delegateAccountId;
			walletElectionsHistory.MiningTier = electionResult.ElectedAccounts[electedAccountId].electedTier;
			walletElectionsHistory.SelectedTransactions = electionResult.ElectedAccounts[electedAccountId].selectedTransactions;
		}

	#endregion

	#region Transaction History

		protected async Task<(IWalletAccount sendingAccount, List<IWalletAccount> recipientAccounts)> GetImpactedLocalAccounts(ITransaction transaction, LockContext lockContext) {

			async Task<IWalletAccount> GetImpactedAccount(AccountId accountId) {

				var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
				IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => a.PublicAccountId != null && a.PublicAccountId.Equals(accountId));

				return account ?? walletbase.Accounts.Values.SingleOrDefault(a => a.AccountUuidHash != null && a.AccountUuidHash.Equals(accountId));
			}

			IWalletAccount sendingAccount = await GetImpactedAccount(transaction.TransactionId.Account).ConfigureAwait(false);
			List<IWalletAccount> recipientAccounts = new List<IWalletAccount>();

			foreach(var account in transaction.TargetAccounts) {
				IWalletAccount recipient = await GetImpactedAccount(account).ConfigureAwait(false);

				if(recipient != null) {
					recipientAccounts.Add(recipient);
				}
			}

			return (sendingAccount, recipientAccounts);
		}

		public virtual async Task<List<IWalletTransactionHistory>> InsertTransactionHistoryEntry(ITransaction transaction, string note, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			(IWalletAccount sendingAccount, var recipientAccounts) = await this.GetImpactedLocalAccounts(transaction, lockContext).ConfigureAwait(false);

			List<(IWalletAccount account, bool sender)> accounts = recipientAccounts.Select(a => (a, false)).ToList();

			if(sendingAccount != null) {
				accounts.Add((sendingAccount, true));
			}

			List<IWalletTransactionHistory> historyEntries = new List<IWalletTransactionHistory>();

			// loop on all accounts unless the receiver is also the sender, where it is already covered by the sending account entry
			foreach((IWalletAccount account, bool sender) in accounts.Where(e => e.sender || e.sender == false && e.account != sendingAccount)) {

				IWalletTransactionHistory walletAccountTransactionHistory = this.CreateNewWalletAccountTransactionHistoryEntry(lockContext);

				walletAccountTransactionHistory.Local = sender;

				AccountId realAccountId = account.GetAccountId();

				await this.FillWalletTransactionHistoryEntry(walletAccountTransactionHistory, transaction, sender, note, lockContext).ConfigureAwait(false);

				IWalletTransactionHistoryFileInfo transactionHistoryFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionHistoryInfo;

				await transactionHistoryFileInfo.InsertTransactionHistoryEntry(walletAccountTransactionHistory, lockContext).ConfigureAwait(false);

				historyEntries.Add(walletAccountTransactionHistory);
			}

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionHistoryUpdated(this.centralCoordinator.ChainId));

			return historyEntries;

		}

		protected virtual Task FillWalletTransactionHistoryEntry(IWalletTransactionHistory walletAccountTransactionHistory, ITransaction transaction, bool local, string note, LockContext lockContext) {
			walletAccountTransactionHistory.TransactionId = transaction.TransactionId.ToString();
			walletAccountTransactionHistory.Version = transaction.Version.ToString();
			walletAccountTransactionHistory.Contents = JsonUtils.SerializeJsonSerializable(transaction);

			walletAccountTransactionHistory.Note = note;
			walletAccountTransactionHistory.Timestamp = this.serviceSet.BlockchainTimeService.GetTransactionDateTime(transaction.TransactionId, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
			walletAccountTransactionHistory.Recipient = transaction.TargetAccountsSerialized;

			if(local) {
				// this is ours
				walletAccountTransactionHistory.Status = (byte) WalletTransactionHistory.TransactionStatuses.New;
			} else {
				// incoming transactions are always confirmed
				walletAccountTransactionHistory.Status = (byte) WalletTransactionHistory.TransactionStatuses.Confirmed;
			}

			return Task.CompletedTask;
		}

		public virtual async Task<IWalletTransactionHistoryFileInfo> UpdateLocalTransactionHistoryEntry(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
			IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => a.PublicAccountId != null && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = walletbase.Accounts.Values.SingleOrDefault(a => a.AccountUuidHash != null && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionHistoryFileInfo transactionHistoryFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionHistoryInfo;

			await transactionHistoryFileInfo.UpdateTransactionStatus(transactionId, status, lockContext).ConfigureAwait(false);

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionHistoryUpdated(this.centralCoordinator.ChainId));

			return transactionHistoryFileInfo;

		}

	#endregion

	#region Transaction Cache

		public virtual async Task InsertLocalTransactionCacheEntry(ITransactionEnvelope transactionEnvelope, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			TransactionId transactionId = transactionEnvelope.Contents.RehydratedTransaction.TransactionId;

			var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
			IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => a.PublicAccountId != null && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = walletbase.Accounts.Values.SingleOrDefault(a => a.AccountUuidHash != null && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionCache walletAccountTransactionCache = this.CreateNewWalletAccountTransactionCacheEntry(lockContext);

			this.FillWalletTransactionCacheEntry(walletAccountTransactionCache, transactionEnvelope, transactionId.Account);

			IWalletTransactionCacheFileInfo transactionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionCacheInfo;

			await transactionCacheFileInfo.InsertTransactionCacheEntry(walletAccountTransactionCache, lockContext).ConfigureAwait(false);

		}

		protected virtual void FillWalletTransactionCacheEntry(IWalletTransactionCache walletAccountTransactionCache, ITransactionEnvelope transactionEnvelope, AccountId targetAccountId) {

			walletAccountTransactionCache.TransactionId = transactionEnvelope.Contents.RehydratedTransaction.TransactionId.ToString();
			walletAccountTransactionCache.Version = transactionEnvelope.Contents.RehydratedTransaction.Version.ToString();
			walletAccountTransactionCache.Timestamp = this.serviceSet.BlockchainTimeService.GetTransactionDateTime(transactionEnvelope.Contents.RehydratedTransaction.TransactionId, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
			walletAccountTransactionCache.Transaction.Entry = transactionEnvelope.DehydrateEnvelope().Entry;

			walletAccountTransactionCache.Expiration = transactionEnvelope.GetExpirationTime(this.serviceSet.BlockchainTimeService, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);

			bool ours = transactionEnvelope.Contents.Uuid.Account == targetAccountId;

			if(ours) {
				// this is ours
				walletAccountTransactionCache.Status = (byte) WalletTransactionCache.TransactionStatuses.New;
			} else {
				// incoming transactions are always confirmed
				walletAccountTransactionCache.Status = (byte) WalletTransactionCache.TransactionStatuses.Confirmed;
			}
		}

		public virtual async Task<IWalletTransactionCache> GetLocalTransactionCacheEntry(TransactionId transactionId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
			IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => a.PublicAccountId != null && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = walletbase.Accounts.Values.SingleOrDefault(a => a.AccountUuidHash != null && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			return await this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionCacheInfo.GetTransactionBase(transactionId, lockContext).ConfigureAwait(false);

		}

		public virtual async Task UpdateLocalTransactionCacheEntry(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
			IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => a.PublicAccountId != null && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = walletbase.Accounts.Values.SingleOrDefault(a => a.AccountUuidHash != null && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionCacheFileInfo transactionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionCacheInfo;

			await transactionCacheFileInfo.UpdateTransaction(transactionId, status, gossipMessageHash, lockContext).ConfigureAwait(false);

		}

		public virtual async Task RemoveLocalTransactionCacheEntry(TransactionId transactionId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
			IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => a.PublicAccountId != null && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = walletbase.Accounts.Values.SingleOrDefault(a => a.AccountUuidHash != null && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionCacheFileInfo transactionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionCacheInfo;

			await transactionCacheFileInfo.RemoveTransaction(transactionId, lockContext).ConfigureAwait(false);

		}

	#endregion

	#region Election Cache

		public async Task CreateElectionCacheWalletFile(IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			await this.DeleteElectionCacheWalletFile(account, lockContext).ConfigureAwait(false);

			IAccountFileInfo walletFileInfoAccount = this.WalletFileInfo.Accounts[account.AccountUuid];

			walletFileInfoAccount.WalletElectionCacheInfo = this.SerialisationFal.CreateWalletElectionCacheFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			await walletFileInfoAccount.WalletElectionCacheInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

		}

		public Task DeleteElectionCacheWalletFile(IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			IAccountFileInfo walletFileInfoAccount = this.WalletFileInfo.Accounts[account.AccountUuid];

			walletFileInfoAccount.WalletElectionCacheInfo?.DeleteFile();

			walletFileInfoAccount.WalletElectionCacheInfo = null;

			return Task.CompletedTask;
		}

		public Task<List<TransactionId>> GetElectionCacheTransactions(IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionCacheInfo;

			return electionCacheFileInfo?.GetAllTransactions(lockContext);

		}

		public Task InsertElectionCacheTransactions(List<TransactionId> transactionIds, long blockId, IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			var entries = new List<WalletElectionCache>();

			foreach(TransactionId transactionId in transactionIds) {

				WalletElectionCache entry = this.CreateNewWalletAccountElectionCacheEntry(lockContext);
				entry.TransactionId = transactionId;
				entry.BlockId = blockId;

				entries.Add(entry);
			}

			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionCacheInfo;

			return electionCacheFileInfo?.InsertElectionCacheEntries(entries, lockContext);

		}

		public Task RemoveBlockElection(long blockId, IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionCacheInfo;

			return electionCacheFileInfo?.RemoveBlockElection(blockId, lockContext);

		}

		public Task RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactionIds, IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionCacheInfo;

			return electionCacheFileInfo?.RemoveBlockElectionTransactions(blockId, transactionIds, lockContext);

		}

	#endregion

	#region Keys

		/// <summary>
		///     here we add a new key to the account
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public async Task AddAccountKey<KEY>(Guid accountUuid, KEY key, ImmutableDictionary<int, string> passphrases, LockContext lockContext, KEY nextKey = null)
			where KEY : class, IWalletKey {
			this.EnsureWalletIsLoaded();

			key.AccountUuid = accountUuid;

			if(nextKey != null) {
				nextKey.AccountUuid = accountUuid;
			}

			(KeyInfo keyInfo1, IWalletAccount account) = await this.GetKeyInfo(accountUuid, key.Name, lockContext).ConfigureAwait(false);

			if(keyInfo1 != null) {
				account.Keys.Remove(keyInfo1);
			}

			// its a brand new key
			KeyInfo keyInfo = new KeyInfo();
			byte ordinal = key.KeyAddress.OrdinalId;

			if(ordinal == 0) {

				ordinal = 1;

				// find the highest ordinal, and add 1
				if(account.Keys.Count != 0) {
					ordinal = (byte) (ordinal + account.Keys.Max(k => k.Ordinal));
				}

				key.KeyAddress.OrdinalId = ordinal;
			}

			keyInfo.Name = key.Name;
			keyInfo.Ordinal = ordinal;
			key.KeySequenceId = 0;

			if(nextKey != null) {
				nextKey.KeyAddress.OrdinalId = ordinal;
				nextKey.KeySequenceId = 0;
			}

			// we add this new key
			account.Keys.Add(keyInfo);

			IAccountFileInfo walletAccountFileInfo = this.WalletFileInfo.Accounts[accountUuid];

			if(walletAccountFileInfo.AccountSecurityDetails.EncryptWalletKeys) {
				keyInfo.EncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration);

				string passphrase = "";

				if(walletAccountFileInfo.AccountSecurityDetails.EncryptWalletKeysIndividually) {
					if(passphrases?.ContainsKey(keyInfo.Ordinal) ?? false) {
						passphrase = passphrases[keyInfo.Ordinal];
					}
				} else {
					if(passphrases?.ContainsKey(1) ?? false) {
						passphrase = passphrases[1];
					}
				}

				if(!string.IsNullOrWhiteSpace(passphrase)) {
					this.SetKeysPassphrase(accountUuid, keyInfo.Name, passphrase, lockContext);
				}

				this.EnsureKeyPassphrase(accountUuid, keyInfo.Name, 1, lockContext);
			}

			// ensure we create the key file
			await walletAccountFileInfo.WalletKeysFileInfo[key.Name].Reset(lockContext).ConfigureAwait(false);
			walletAccountFileInfo.WalletKeysFileInfo[key.Name].DeleteFile();
			await walletAccountFileInfo.WalletKeysFileInfo[key.Name].CreateEmptyFile(key, nextKey, lockContext).ConfigureAwait(false);

			// add the key chainstate
			IWalletAccountChainStateKey chainStateKey = this.CreateNewWalletAccountChainStateKeyEntry(lockContext);
			chainStateKey.Ordinal = key.KeyAddress.OrdinalId;

			KeyUseIndexSet keyUseIndex = new KeyUseIndexSet(key.KeySequenceId, -1, chainStateKey.Ordinal);

			if(key is XmssWalletKey xmssWalletKey) {
				keyUseIndex.KeyUseIndex = xmssWalletKey.KeyUseIndex;
			}

			chainStateKey.LocalKeyUse = keyUseIndex;
			chainStateKey.LatestBlockSyncKeyUse = new KeyUseIndexSet(0, 0, chainStateKey.Ordinal);

			var chainState = await walletAccountFileInfo.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

			if(chainState.Keys.ContainsKey(chainStateKey.Ordinal)) {
				chainState.Keys.Remove(chainStateKey.Ordinal);
			}

			chainState.Keys.Add(chainStateKey.Ordinal, chainStateKey);
		}

		/// <summary>
		///     this method can be called to create and set the next XMSS key. This can be useful to pre create large keys as the
		///     next key, save some time at key change time.
		/// </summary>
		/// <param name="accountUuid"></param>
		/// <param name="keyName"></param>
		public async Task CreateNextXmssKey(Guid accountUuid, string keyName, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			await this.EnsureWalletKeyIsReady(accountUuid, keyName, lockContext).ConfigureAwait(false);

			bool nextKeySet = await this.IsNextKeySet(accountUuid, keyName, lockContext).ConfigureAwait(false);

			using IXmssWalletKey nextKey = await this.CreateXmssKey(keyName).ConfigureAwait(false);

			if(nextKeySet) {
				await this.UpdateNextKey(nextKey, lockContext).ConfigureAwait(false);
			} else {
				await this.SetNextKey(accountUuid, nextKey, lockContext).ConfigureAwait(false);
			}
		}

		public async Task CreateNextXmssKey(Guid accountUuid, byte ordinal, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(accountUuid, ordinal, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			await this.CreateNextXmssKey(accountUuid, keyMeta.keyInfo.Name, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<bool> IsKeyEncrypted(Guid accountUuid, LockContext lockContext) {

			return (await this.GetWalletAccount(accountUuid, lockContext).ConfigureAwait(false)).KeysEncrypted;
		}

		/// <summary>
		///     determine if the next key has already been created and set
		/// </summary>
		/// <param name="taskStasher"></param>
		/// <param name="accountUuid"></param>
		/// <param name="ordinal"></param>
		/// <returns></returns>
		public virtual async Task<bool> IsNextKeySet(Guid accountUuid, string keyName, LockContext lockContext) {

			this.EnsureWalletIsLoaded();
			await this.EnsureWalletKeyIsReady(accountUuid, keyName, lockContext).ConfigureAwait(false);

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(accountUuid, keyName, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			Repeater.Repeat(index => {
				// ensure the key files are present
				this.EnsureKeyFileIsPresent(accountUuid, keyMeta.keyInfo, index, lockContext);
			});

			bool isNextKeySet = false;

			await this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[keyName];

			isNextKeySet = await walletKeyInfo.IsNextKeySet(lockContext).ConfigureAwait(false);

			return isNextKeySet;

		}

		public virtual async Task SetNextKey(Guid accountUuid, IWalletKey nextKey, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			nextKey.AccountUuid = accountUuid;

			nextKey.Status = Enums.KeyStatus.New;

			(KeyInfo keyInfo, var _) = await this.GetKeyInfo(accountUuid, nextKey.Name, lockContext).ConfigureAwait(false);

			if(keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(accountUuid, keyInfo, lockContext).ConfigureAwait(false);
			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[nextKey.Name];

			await walletKeyInfo.SetNextKey(nextKey, lockContext).ConfigureAwait(false);
		}

		public virtual async Task UpdateNextKey(IWalletKey nextKey, LockContext lockContext) {

			nextKey.Status = Enums.KeyStatus.New;

			(KeyInfo keyInfo, var _) = await this.GetKeyInfo(nextKey.AccountUuid, nextKey.Name, lockContext).ConfigureAwait(false);

			if(keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(nextKey.AccountUuid, keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[nextKey.AccountUuid].WalletKeysFileInfo[nextKey.Name];

			await walletKeyInfo.UpdateNextKey(keyInfo, nextKey, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<IWalletKey> LoadKey(string keyName, LockContext lockContext) {
			return await this.LoadKey<IWalletKey>(await this.GetAccountUuid(lockContext).ConfigureAwait(false), keyName, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<IWalletKey> LoadKey(byte ordinal, LockContext lockContext) {
			return await this.LoadKey<IWalletKey>(await this.GetAccountUuid(lockContext).ConfigureAwait(false), ordinal, lockContext).ConfigureAwait(false);
		}

		public virtual Task<IWalletKey> LoadKey(Guid AccountUuid, string keyName, LockContext lockContext) {
			return this.LoadKey<IWalletKey>(AccountUuid, keyName, lockContext);
		}

		public virtual Task<IWalletKey> LoadKey(Guid AccountUuid, byte ordinal, LockContext lockContext) {
			return this.LoadKey<IWalletKey>(AccountUuid, ordinal, lockContext);
		}

		public virtual async Task<T> LoadKey<T>(string keyName, LockContext lockContext)
			where T : class, IWalletKey {

			return await this.LoadKey<T>(await this.GetAccountUuid(lockContext).ConfigureAwait(false), keyName, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<T> LoadKey<T>(byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {

			return await this.LoadKey<T>(await this.GetAccountUuid(lockContext).ConfigureAwait(false), ordinal, lockContext).ConfigureAwait(false);
		}

		public virtual Task<T> LoadKey<T>(Guid accountUuid, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			T Selector(T key) {
				return key;
			}

			return this.LoadKey<T>(Selector, accountUuid, keyName, lockContext);

		}

		public virtual Task<T> LoadKey<T>(Guid accountUuid, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {
			T Selector(T key) {
				return key;
			}

			return this.LoadKey<T>(Selector, accountUuid, ordinal, lockContext);

		}

		public virtual Task<T> LoadKey<T>(Func<T, T> selector, Guid accountUuid, string name, LockContext lockContext)
			where T : class, IWalletKey {
			return this.LoadKey<T, T>(selector, accountUuid, name, lockContext);
		}

		public virtual Task<T> LoadKey<T>(Func<T, T> selector, Guid accountUuid, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {

			return this.LoadKey<T, T>(selector, accountUuid, ordinal, lockContext);
		}

		/// <summary>
		///     Load a key with a custom selector
		/// </summary>
		/// <param name="selector"></param>
		/// <param name="accountUuid"></param>
		/// <param name="name"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<T> LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string name, LockContext lockContext)
			where T : class
			where K : class, IWalletKey {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, var _) = await this.GetKeyInfo(accountUuid, name, lockContext).ConfigureAwait(false);

			if(keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			// ensure the key files are present
			await this.EnsureWalletKeyIsReady(accountUuid, keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[keyInfo.Name];

			return await walletKeyInfo.LoadKey(selector, accountUuid, keyInfo.Name, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<T> LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, byte ordinal, LockContext lockContext)
			where T : class
			where K : class, IWalletKey {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(accountUuid, ordinal, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[keyMeta.keyInfo.Name];

			return await walletKeyInfo.LoadKey(selector, accountUuid, keyMeta.keyInfo.Name, lockContext).ConfigureAwait(false);
		}

		public virtual Task<IWalletKey> LoadNextKey(Guid accountUuid, string keyName, LockContext lockContext) {
			return this.LoadNextKey<IWalletKey>(accountUuid, keyName, lockContext);
		}

		public virtual async Task<T> LoadNextKey<T>(Guid accountUuid, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(accountUuid, keyName, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[keyMeta.keyInfo.Name];

			var key = await walletKeyInfo.LoadNextKey<T>(accountUuid, keyMeta.keyInfo.Name, lockContext).ConfigureAwait(false);

			// this might not have been set
			key.AccountUuid = accountUuid;

			return key;
		}

		public virtual async Task UpdateKey(IWalletKey key, LockContext lockContext) {

			if(key.PrivateKey == null || key.PrivateKey.Length == 0) {
				throw new ApplicationException("Private key is not set");
			}

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(key.AccountUuid, key.Name, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(key.AccountUuid, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[key.AccountUuid].WalletKeysFileInfo[key.Name];

			await walletKeyInfo.UpdateKey(key, lockContext).ConfigureAwait(false);
		}

		/// <summary>
		///     Swap the next key with the current key. the old key is placed in the key history for archiving
		/// </summary>
		/// <param name="key"></param>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task SwapNextKey(IWalletKey key, LockContext lockContext, bool storeHistory = true) {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(key.AccountUuid, key.Name, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(key.AccountUuid, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[key.AccountUuid].WalletKeysFileInfo[key.Name];

			if(!await walletKeyInfo.IsNextKeySet(lockContext).ConfigureAwait(false)) {
				throw new ApplicationException("Next private key is not set");
			}

			if(storeHistory) {
				WalletKeyHistoryFileInfo walletKeyHistoryInfo = this.WalletFileInfo.Accounts[key.AccountUuid].WalletKeyHistoryInfo;

				await walletKeyHistoryInfo.InsertKeyHistoryEntry(key, this.CreateNewWalletKeyHistoryEntry(lockContext), lockContext).ConfigureAwait(false);
			}

			await walletKeyInfo.SwapNextKey(keyMeta.keyInfo, key.AccountUuid, lockContext).ConfigureAwait(false);

			using var newKey = await walletKeyInfo.LoadKey<IWalletKey>(key.AccountUuid, key.Name, lockContext).ConfigureAwait(false);

			// we swapped our key, we must update the chain state
			await this.UpdateLocalChainStateKeyHeight(newKey, lockContext).ConfigureAwait(false);

		}

		public virtual async Task SwapNextKey(Guid accountUUid, string keyName, LockContext lockContext, bool storeHistory = true) {

			using var key = await this.LoadKey(accountUUid, keyName, lockContext).ConfigureAwait(false);

			await this.SwapNextKey(key, lockContext, storeHistory).ConfigureAwait(false);

		}

	#endregion

	#region external requests

		// a special method that will block and request the outside world to load the wallet or create a new one
		public async Task EnsureWalletLoaded(LockContext lockContext) {
			if(!this.IsWalletLoaded) {

				throw new WalletNotLoadedException();
			}

		}

		/// <summary>
		///     here we will raise events when we need the passphrases, and external providers can provide us with what we need.
		/// </summary>
		public virtual Task SetExternalPassphraseHandlers(Delegates.RequestPassphraseDelegate requestPassphraseDelegate, Delegates.RequestKeyPassphraseDelegate requestKeyPassphraseDelegate, Delegates.RequestCopyKeyFileDelegate requestKeyCopyFileDelegate, Delegates.RequestCopyWalletFileDelegate copyWalletDelegate, LockContext lockContext) {
			this.WalletPassphraseRequest += requestPassphraseDelegate;

			this.WalletKeyPassphraseRequest += requestKeyPassphraseDelegate;

			this.WalletCopyKeyFileRequest += requestKeyCopyFileDelegate;

			this.CopyWalletRequest += copyWalletDelegate;

			return Task.CompletedTask;
		}

	#endregion

	#region console

		/// <summary>
		///     Set the default passphrase request handling to the console
		/// </summary>
		public Task SetConsolePassphraseHandlers(LockContext lockContext) {
			this.WalletPassphraseRequest += (correlationContext, attempt, lc) => this.RequestWalletPassphraseByConsole(lc);

			this.WalletKeyPassphraseRequest += (correlationContext, accountUUid, keyName, attempt, lc) => this.RequestKeysPassphraseByConsole(accountUUid, keyName, lc);

			this.WalletCopyKeyFileRequest += (correlationContext, accountUUid, keyName, attempt, lc) => this.RequestKeysCopyFileByConsole(accountUUid, keyName, lc);

			this.CopyWalletRequest += (correlationContext, attempt, lc) => this.RequestCopyWalletByConsole(lc);

			return Task.CompletedTask;
		}

		public Task<(SecureString passphrase, bool keysToo)> RequestWalletPassphraseByConsole(LockContext lockContext, int maxTryCount = 10) {
			return this.RequestPassphraseByConsole(lockContext, "wallet", maxTryCount);
		}

		public async Task<SecureString> RequestKeysPassphraseByConsole(Guid accountUUid, string keyName, LockContext lockContext, int maxTryCount = 10) {
			return (await this.RequestPassphraseByConsole(lockContext, $"wallet key (account: {accountUUid}, key name: {keyName})", maxTryCount).ConfigureAwait(false)).passphrase;
		}

		public Task RequestKeysCopyFileByConsole(Guid accountUUid, string keyName, LockContext lockContext, int maxTryCount = 10) {
			Log.Warning($"Wallet key file (account: {accountUUid}, key name: {keyName}) is not present. Please copy it.", maxTryCount);

			Console.ReadKey();

			return Task.CompletedTask;
		}

		public Task RequestCopyWalletByConsole(LockContext lockContext) {
			Log.Information("Please ensure the wallet file is in the wallets baseFolder");
			Console.ReadKey();

			return Task.CompletedTask;
		}

		/// <summary>
		///     a utility method to request for the passphrase via the console. This only works in certain situations, not for RPC
		///     calls for sure.
		/// </summary>
		/// <param name="passphraseType"></param>
		/// <returns>the secure string or null if error occured</returns>
		public async Task<(SecureString passphrase, bool keysToo)> RequestPassphraseByConsole(LockContext lockContext, string passphraseType = "wallet", int maxTryCount = 10) {
			bool valid = false;
			SecureString pass = null;

			int counter = 0;

			do {
				// we must request the passwords by console
				Log.Verbose("");
				Log.Verbose($"Enter your {passphraseType} passphrase (ESC to skip):");
				SecureString temp = await this.RequestConsolePassphrase(lockContext).ConfigureAwait(false);

				if(temp == null) {
					Log.Verbose("Entry has been skipped.");

					return (null, false);
				}

				Log.Verbose($"Enter your {passphraseType} passphrase again:");
				SecureString pass2 = await this.RequestConsolePassphrase(lockContext).ConfigureAwait(false);

				valid = temp.SecureStringEqual(pass2);

				if(!valid) {
					Log.Verbose("Passphrases are different.");
				} else {
					// its valid!
					pass = temp;
				}

				counter++;
			} while(valid == false && counter < maxTryCount);

			return (pass, false);

		}

		/// <summary>
		///     a simple method to capture a console fairly securely from the console
		/// </summary>
		/// <returns></returns>
		private Task<SecureString> RequestConsolePassphrase(LockContext lockContext) {
			SecureString securePwd = new SecureString();
			ConsoleKeyInfo key;

			do {
				key = Console.ReadKey(true);

				if(key.Key == ConsoleKey.Escape) {
					return Task.FromResult((SecureString) null);
				}

				// Ignore any key out of range.
				if((int) key.Key >= 65 && (int) key.Key <= 90) {
					// Append the character to the password.
					securePwd.AppendChar(key.KeyChar);
					Console.Write("*");
				}

				// Exit if Enter key is pressed.
			} while(key.Key != ConsoleKey.Enter || securePwd.Length == 0);

			Log.Verbose("");

			return Task.FromResult(securePwd);

		}

	#endregion

	#region entry creation

		protected virtual IUserWallet CreateNewWalletEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewUserWallet();
		}

		protected virtual IWalletAccount CreateNewWalletAccountEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccount();
		}

		protected virtual WalletAccountKeyLog CreateNewWalletAccountKeyLogEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountKeyLog();
		}

		protected virtual IWalletTransactionCache CreateNewWalletAccountTransactionCacheEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountTransactionCache();
		}

		protected virtual IWalletElectionsHistory CreateNewWalletElectionsHistoryEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletElectionsHistoryEntry();
		}

		protected virtual IWalletTransactionHistory CreateNewWalletAccountTransactionHistoryEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountTransactionHistory();
		}

		protected virtual WalletElectionCache CreateNewWalletAccountElectionCacheEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountElectionCache();
		}

		protected virtual WalletAccountChainState CreateNewWalletAccountChainStateEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountChainState();
		}

		protected virtual IWalletAccountChainStateKey CreateNewWalletAccountChainStateKeyEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountChainStateKey();
		}

		protected virtual WalletKeyHistory CreateNewWalletKeyHistoryEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletKeyHistory();
		}

		public virtual Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshotEntry(LockContext lockContext) {
			return Task.FromResult(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountSnapshot());
		}

		public virtual Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshotEntry(LockContext lockContext) {
			return Task.FromResult(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletJointAccountSnapshot());
		}

	#endregion

	#region XMSS

		public Task<IXmssWalletKey> CreateXmssKey(string name, Func<int, Task> progressCallback = null) {
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			int treeHeight = WalletProvider.MINIMAL_XMSS_KEY_HEIGHT;
			float xmssKeyWarningLevel = 0.7F;
			float xmssKeyChangeLevel = 0.9F;
			int keyHashBits = 0;
			WalletProvider.HashTypes hashType = WalletProvider.HashTypes.Sha2;

			WalletProvider.HashTypes GetHashType(ChainConfigurations.HashTypes source) {
				switch(source) {
					case ChainConfigurations.HashTypes.Sha2:
						return WalletProvider.HashTypes.Sha2;
					case ChainConfigurations.HashTypes.Sha3:
						return WalletProvider.HashTypes.Sha3;
					case ChainConfigurations.HashTypes.Blake2:
						return WalletProvider.HashTypes.Blake2;
				}

				return WalletProvider.HashTypes.Sha3;
			}

			if(name == GlobalsService.TRANSACTION_KEY_NAME) {
				treeHeight = Math.Max((int) chainConfiguration.TransactionXmssKeyTreeHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.TransactionXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.TransactionXmssKeyChangeLevel;
				hashType = GetHashType(chainConfiguration.TransactionXmssKeyHashType);
				keyHashBits = WalletProvider.TRANSACTION_KEY_HASH_BITS;
			}

			if(name == GlobalsService.MESSAGE_KEY_NAME) {
				treeHeight = Math.Max((int) chainConfiguration.MessageXmssKeyTreeHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.MessageXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.MessageXmssKeyChangeLevel;
				hashType = GetHashType(chainConfiguration.MessageXmssKeyHashType);
				keyHashBits = WalletProvider.MESSAGE_KEY_HASH_BITS;
			}

			if(name == GlobalsService.CHANGE_KEY_NAME) {
				treeHeight = Math.Max((int) chainConfiguration.ChangeXmssKeyTreeHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.ChangeXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.ChangeXmssKeyChangeLevel;

				hashType = GetHashType(chainConfiguration.ChangeXmssKeyHashType);
				keyHashBits = WalletProvider.CHANGE_KEY_HASH_BITS;
			}

			return this.CreateXmssKey(name, treeHeight, keyHashBits, hashType, xmssKeyWarningLevel, xmssKeyChangeLevel, progressCallback);
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null) {
			return this.CreateXmssKey(name, XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT, WalletProvider.DEFAULT_KEY_HASH_BITS, WalletProvider.HashTypes.Sha2, warningLevel, changeLevel, progressCallback);
		}

		public async Task<IXmssWalletKey> CreateXmssKey(string name, int treeHeight, int hashBits, WalletProvider.HashTypes HashType, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null) {
			IXmssWalletKey key = this.CreateBasicKey<IXmssWalletKey>(name, Enums.KeyTypes.XMSS);

			Enums.KeyHashBits fullHashbits = Enums.KeyHashBits.SHA2_256;

			if(HashType == WalletProvider.HashTypes.Sha2 && hashBits == 256) {
				fullHashbits = Enums.KeyHashBits.SHA2_256;
			} else if(HashType == WalletProvider.HashTypes.Sha2 && hashBits == 512) {
				fullHashbits = Enums.KeyHashBits.SHA2_512;
			} else if(HashType == WalletProvider.HashTypes.Sha3 && hashBits == 256) {
				fullHashbits = Enums.KeyHashBits.SHA3_256;
			} else if(HashType == WalletProvider.HashTypes.Sha3 && hashBits == 512) {
				fullHashbits = Enums.KeyHashBits.SHA3_512;
			} else if(HashType == WalletProvider.HashTypes.Blake2 && hashBits == 256) {
				fullHashbits = Enums.KeyHashBits.BLAKE2_256;
			} else if(HashType == WalletProvider.HashTypes.Blake2 && hashBits == 512) {
				fullHashbits = Enums.KeyHashBits.BLAKE2_512;
			}

			using(XMSSProvider provider = new XMSSProvider(fullHashbits, treeHeight, Enums.ThreadMode.ThreeQuarter)) {

				provider.Initialize();

				Log.Information($"Creating a new XMSS key named '{name}' with tree height {treeHeight} and hashBits {provider.HashBits} and good for {provider.MaximumHeight} signatures.");

				(ByteArray privateKey, ByteArray publicKey) = await provider.GenerateKeys(progressCallback).ConfigureAwait(false);

				key.HashBits = provider.HashBitsEnum;
				key.TreeHeight = provider.TreeHeight;
				key.KeyType = Enums.KeyTypes.XMSS;
				key.WarningHeight = provider.GetKeyUseThreshold(warningLevel);
				key.ChangeHeight = provider.GetKeyUseThreshold(changeLevel);
				key.MaximumHeight = provider.MaximumHeight;
				key.KeyUseIndex = 0;

				key.PrivateKey = privateKey.ToExactByteArrayCopy();
				key.PublicKey = publicKey.ToExactByteArrayCopy();

				privateKey.Return();
				publicKey.Return();
			}

			this.HashKey(key);

			Log.Information($"XMSS Key '{name}' created");

			return key;
		}

		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, float warningLevel, float changeLevel, Func<int, int, int, Task> progressCallback = null) {
			return this.CreateXmssmtKey(name, XMSSMTProvider.DEFAULT_XMSSMT_TREE_HEIGHT, XMSSMTProvider.DEFAULT_XMSSMT_TREE_LAYERS, XMSSProvider.DEFAULT_HASH_BITS, warningLevel, changeLevel, progressCallback);
		}

		public async Task<IXmssMTWalletKey> CreateXmssmtKey(string name, int treeHeight, int treeLayers, Enums.KeyHashBits hashBits, float warningLevel, float changeLevel, Func<int, int, int, Task> progressCallback = null) {
			IXmssMTWalletKey key = this.CreateBasicKey<IXmssMTWalletKey>(name, Enums.KeyTypes.XMSSMT);

			using(XMSSMTProvider provider = new XMSSMTProvider(hashBits, treeHeight, treeLayers, Enums.ThreadMode.Half)) {
				provider.Initialize();

				Log.Information($"Creating a new XMSS^MT key named '{name}' with tree height {treeHeight}, tree layers {treeLayers} and hashBits {provider.HashBits} and good for {provider.MaximumHeight} signatures.");

				(ByteArray privateKey, ByteArray publicKey) = await provider.GenerateKeys(true, progressCallback).ConfigureAwait(false);

				key.HashBits = provider.HashBitsEnum;
				key.TreeHeight = provider.TreeHeight;
				key.TreeLayers = provider.TreeLayers;
				key.KeyType = Enums.KeyTypes.XMSSMT;
				key.WarningHeight = provider.GetKeyUseThreshold(warningLevel);
				key.ChangeHeight = provider.GetKeyUseThreshold(changeLevel);
				key.MaximumHeight = provider.MaximumHeight;
				key.KeyUseIndex = 0;

				key.PrivateKey = privateKey.ToExactByteArrayCopy();
				key.PublicKey = publicKey.ToExactByteArrayCopy();

				privateKey.Return();
				publicKey.Return();
			}

			this.HashKey(key);

			Log.Information("XMSS^MT Keys created");

			return key;
		}

		public IQTeslaWalletKey CreateQTeslaKey(string name, QTESLASecurityCategory.SecurityCategories securityCategory) {
			IQTeslaWalletKey key = this.CreateBasicKey<IQTeslaWalletKey>(name, Enums.KeyTypes.QTESLA);

			this.PrepareQTeslaKey(key, securityCategory);

			Log.Information("QTesla Key created");

			return key;
		}

		public void PrepareQTeslaKey<T>(T key, QTESLASecurityCategory.SecurityCategories securityCategory)
			where T : IQTeslaWalletKey {

			using(QTeslaProvider provider = new QTeslaProvider(securityCategory)) {
				provider.Initialize();

				Log.Information($"Creating a new QTesla key named '{key.Name}' with security category {securityCategory}");

				key.KeyType = Enums.KeyTypes.QTESLA;
				key.SecurityCategory = (byte) securityCategory;

				(SafeArrayHandle privateKey, SafeArrayHandle publicKey) keys = provider.GenerateKeys();
				key.PrivateKey = keys.privateKey.ToExactByteArrayCopy();
				key.PublicKey = keys.publicKey.ToExactByteArrayCopy();

			}

			this.HashKey(key);
		}

		public IQTeslaWalletKey CreatePresentationQTeslaKey(string name) {
			return this.CreateQTeslaKey(name, QTESLASecurityCategory.SecurityCategories.HEURISTIC_I);
		}

		public ISecretWalletKey CreateSuperKey() {
			return this.CreateSecretKey(GlobalsService.SUPER_KEY_NAME, QTESLASecurityCategory.SecurityCategories.HEURISTIC_V);
		}

		public ISecretWalletKey CreateSecretKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null) {

			Log.Information($"Creating a new Secret key named '{name}'. generating qTesla base.");

			ISecretWalletKey key = this.CreateBasicKey<ISecretWalletKey>(name, Enums.KeyTypes.Secret);

			this.PrepareQTeslaKey(key, securityCategorySecret);

			key.KeyType = Enums.KeyTypes.Secret;

			// since secret keys are often chained, here we ensure the new key contains the same general parameters as its previous one
			if(previousKey != null) {
				key.KeyAddress = previousKey.KeyAddress;
				key.AccountUuid = previousKey.AccountUuid;
			}

			Log.Information("Secret Key created");

			return key;
		}

		public ISecretComboWalletKey CreateSecretComboKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null) {

			Log.Information($"Creating a new Secret combo key named '{name}'. generating qTesla base.");

			ISecretComboWalletKey key = this.CreateBasicKey<ISecretComboWalletKey>(name, Enums.KeyTypes.SecretCombo);

			this.PrepareQTeslaKey(key, securityCategorySecret);

			key.KeyType = Enums.KeyTypes.SecretCombo;

			key.PromisedNonce1 = GlobalRandom.GetNextLong();
			key.PromisedNonce2 = GlobalRandom.GetNextLong();

			// since secret keys are often chained, here we ensure the new key contains the same general parameters as its previous one
			if(previousKey != null) {
				key.KeyAddress = previousKey.KeyAddress;
				key.AccountUuid = previousKey.AccountUuid;
			}

			Log.Information("Secret combo Key created");

			return key;
		}

		public ISecretDoubleWalletKey CreateSecretDoubleKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, QTESLASecurityCategory.SecurityCategories securityCategorySecond, ISecretDoubleWalletKey previousKey = null) {

			Log.Information($"Creating a new Secret double key named '{name}'. generating qTesla base.");
			ISecretDoubleWalletKey key = this.CreateBasicKey<ISecretDoubleWalletKey>(name, Enums.KeyTypes.SecretDouble);

			this.PrepareQTeslaKey(key, securityCategorySecret);

			key.KeyType = Enums.KeyTypes.SecretDouble;

			key.PromisedNonce1 = GlobalRandom.GetNextLong();
			key.PromisedNonce2 = GlobalRandom.GetNextLong();

			key.SecondKey = (QTeslaWalletKey) this.CreateQTeslaKey(name, securityCategorySecond);

			// since secret keys are often chained, here we ensure the new key contains the same general parameters as its previous one
			if(previousKey != null) {
				key.KeyAddress = previousKey.KeyAddress;
				key.AccountUuid = previousKey.AccountUuid;
			}

			Log.Information("Secret double Key created");

			return key;
		}

		/// <summary>
		///     Here, we sign a message with the
		/// </summary>
		/// <param name="key"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<SafeArrayHandle> PerformCryptographicSignature(Guid accountUuid, string keyName, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {

			this.EnsureWalletIsLoaded();
			await this.EnsureWalletKeyIsReady(accountUuid, keyName, lockContext).ConfigureAwait(false);

			IWalletKey key = await this.LoadKey<IWalletKey>(k => {
				return k;
			}, accountUuid, keyName, lockContext).ConfigureAwait(false);

			if(key == null) {
				throw new ApplicationException($"The key named '{keyName}' could not be loaded. Make sure it is available before progressing.");
			}

			return await this.PerformCryptographicSignature(key, message, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}

		public virtual async Task<SafeArrayHandle> PerformCryptographicSignature(IWalletKey key, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {

			this.EnsureWalletIsLoaded();
			await this.EnsureWalletKeyIsReady(key.AccountUuid, key.Name, lockContext).ConfigureAwait(false);

			SafeArrayHandle signature = null;

			if(key is IXmssWalletKey xmssWalletKey) {

				// check if we reached the maximum use of our key
				bool keyStillUsable = xmssWalletKey.KeyUseIndex < xmssWalletKey.ChangeHeight || allowPassKeyLimit;
				bool keyMaxedOut = xmssWalletKey.KeyUseIndex > xmssWalletKey.MaximumHeight;

				if(keyStillUsable && !keyMaxedOut) {

					XMSSProviderBase provider = null;

					if(key is IXmssMTWalletKey xmssMTWalletKey && key.KeyType == Enums.KeyTypes.XMSSMT) {
						provider = new XMSSMTProvider(xmssMTWalletKey.HashBits, Enums.ThreadMode.Half, xmssMTWalletKey.TreeHeight, xmssMTWalletKey.TreeLayers);
					} else {
						provider = new XMSSProvider(xmssWalletKey.HashBits, xmssWalletKey.TreeHeight);
					}

					(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey) result;

					using(provider) {

						provider.Initialize();

						if(provider is XMSSMTProvider xmssmtProvider) {
							result = await xmssmtProvider.Sign(message, ByteArray.Wrap(key.PrivateKey)).ConfigureAwait(false);
						} else if(provider is XMSSProvider xmssProvider) {
							result = await xmssProvider.Sign(message, ByteArray.Wrap(key.PrivateKey)).ConfigureAwait(false);
						} else {
							throw new InvalidOperationException();
						}
					}

					signature = result.signature;

					// now we increment out key and its private key
					xmssWalletKey.PrivateKey = result.nextPrivateKey.ToExactByteArrayCopy();
					xmssWalletKey.KeyUseIndex += 1;

					result.nextPrivateKey.Return();

					// save the key change
					await this.UpdateKey(key, lockContext).ConfigureAwait(false);
				}

				List<(Guid accountUuid, string name)> forcedKeys = new List<(Guid accountUuid, string name)>();

				// we are about to use this key, let's make sure we check it to eliminate any applicable timeouts
				forcedKeys.Add((key.AccountUuid, key.Name));

				await this.ResetAllTimedOut(lockContext, forcedKeys).ConfigureAwait(false);

				if(key.Status != Enums.KeyStatus.Changing) {
					// Here we trigger the key change workflow, we must change the key, its time adn we wont trust the user to do it in time at this point. they were warned already

					if(keyMaxedOut) {
						Log.Fatal($"Key named {key.Name} has reached end of life. It must be changed with a super key.");
					} else if(xmssWalletKey.KeyUseIndex >= xmssWalletKey.ChangeHeight) {
						Log.Warning($"Key named {key.Name} has reached end of life. An automatic key change is being performed. You can not use the key until the change is fully confirmed.");

						this.KeyUseMaximumLevelReached(key.KeyAddress.OrdinalId, xmssWalletKey.KeyUseIndex, xmssWalletKey.WarningHeight, xmssWalletKey.ChangeHeight, new CorrelationContext());
					} else if(xmssWalletKey.KeyUseIndex >= xmssWalletKey.WarningHeight) {
						Log.Warning($"Key named {key.Name}  nearing its end of life. An automatic key change is being performed. You can keep using it until the change is fully confirmed.");
						this.KeyUseWarningLevelReached(key.KeyAddress.OrdinalId, xmssWalletKey.KeyUseIndex, xmssWalletKey.WarningHeight, xmssWalletKey.ChangeHeight, new CorrelationContext());
					}
				}

				if(!keyStillUsable) {
					// we have reached the maximum use amount for this key. we can't sign anything else until a key change happens
					throw new ApplicationException("Your xmss key has reached it's full use. A key change must now be performed!");
				}

			} else if(key is ISecretDoubleWalletKey qsecretDoubleWalletKey) {
				SafeArrayHandle signature1 = null;
				SafeArrayHandle signature2 = null;

				using(QTeslaProvider provider = new QTeslaProvider(qsecretDoubleWalletKey.SecurityCategory)) {
					provider.Initialize();

					// thats it, perform the signature and increment our private key
					signature1 = await provider.Sign(message, ByteArray.Wrap(qsecretDoubleWalletKey.PrivateKey)).ConfigureAwait(false);
				}

				using(QTeslaProvider provider = new QTeslaProvider(qsecretDoubleWalletKey.SecondKey.SecurityCategory)) {
					provider.Initialize();

					// thats it, perform the signature and increment our private key
					signature2 = await provider.Sign(message, ByteArray.Wrap(qsecretDoubleWalletKey.SecondKey.PrivateKey)).ConfigureAwait(false);
				}

				using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

				dehydrator.Write(signature1);
				dehydrator.Write(signature2);

				signature = dehydrator.ToArray();

			} else if(key is IQTeslaWalletKey qTeslaWalletKey) {
				using QTeslaProvider provider = new QTeslaProvider(qTeslaWalletKey.SecurityCategory);

				provider.Initialize();

				// thats it, perform the signature and increment our private key
				signature = await provider.Sign(message, ByteArray.Wrap(qTeslaWalletKey.PrivateKey)).ConfigureAwait(false);

			} else {
				throw new ApplicationException("Invalid key type provided");
			}

			return signature;
		}

		protected virtual void KeyUseWarningLevelReached(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			// do nothing
			this.LaunchChangeKeyWorkflow(changeKeyOrdinal, keyUseIndex, warningHeight, maximumHeight, correlationContext);

		}

		protected virtual void KeyUseMaximumLevelReached(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			this.LaunchChangeKeyWorkflow(changeKeyOrdinal, keyUseIndex, warningHeight, maximumHeight, correlationContext);
		}

		protected virtual void LaunchChangeKeyWorkflow(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			var changeKeyTransactionWorkflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateChangeKeyTransactionWorkflow(changeKeyOrdinal, "automatically triggered keychange", correlationContext);

			this.centralCoordinator.PostWorkflow(changeKeyTransactionWorkflow);
		}

		public virtual async Task<bool> ResetAllTimedOut(LockContext lockContext, List<(Guid accountUuid, string name)> forcedKeys = null) {

			this.EnsureWalletIsLoaded();

			var synced = await this.Synced(lockContext).ConfigureAwait(false);

			if(!synced.HasValue || !synced.Value) {
				// we cant do it if not synced, we will give a chance for the transactions to arrive
				return false;
			}

			bool changed = await this.ResetTimedOutWalletEntries(lockContext, forcedKeys).ConfigureAwait(false);

			var result = await this.ClearTimedOutTransactions(lockContext).ConfigureAwait(false);

			if(result.Any(e => e.Value != 0)) {
				changed = true;
			}

			return changed;
		}

		/// <summary>
		/// update wallet accounts adn keys for any timeout in transaction operations.
		/// </summary>
		/// <param name="forcedKeys"></param>
		public virtual async Task<bool> ResetTimedOutWalletEntries(LockContext lockContext, List<(Guid accountUuid, string name)> forcedKeys = null) {
			this.EnsureWalletIsLoaded();

			var synced = await this.Synced(lockContext).ConfigureAwait(false);

			if(!synced.HasValue || !synced.Value) {
				// we cant do it if not synced, we will give a chance for the transactions to arrive
				return false;
			}

			// let's use the last block timestamp as a limit, in case its not up to date
			DateTime lastBlockTimestamp = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockTimestamp;

			var accounts = await this.GetAllAccounts(lockContext).ConfigureAwait(false);

			bool changed = false;

			foreach(var account in accounts) {
				//now we ttake care of presentation transactions
				if(account.Status == Enums.PublicationStatus.Dispatched && account.PresentationTransactionTimeout.HasValue && account.PresentationTransactionTimeout.Value < lastBlockTimestamp) {
					// ok, this is a timeout, we reset it
					account.PresentationTransactionTimeout = null;
					account.PresentationTransactionId = null;
					account.Status = Enums.PublicationStatus.New;
					changed = true;
				}
			}

			changed = false;

			foreach(var account in accounts) {
				// and finally keys if we can
				foreach(var key in account.Keys) {

					if(forcedKeys != null && forcedKeys.Contains((account.AccountUuid, key.Name))) {
						// ok, this key MUST be checked
						this.EnsureKeyFileIsPresent(account.AccountUuid, key.Name, 1, lockContext);
						this.EnsureKeyPassphrase(account.AccountUuid, key.Name, 1, lockContext);
					}

					if(this.IsKeyFileIsPresent(account.AccountUuid, key.Name, 1, lockContext) && this.IsKeyPassphraseValid(account.AccountUuid, key.Name, 1, lockContext)) {
						using var walletKey = await this.LoadKey(account.AccountUuid, key.Name, lockContext).ConfigureAwait(false);

						if(walletKey.Status == Enums.KeyStatus.Changing && walletKey.KeyChangeTimeout.HasValue && walletKey.KeyChangeTimeout.Value < lastBlockTimestamp) {

							walletKey.Status = Enums.KeyStatus.Ok;
							walletKey.KeyChangeTimeout = null;
							walletKey.ChangeTransactionId = null;
							changed = true;
						}

					}
				}
			}

			return changed;
		}

		/// <summary>
		/// here we remove all timed out transactions from the wallet
		/// </summary>
		public virtual async Task<Dictionary<AccountId, int>> ClearTimedOutTransactions(LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			var synced = await this.Synced(lockContext).ConfigureAwait(false);

			if(!synced.HasValue || !synced.Value) {
				// we cant do it if not synced, we will give a chance for the transactions to arrive
				return new Dictionary<AccountId, int>();
			}

			// let's use the last block timestamp as a limit, in case its not up to date
			DateTime lastBlockTimestamp = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockTimestamp;

			var accounts = await this.GetAllAccounts(lockContext).ConfigureAwait(false);

			bool changed = false;

			Dictionary<AccountId, int> totals = new Dictionary<AccountId, int>();

			foreach(var account in accounts) {

				int total = 0;
				IWalletTransactionCacheFileInfo transactionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionCacheInfo;

				total = await transactionCacheFileInfo.ClearTimedOutTransactions(lastBlockTimestamp, lockContext).ConfigureAwait(false);

				if(total != 0) {
					changed = true;
				}

				totals.Add(account.GetAccountId(), total);

			}

			return totals;
		}

	#endregion

	#region external API

		/// <summary>
		///     Query the entire wallet transaction history
		/// </summary>
		/// <param name="taskStasher"></param>
		/// <returns></returns>
		public virtual async Task<List<WalletTransactionHistoryHeaderAPI>> APIQueryWalletTransactionHistory(Guid accountUuid, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			var results = await this.WalletFileInfo.Accounts[accountUuid].WalletTransactionHistoryInfo.RunQuery<WalletTransactionHistoryHeaderAPI, WalletTransactionHistory>(caches => caches.Select(t => {

				TransactionId transactionId = new TransactionId(t.TransactionId);
				var version = new ComponentVersion<TransactionType>(t.Version);

				return new WalletTransactionHistoryHeaderAPI {
					TransactionId = t.TransactionId, Sender = transactionId.Account.ToString(), Timestamp = TimeService.FormatDateTimeStandardUtc(t.Timestamp), Status = t.Status,
					Version = new VersionAPI() {TransactionType = version.Type.Value.Value, Major = version.Major.Value, Minor = version.Minor.Value}, Local = t.Local, Note = t.Note, Recipient = t.Recipient
				};
			}).OrderByDescending(t => t.Timestamp).ToList(), lockContext).ConfigureAwait(false);

			return results.ToList();

		}

		public virtual async Task<WalletTransactionHistoryDetailsAPI> APIQueryWalletTransactionHistoryDetails(Guid accountUuid, string transactionId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(accountUuid == Guid.Empty) {
				accountUuid = await this.GetAccountUuid(lockContext).ConfigureAwait(false);
			}

			var results = await this.WalletFileInfo.Accounts[accountUuid].WalletTransactionHistoryInfo.RunQuery<WalletTransactionHistoryDetailsAPI, WalletTransactionHistory>(caches => caches.Where(t => t.TransactionId == transactionId).Select(t => {

				var version = new ComponentVersion<TransactionType>(t.Version);

				return new WalletTransactionHistoryDetailsAPI {
					TransactionId = t.TransactionId, Sender = new TransactionId(t.TransactionId).Account.ToString(), Timestamp = TimeService.FormatDateTimeStandardUtc(t.Timestamp), Status = t.Status,
					Version = new VersionAPI() {TransactionType = version.Type.Value.Value, Major = version.Major.Value, Minor = version.Minor.Value}, Recipient = t.Recipient, Contents = t.Contents, Local = t.Local,
					Note = t.Note
				};
			}).ToList(), lockContext).ConfigureAwait(false);

			return results.SingleOrDefault();

		}

		/// <summary>
		///     Get the list of all accounts in the wallet
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<WalletInfoAPI> APIQueryWalletInfoAPI(LockContext lockContext) {

			WalletInfoAPI walletInfoApi = new WalletInfoAPI();

			walletInfoApi.WalletExists = await this.WalletFileExists(lockContext).ConfigureAwait(false);
			walletInfoApi.IsWalletLoaded = this.IsWalletLoaded;
			walletInfoApi.WalletPath = this.GetChainDirectoryPath();

			if(walletInfoApi.WalletExists && walletInfoApi.IsWalletLoaded) {
				walletInfoApi.WalletEncrypted = await this.IsWalletEncrypted(lockContext).ConfigureAwait(false);
			}

			return walletInfoApi;
		}

		/// <summary>
		///     Get the list of all accounts in the wallet
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<List<WalletAccountAPI>> APIQueryWalletAccounts(LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			Guid activeAccountUuid = (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).AccountUuid;

			var apiAccounts = new List<WalletAccountAPI>();

			foreach(IWalletAccount account in await this.GetAccounts(lockContext).ConfigureAwait(false)) {

				apiAccounts.Add(new WalletAccountAPI {
					AccountUuid = account.AccountUuid, AccountId = account.GetAccountId().ToString(), FriendlyName = account.FriendlyName, Status = (int) account.Status,
					IsActive = account.AccountUuid == activeAccountUuid
				});
			}

			return apiAccounts;

		}

		/// <summary>
		///     Get the details of an account
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<WalletAccountDetailsAPI> APIQueryWalletAccountDetails(Guid accountUuid, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			Guid activeAccountUuid = (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).AccountUuid;
			IWalletAccount account = await this.GetWalletAccount(accountUuid, lockContext).ConfigureAwait(false);

			var apiAccounts = new List<WalletAccountAPI>();

			return new WalletAccountDetailsAPI {
				AccountUuid = account.AccountUuid, AccountId = account.PublicAccountId?.ToString(), AccountHash = account.AccountUuidHash?.ToString(), FriendlyName = account.FriendlyName,
				Status = (int) account.Status, IsActive = account.AccountUuid == activeAccountUuid, AccountType = (int) account.WalletAccountType, TrustLevel = account.TrustLevel,
				DeclarationBlockId = account.ConfirmationBlockId, KeysEncrypted = account.KeysEncrypted, Correlated = account.Correlated
			};

		}

		public async Task<TransactionId> APIQueryWalletAccountPresentationTransactionId(Guid accountUuid, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = await this.GetWalletAccount(accountUuid, lockContext).ConfigureAwait(false);

			if(account == null) {
				throw new ApplicationException($"Failed to load account with Uuid {accountUuid}");
			}

			return account.PresentationTransactionId.Clone;

		}

	#endregion

	#region walletservice methods

		public async Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false) {
			this.EnsureWalletIsLoaded();

			//TODO: make sure we confirm our signature height in the wallet with the recorded one on chain. To prevent mistaken wallet copies.
			IWalletAccount activeAccount = await GetActiveAccount(lockContext).ConfigureAwait(false);

			using IWalletKey key = await this.LoadKey<IWalletKey>(k => k, activeAccount.AccountUuid, keyName, lockContext).ConfigureAwait(false);

			if(key == null) {
				throw new ApplicationException($"The key named '{keyName}' could not be loaded. Make sure it is available before progressing.");
			}

			// thats it, lets perform the signature
			if(key is IXmssWalletKey xmssWalletKey) {
				return await this.SignTransactionXmss(transactionHash, xmssWalletKey, lockContext, allowPassKeyLimit).ConfigureAwait(false);
			}

			return await this.SignTransaction(transactionHash, key, lockContext).ConfigureAwait(false);

		}

		/// <summary>
		///     This version will ensure track key usage heights
		/// </summary>
		/// <param name="taskStasher"></param>
		/// <param name="transactionHash"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public async Task<SafeArrayHandle> SignTransactionXmss(SafeArrayHandle transactionHash, IXmssWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false) {
			this.EnsureWalletIsLoaded();

			//TODO: we would want to do it for (potentially) sphincs and xmssmt too
			ChainConfigurations configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration();

			if(configuration.UseKeyLog && configuration.KeySecurityConfigurations.EnableKeyHeightChecks) {
				KeyUseIndexSet lastSyncedKeyUse = await this.GetChainStateLastSyncedKeyHeight(key, lockContext).ConfigureAwait(false);

				if(lastSyncedKeyUse.IsSet && new KeyUseIndexSet(key.KeySequenceId, key.KeyUseIndex, key.KeyAddress.OrdinalId) < lastSyncedKeyUse) {
					throw new ApplicationException("Your key height is lower than the chain key use height. This is very serious. Are you using an older copy of your regular wallet?");
				}
			}

			// thats it, lets perform the signature
			return await this.SignTransaction(transactionHash, key, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}

		public Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false) {

			// thats it, lets perform the signature
			return this.PerformCryptographicSignature(key, transactionHash, lockContext, allowPassKeyLimit);
		}

		public async Task<SafeArrayHandle> SignMessageXmss(SafeArrayHandle messageHash, IXmssWalletKey key, LockContext lockContext) {

			// thats it, lets perform the signature
			SafeArrayHandle results = await this.SignTransactionXmss(messageHash, key, lockContext).ConfigureAwait(false);

			// for messages, we never get confirmation, so we update the key height right away
			await this.UpdateLocalChainStateKeyHeight(key, lockContext).ConfigureAwait(false);

			// and confirmation too
			await this.UpdateLocalChainStateTransactionKeyLatestSyncHeight(key.AccountUuid, new KeyUseIndexSet(key.KeySequenceId, key.KeyUseIndex, key.KeyAddress.OrdinalId), lockContext).ConfigureAwait(false);

			return results;
		}

		public async Task<SafeArrayHandle> SignMessageXmss(Guid accountUuid, SafeArrayHandle message, LockContext lockContext) {

			if(accountUuid == Guid.Empty) {
				accountUuid = await this.GetAccountUuid(lockContext).ConfigureAwait(false);
			}

			using IXmssWalletKey key = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(accountUuid, GlobalsService.MESSAGE_KEY_NAME, lockContext).ConfigureAwait(false);

			// and sign the whole thing with our key
			return await this.SignMessageXmss(message, key, lockContext).ConfigureAwait(false);

		}

		public Task<SafeArrayHandle> SignMessage(SafeArrayHandle messageHash, IWalletKey key, LockContext lockContext) {

			// thats it, lets perform the signature
			return this.SignTransaction(messageHash, key, lockContext);
		}

		public virtual async Task<SynthesizedBlock> ConvertApiSynthesizedBlock(SynthesizedBlockAPI synthesizedBlockApi, LockContext lockContext) {
			SynthesizedBlock synthesizedBlock = this.CreateSynthesizedBlockFromApi(synthesizedBlockApi);

			synthesizedBlock.BlockId = synthesizedBlockApi.BlockId;

			BrotliCompression compressor = new BrotliCompression();

			foreach(var apiTransaction in synthesizedBlockApi.ConfirmedGeneralTransactions) {
				IDehydratedTransaction dehydratedTransaction = new DehydratedTransaction();

				SafeArrayHandle bytes = compressor.Decompress(ByteArray.Wrap(apiTransaction.Value));
				dehydratedTransaction.Rehydrate(bytes);
				bytes.Return();

				ITransaction transaction = null;

				try {
					transaction = dehydratedTransaction.RehydrateTransaction(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);
				} catch(UnrecognizedElementException urex) {

					throw;
				}

				synthesizedBlock.ConfirmedGeneralTransactions.Add(transaction.TransactionId, transaction);
			}

			synthesizedBlock.RejectedTransactions.AddRange(synthesizedBlockApi.RejectedTransactions.Select(t => new RejectedTransaction {TransactionId = new TransactionId(t.Key), Reason = (RejectionCode) t.Value}));

			AccountId accountId = null;

			bool hasPublicAccount = !string.IsNullOrWhiteSpace(synthesizedBlockApi.AccountId);
			bool hasAcountHash = !string.IsNullOrWhiteSpace(synthesizedBlockApi.AccountHash);

			if(hasPublicAccount || hasAcountHash) {
				var accounts = await this.GetAccounts(lockContext).ConfigureAwait(false);

				if(hasPublicAccount) {
					accountId = new AccountId(synthesizedBlockApi.AccountId);

					if(accounts.All(a => a.PublicAccountId != accountId)) {
						throw new ApplicationException();
					}

					synthesizedBlock.AccountType = SynthesizedBlock.AccountIdTypes.Public;
				} else if(hasAcountHash) {
					accountId = new AccountId(synthesizedBlockApi.AccountHash);

					if(accounts.All(a => a.AccountUuidHash != accountId)) {
						throw new ApplicationException();
					}

					synthesizedBlock.AccountType = SynthesizedBlock.AccountIdTypes.Hash;
				}

				SynthesizedBlock.SynthesizedBlockAccountSet synthesizedBlockAccountSet = new SynthesizedBlock.SynthesizedBlockAccountSet();

				foreach(var apiTransaction in synthesizedBlockApi.ConfirmedTransactions) {

					IDehydratedTransaction dehydratedTransaction = new DehydratedTransaction();

					SafeArrayHandle bytes = compressor.Decompress(ByteArray.Wrap(apiTransaction.Value));
					dehydratedTransaction.Rehydrate(bytes);
					bytes.Return();

					try {
						ITransaction transaction = dehydratedTransaction.RehydrateTransaction(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

						if(transaction.TransactionId.Account == accountId) {
							synthesizedBlockAccountSet.ConfirmedLocalTransactions.Add(transaction.TransactionId, transaction);
						} else {
							synthesizedBlockAccountSet.ConfirmedExternalsTransactions.Add(transaction.TransactionId, transaction);
						}

						synthesizedBlock.ConfirmedTransactions.Add(transaction.TransactionId, transaction);
					} catch(UnrecognizedElementException urex) {

						throw;
					}
				}

				synthesizedBlock.AccountScopped.Add(accountId, synthesizedBlockAccountSet);
				synthesizedBlock.Accounts.Add(accountId);

			}

			return synthesizedBlock;
		}

		protected abstract SynthesizedBlock CreateSynthesizedBlockFromApi(SynthesizedBlockAPI synthesizedBlockApi);

		public abstract SynthesizedBlockAPI DeserializeSynthesizedBlockAPI(string synthesizedBlock);

	#endregion

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.centralCoordinator.ShutdownRequested -= this.CentralCoordinatorOnShutdownRequested;
			}

			this.IsDisposed = true;
		}

		~WalletProvider() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

		public Task Pause() {
			using(this.locker.Lock()) {
				this.paused = true;
			}

			return this.WaitTransactionCompleted();
		}

		public Task Resume() {
			using(this.locker.Lock()) {
				this.paused = false;
			}

			return Task.CompletedTask;
		}
	}

}