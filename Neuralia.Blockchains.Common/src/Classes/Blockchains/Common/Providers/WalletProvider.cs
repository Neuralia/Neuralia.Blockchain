using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
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
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.Archiving;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Transactions;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;
using static Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI.SynthesizedBlockAPI;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public static class WalletProvider {
		
		public enum BackupTypes {
			CopyFull,
			CopyPartial,
			Essence,
			SuperKey
		}
		
		public enum HashTypes {
			Sha2,
			Sha3
		}

		public const int DEFAULT_KEY_HASH_BITS = 256;
		public const int DEFAULT_KEY_BACKUP_HASH_BITS = 256;

		public const int TRANSACTION_KEY_HASH_BITS = 256;
		public const int MESSAGE_KEY_HASH_BITS = 256;
		public const int CHANGE_KEY_HASH_BITS = 256;
		public const int SUPER_KEY_HASH_BITS = 512;

		public const int VALIDATOR_SIGNATURE_KEY_HASH_BITS = 256;
		
		public const byte TRANSACTION_KEY_NONCES_EXPONENT = XMSSEngine.DEFAULT_NONCES_EXPONENT;
		public const byte MESSAGE_KEY_NONCES_EXPONENT = XMSSEngine.DEFAULT_NONCES_EXPONENT;
		public const byte CHANGE_KEY_NONCES_EXPONENT = XMSSEngine.DEFAULT_NONCES_EXPONENT;
		public const byte SUPER_KEY_NONCES_EXPONENT = XMSSEngine.DEFAULT_NONCES_EXPONENT;
		public const byte VALIDATOR_SIGNATURE_NONCES_EXPONENT = XMSSEngine.DEFAULT_NONCES_EXPONENT;
		
		public const int TRANSACTION_KEY_XMSS_TREE_HEIGHT = XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT;

#if DEVNET || TESTNET
		public const byte MINIMAL_XMSS_KEY_HEIGHT = 4;
		public const byte MINIMAL_XMSSMT_KEY_HEIGHT = 4;
		public const byte MINIMAL_XMSSMT_KEY_LAYER = 2;
		public const int MINIMAL_KEY_SEED_SIZE = 500;
#else
		#if COLORADO_EXCLUSION
		public const byte MINIMAL_XMSS_KEY_HEIGHT = 4;
		public const byte MINIMAL_XMSSMT_KEY_HEIGHT = 6;
		public const byte MINIMAL_XMSSMT_KEY_LAYER = 2;
		public const int MINIMAL_KEY_SEED_SIZE = 50;
		#else
		public const byte MINIMAL_XMSS_KEY_HEIGHT = 9;
		public const byte MINIMAL_XMSSMT_KEY_HEIGHT = 16;
		public const byte MINIMAL_XMSSMT_KEY_LAYER = 2;
		public const int MINIMAL_KEY_SEED_SIZE = 500;
		#endif
#endif

		public static Enums.KeyHashType ConvertFullHashType(int bitType, HashTypes hashType) {
			Enums.KeyHashType fullHashbits = Enums.KeyHashType.SHA2_256;

			if((hashType == WalletProvider.HashTypes.Sha2) && (bitType == 256)) {
				fullHashbits = Enums.KeyHashType.SHA2_256;
			} else if((hashType == WalletProvider.HashTypes.Sha2) && (bitType == 512)) {
				fullHashbits = Enums.KeyHashType.SHA2_512;
			} else if((hashType == WalletProvider.HashTypes.Sha3) && (bitType == 256)) {
				fullHashbits = Enums.KeyHashType.SHA3_256;
			} else if((hashType == WalletProvider.HashTypes.Sha3) && (bitType == 512)) {
				fullHashbits = Enums.KeyHashType.SHA3_512;
			} 

			return fullHashbits;
		}
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

		IWalletKey CreateBasicKey(string name, CryptographicKeyType keyType);

		T CreateBasicKey<T>(string name, CryptographicKeyType keyType)
			where T : IWalletKey;

		Task GenerateXmssKeyIndexNodeCache(string accountCode, byte ordinal, long index, LockContext lockContext = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, int hashBits, WalletProvider.HashTypes HashType, int backupHashBits, WalletProvider.HashTypes backupHashType, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, bool? enableCache = null, Action<XMSSProvider> prepare = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, Enums.KeyHashType hashbits, Enums.KeyHashType backupHashbits, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null);
		Task<IXmssMTWalletKey> CreateXmssmtKey(string name, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null);
		Task<IXmssMTWalletKey> CreateXmssmtKey(string name, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null);
		Task<IXmssMTWalletKey> CreateXmssmtKey(string name, byte treeHeight, byte treeLayers, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null);
		Task<INTRUPrimeWalletKey> CreateNTRUPrimeKey(string name, NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes strength);
		Task<SafeArrayHandle> CreateNTRUPrimeAppointmentRequestKey(LockContext lockContext);
	}

	public interface IReadonlyWalletProvider {
		Task<bool> IsWalletEncrypted(LockContext lockContext);
		Task<bool> IsWalletAccountLoaded(LockContext lockContext);
		Task<bool> WalletFileExists(LockContext lockContext);
		Task<bool> WalletFullyCreated(LockContext lockContext);
		Task<long?> LowestAccountBlockSyncHeight(LockContext lockContext);
		Task<bool?> Synced(LockContext lockContext);
		Task<bool> WalletContainsAccount(string accountCode, LockContext lockContext);
		Task<List<IWalletAccount>> GetWalletSyncableAccounts(long blockId, long previousSyncedBlockId, LockContext lockContext);
		Task<IAccountFileInfo> GetAccountFileInfo(string accountCode, LockContext lockContext);
		Task<List<IWalletAccount>> GetAccounts(LockContext lockContext);
		Task<List<IWalletAccount>> GetAllAccounts(LockContext lockContext);
		Task<string> GetAccountCode(LockContext lockContext);
		Task<AccountId> GetPublicAccountId(LockContext lockContext);
		Task<AccountId> GetPublicAccountId(string accountCode, LockContext lockContext);
		Task<AccountId> GetInitiationId(LockContext lockContext);
		Task<bool> IsDefaultAccountPublished(LockContext lockContext);
		Task<bool> IsAccountPublished(string accountCode, LockContext lockContext);
		Task<bool> HasAccount(LockContext lockContext);
		Task<IWalletAccount> GetActiveAccount(LockContext lockContext);
		Task<IWalletAccount> GetWalletAccountByName(string name, LockContext lockContext);
		Task<IWalletAccount> GetWalletAccount(string accountCode, LockContext lockContext);
		Task<IWalletAccount> GetWalletAccount(AccountId accountId, LockContext lockContext);

		Task<List<WalletTransactionHistoryHeaderAPI>> APIQueryWalletTransactionHistory(string accountCode, LockContext lockContext);
		Task<WalletTransactionHistoryDetailsAPI> APIQueryWalletTransactionHistoryDetails(string accountCode, string transactionId, LockContext lockContext);
		Task<WalletInfoAPI> APIQueryWalletInfoAPI(LockContext lockContext);
		Task<List<WalletAccountAPI>> APIQueryWalletAccounts(LockContext lockContext);
		Task<WalletAccountDetailsAPI> APIQueryWalletAccountDetails(string accountCode, LockContext lockContext);
		Task<WalletAccountAppointmentDetailsAPI> APIQueryWalletAccountAppointmentDetails(string accountCode, LockContext lockContext);
		Task<TransactionId> APIQueryWalletAccountPresentationTransactionId(string accountCode, LockContext lockContext);
		
		Task<AccountAppointmentConfirmationResultAPI> APIQueryAppointmentConfirmationResult(string accountCode, LockContext lockContext);
		Task<bool> ClearAppointment(string accountCode, LockContext lockContext, bool force = false);
		Task<AccountCanPublishAPI> APICanPublishAccount(string accountCode, LockContext lockContext);
		Task<bool> SetSMSConfirmationCode(string accountCode, long confirmationCode, LockContext lockContext);
		Task<List<TransactionId>> GetElectionCacheTransactions(IWalletAccount account, LockContext lockContext);

		BlockId GetHighestCachedSynthesizedBlockId(LockContext lockContext);
		bool IsSynthesizedBlockCached(long blockId, LockContext lockContext);
		public SynthesizedBlock ExtractCachedSynthesizedBlock(long blockId);
		public List<SynthesizedBlock> GetCachedSynthesizedBlocks(long minimumBlockId, LockContext lockContext);

		Task<IWalletAccountSnapshot> GetWalletFileInfoAccountSnapshot(string accountCode, LockContext lockContext);

		Task<IWalletAccountSnapshot> GetAccountSnapshot(AccountId accountId, LockContext lockContext);
		Task<DistilledAppointmentContext> GetDistilledAppointmentContextFile();
	}

	public interface IWalletProviderWrite : IChainProvider {

		Task WriteDistilledAppointmentContextFile(DistilledAppointmentContext distilledAppointmentContext);
		void ClearDistilledAppointmentContextFile();
		Task UpdateMiningStatistics(AccountId accountId, Enums.MiningTiers miningTiers, Action<WalletElectionsMiningSessionStatistics> sessionCallback, Action<WalletElectionsMiningAggregateStatistics> totalCallback, LockContext lockContext, bool resetSession = false);
		Task StopSessionMiningStatistics(AccountId accountId, LockContext lockContext);
		
		Task<(MiningStatisticSessionAPI session, MiningStatisticAggregateAPI aggregate)> QueryMiningStatistics(AccountId miningAccountId, Enums.MiningTiers miningTiers, LockContext lockContext);
		
		Task<Dictionary<AccountId, int>> ClearTimedOutTransactions(LockContext lockContext);
		Task<bool> ResetTimedOutWalletEntries(LockContext lockContext, List<(string accountCode, string name)> forcedKeys = null);
		Task<bool> ResetAllTimedOut(LockContext lockContext, List<(string accountCode, string name)> forcedKeys = null);

		Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, LockContext lockContext);
		Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, LockContext lockContext);
		Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot, LockContext lockContext);
		Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot, LockContext lockContext);
		Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshotEntry(LockContext lockContext);
		Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshotEntry(LockContext lockContext);
		Task ChangeAccountsCorrelation(ImmutableList<AccountId> enableAccounts, ImmutableList<AccountId> disableAccounts, LockContext lockContext);
		Task CacheSynthesizedBlock(SynthesizedBlock synthesizedBlock, LockContext lockContext);
		Task CleanSynthesizedBlockCache(LockContext lockContext);
		void ClearSynthesizedBlocksCache();
		
		event Delegates.RequestCopyWalletFileDelegate CopyWalletRequest;
		event Delegates.RequestPassphraseDelegate WalletPassphraseRequest;
		event Delegates.RequestKeyPassphraseDelegate WalletKeyPassphraseRequest;
		event Delegates.RequestCopyKeyFileDelegate WalletCopyKeyFileRequest;
		Task CreateNewEmptyWallet(CorrelationContext correlationContext, bool encryptWallet, string passphrase, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, LockContext lockContext);
		Task<bool> AllAccountsHaveSyncStatus(SynthesizedBlock block, long previousSyncedBlockId, WalletAccountChainState.BlockSyncStatuses status, LockContext lockContext);
		Task<bool> AllAccountsUpdatedWalletBlock(SynthesizedBlock block, long previousSyncedBlockId, LockContext lockContext);
		Task UpdateWalletBlock(SynthesizedBlock synthesizedBlock, long previousSyncedBlockId, Func<SynthesizedBlock, LockContext, Task> callback, LockContext lockContext);
		Task<bool> AllAccountsWalletKeyLogSet(SynthesizedBlock block, long previousSyncedBlockId, LockContext lockContext);
		Task<bool> SetActiveAccount(string accountCode, LockContext lockContext);
		Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, string accountName, Enums.AccountTypes accountType, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null);
		Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, Enums.AccountTypes accountType, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null);
		Task UpdateWalletSnapshotFromDigest(IAccountSnapshotDigestChannelCard accountCard, LockContext lockContext);
		Task UpdateWalletSnapshotFromDigest(IStandardAccountSnapshotDigestChannelCard accountCard, LockContext lockContext);
		Task UpdateWalletSnapshotFromDigest(IJointAccountSnapshotDigestChannelCard accountCard, LockContext lockContext);
		Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, LockContext lockContext);
		Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, string accountCode, LockContext lockContext);
		Task ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext);
		Task SaveWallet(LockContext lockContext);
		Task<IWalletAccount> CreateNewStandardAccount(string name, Enums.AccountTypes accountType, bool encryptKeys, bool encryptKeysIndividually, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, LockContext lockContext, bool setactive = false);
		Task<bool> CreateNewCompleteStandardAccount(CorrelationContext correlationContext, string accountName, Enums.AccountTypes accountType, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, Action<IWalletAccount> accountCreatedCallback = null);
		Task<bool> CreateNewCompleteStandardAccount(CorrelationContext correlationContext, string accountName, Enums.AccountTypes accountType, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext);
		Task InsertKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, IdKeyUseIndexSet idKeyUseIndexSet, byte keyOrdinalId, LockContext lockContext);
		Task InsertKeyLogBlockEntry(IWalletAccount account, BlockId blockId, byte keyOrdinalId, IdKeyUseIndexSet keyUseIndex, LockContext lockContext);
		Task InsertKeyLogDigestEntry(IWalletAccount account, int digestId, byte keyOrdinalId, IdKeyUseIndexSet keyUseIndex, LockContext lockContext);
		Task InsertKeyLogEntry(IWalletAccount account, string eventId, Enums.BlockchainEventTypes eventType, byte keyOrdinalId, IdKeyUseIndexSet keyUseIndex, LockContext lockContext);
		Task ConfirmKeyLogBlockEntry(IWalletAccount account, BlockId blockId, long confirmationBlockId, LockContext lockContext);
		Task ConfirmKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, IdKeyUseIndexSet idKeyUseIndexSet, long confirmationBlockId, LockContext lockContext);
		Task<bool> KeyLogTransactionExists(IWalletAccount account, TransactionId transactionId, LockContext lockContext);

		Task SetChainStateHeight(string accountCode, long blockId, LockContext lockContext);
		Task SetChainStateHeight(IWalletAccountChainState chainState, long blockId, LockContext lockContext);

		Task<long> GetChainStateHeight(string accountCode, LockContext lockContext);
		Task<IdKeyUseIndexSet> GetChainStateLastSyncedKeyHeight(IWalletKey key, LockContext lockContext);
		Task<WalletAccount.WalletAccountChainStateMiningCache> GetAccountMiningCache(AccountId accountId, LockContext lockContext);
		Task UpdateAccountMiningCache(AccountId accountId, WalletAccount.WalletAccountChainStateMiningCache miningCache, LockContext lockContext);
		Task UpdateLocalChainStateKeyHeight(IWalletKey key, LockContext lockContext);
		Task<IWalletElectionsHistory> InsertElectionsHistoryEntry(SynthesizedBlock.SynthesizedElectionResult electionResult, SynthesizedBlock synthesizedBlock, AccountId electedAccountId, LockContext lockContext);
		
		Task InsertTransactionHistoryEntry(ITransaction transaction, bool own, string note, BlockId blockId,  WalletTransactionHistory.TransactionStatuses status, LockContext lockContext);
		Task<IWalletTransactionHistoryFileInfo> UpdateLocalTransactionHistoryEntry(ITransaction transaction, TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status, BlockId blockId, LockContext lockContext);

		Task InsertGenerationCacheEntry(IWalletGenerationCache entry, LockContext lockContext);
		Task UpdateGenerationCacheEntry(IWalletGenerationCache entry, LockContext lockContext);
		Task<IWalletGenerationCache> GetGenerationCacheEntry(string key, LockContext lockContext);
		Task<IWalletGenerationCache> GetGenerationCacheEntry<T>(T key, LockContext lockContext);
		Task<IWalletGenerationCache> GetGenerationCacheEntry(WalletGenerationCache.DispatchEventTypes type, string subtype, LockContext lockContext);
		Task<List<IWalletGenerationCache>> GetRetryEntriesBase(LockContext lockContext) ;
		Task DeleteGenerationCacheEntry(string key, LockContext lockContext);
		Task DeleteGenerationCacheEntry<T>(T key, LockContext lockContext);
		
		Task CreateElectionCacheWalletFile(IWalletAccount account, LockContext lockContext);
		Task DeleteElectionCacheWalletFile(IWalletAccount account, LockContext lockContext);
		Task InsertElectionCacheTransactions(List<TransactionId> transactionIds, long blockId, IWalletAccount account, LockContext lockContext);
		Task RemoveBlockElection(long blockId, IWalletAccount account, LockContext lockContext);
		Task RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactionIds, IWalletAccount account, LockContext lockContext);

		Task AddAccountKey<KEY>(string accountCode, KEY key, ImmutableDictionary<int, string> passphrases, LockContext lockContext, KEY nextKey = null)
			where KEY : class, IWalletKey;

		Task SetNextKey(string accountCode, IWalletKey nextKey, LockContext lockContext);
		Task SetNextKey(IWalletKey nextKey, LockContext lockContext);
		
		Task CreateNextXmssKey(string accountCode, string keyName, LockContext lockContext);
		Task CreateNextXmssKey(string accountCode, byte ordinal, LockContext lockContext);
		Task<bool> IsKeyEncrypted(string accountCode, LockContext lockContext);
		Task<bool> IsNextKeySet(string accountCode, string keyName, LockContext lockContext);

		Task<T> LoadNextKey<T>(string accountCode, string keyName, LockContext lockContext)
			where T : class, IWalletKey;

		Task<IWalletKey> LoadNextKey(string accountCode, string keyName, LockContext lockContext);
		Task<IWalletKey> LoadKey(string accountCode, string keyName, LockContext lockContext);
		Task<IWalletKey> LoadKey(string accountCode, byte ordinal, LockContext lockContext);
		Task<IWalletKey> LoadKey(string keyName, LockContext lockContext);
		Task<IWalletKey> LoadKey(byte ordinal, LockContext lockContext);

		Task<T> LoadKey<K, T>(Func<K, T> selector, string accountCode, string keyName, LockContext lockContext)
			where K : class, IWalletKey
			where T : class;

		Task<T> LoadKey<K, T>(Func<K, T> selector, string accountCode, byte ordinal, LockContext lockContext)
			where K : class, IWalletKey
			where T : class;

		Task<T> LoadKey<T>(Func<T, T> selector, string accountCode, string keyName, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(Func<T, T> selector, string accountCode, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(string accountCode, string keyName, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(string accountCode, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(string keyName, LockContext lockContext)
			where T : class, IWalletKey;

		Task<T> LoadKey<T>(byte ordinal, LockContext lockContext)
			where T : class, IWalletKey;

		Task UpdateKey(IWalletKey key, LockContext lockContext);
		Task SwapNextKey(IWalletKey key, LockContext lockContext, bool storeHistory = true);
		Task SwapNextKey(string accountCode, string keyName, LockContext lockContext, bool storeHistory = true);
		Task EnsureWalletLoaded(LockContext lockContext);
		Task SetExternalPassphraseHandlers(Delegates.RequestPassphraseDelegate requestPassphraseDelegate, Delegates.RequestKeyPassphraseDelegate requestKeyPassphraseDelegate, Delegates.RequestCopyKeyFileDelegate requestKeyCopyFileDelegate, Delegates.RequestCopyWalletFileDelegate copyWalletDelegate, LockContext lockContext);
		Task SetConsolePassphraseHandlers(LockContext lockContext);
		Task<(SecureString passphrase, bool keysToo)> RequestWalletPassphraseByConsole(LockContext lockContext, int maxTryCount = 10);
		Task<SecureString> RequestKeysPassphraseByConsole(string accountCode, string keyName, LockContext lockContext, int maxTryCount = 10);
		Task<(SecureString passphrase, bool keysToo)> RequestPassphraseByConsole(LockContext lockContext, string passphraseType = "wallet", int maxTryCount = 10);
		Task<SafeArrayHandle> PerformCryptographicSignature(string accountCode, string keyName, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> PerformCryptographicSignature(IWalletKey key, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssmtCryptographicSignature(IXmssMTWalletKey xmssMTWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssCryptographicSignature(IXmssWalletKey keyxmssWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null, SafeArrayHandle extraNodeCache = null, Action<XMSSProvider> callback = null, Func<int, int ,int, Task> progressCallback = null);
		Task<IWalletStandardAccountSnapshot> GetStandardAccountSnapshot(AccountId accountId, LockContext lockContext);
		Task<IWalletJointAccountSnapshot> GetJointAccountSnapshot(AccountId accountId, LockContext lockContext);
		Task<(string path, string passphrase, string salt, string nonce, int iterations)> BackupWallet(WalletProvider.BackupTypes backupType, LockContext lockContext);
		Task<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, string nonce, int iterations, LockContext lockContext);
		Task UpdateWalletChainStateSyncStatus(string accountCode, long BlockId, WalletAccountChainState.BlockSyncStatuses blockSyncStatus, LockContext lockContext);
		Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignTransactionXmss(SafeArrayHandle transactionHash, IXmssWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignMessageXmss(string accountCode, SafeArrayHandle message, LockContext lockContext);
		Task<SafeArrayHandle> SignMessageXmss(SafeArrayHandle messageHash, IXmssWalletKey key, LockContext lockContext);
		Task<SafeArrayHandle> SignMessage(SafeArrayHandle messageHash, IWalletKey key, LockContext lockContext);
		Task EnsureWalletKeyIsReady(string accountCode, string keyname, LockContext lockContext);
		Task EnsureWalletKeyIsReady(string accountCode, byte ordinal, LockContext lockContext);
		Task<bool> LoadWallet(CorrelationContext correlationContext, LockContext lockContext, string passphrase = null);

		Task Pause();
		Task Resume();
	}

	public interface IWalletProvider : IWalletProviderWrite, IReadonlyWalletProvider, IUtilityWalletProvider {

		public bool TransactionInProgress(LockContext lockContext);
	}

	public interface IWalletProviderInternal : IWalletProvider {

		public IUserWalletFileInfo WalletFileInfo { get; }
		public IWalletSerialisationFal SerialisationFal { get; }
		public Task<IUserWallet> WalletBase(LockContext lockContext);

		Task<K> PerformWalletTransaction<K>(Func<IWalletProvider, CancellationToken, LockContext, Task<K>> transactionAction, CancellationToken token, LockContext lockContext, Func<IWalletProvider, Func<IWalletProvider, CancellationToken, LockContext, Task>, CancellationToken, LockContext, Task> commitWrapper = null, Func<IWalletProvider, Func<IWalletProvider, CancellationToken, LockContext, Task>, CancellationToken, LockContext, Task> rollbackWrapper = null);
		Task WaitTransactionCompleted();
		Task RequestCopyWallet(CorrelationContext correlationContext, int attempt, LockContext lockContext);
		Task CaptureWalletPassphrase(CorrelationContext correlationContext, int attempt, LockContext lockContext);
		public void EnsureKeyFileIsPresent(string accountCode, string keyName, int attempt, LockContext lockContext);
		public bool IsKeyFileIsPresent(string accountCode, string keyName, int attempt, LockContext lockContext);
		public void EnsureKeyFileIsPresent(string accountCode, byte ordinal, int attempt, LockContext lockContext);
		public void ClearWalletPassphrase();
		Task RequestCopyKeyFile(CorrelationContext correlationContext, string accountCode, string keyName, int attempt, LockContext lockContext);
		Task CaptureKeyPassphrase(CorrelationContext correlationContext, string accountCode, string keyName, int attempt, LockContext lockContext);
		public void EnsureKeyPassphrase(string accountCode, string keyName, int attempt, LockContext lockContext);
		public void EnsureKeyPassphrase(string accountCode, byte ordinal, int attempt, LockContext lockContext);
		public bool IsKeyPassphraseValid(string accountCode, string keyName, int attempt, LockContext lockContext);
		public void SetKeysPassphrase(string accountCode, string keyname, string passphrase, LockContext lockContext);
		public void SetKeysPassphrase(string accountCode, string keyname, SecureString passphrase, LockContext lockContext);
		public void SetWalletPassphrase(string passphrase, LockContext lockContext);
		public void SetWalletPassphrase(SecureString passphrase, LockContext lockContext);
		public void ClearWalletKeyPassphrase(string accountCode, string keyName, LockContext lockContext);

		public Task EnsureWalletFileIsPresent(LockContext lockContext);
		Task EnsureWalletPassphrase(LockContext lockContext, string passphrase = null);
	}

	//TODO: split this class which is too big into multiple modules
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
		public const string DISTILLED_APPOINTMENT_CONTEXT_FILENAME = "AppointmentContext.json";

		/// <summary>
		/// minimum length of a passphrase
		/// </summary>
		public const int MINIMUM_KEY_PASSPHRASE_LENGTH = 6;

		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		protected CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;

		protected readonly string chainPath;

		protected readonly FileSystemWrapper fileSystem;

		protected readonly IGlobalsService globalsService;

		protected readonly RecursiveAsyncLock locker = new RecursiveAsyncLock();

		protected readonly BlockchainServiceSet serviceSet;

		/// <summary>
		///     the synthetized blocks to know which transactions concern us
		/// </summary>
		private readonly ConcurrentDictionary<long, SynthesizedBlock> syncBlockCache = new ConcurrentDictionary<long, SynthesizedBlock>();

		protected IWalletSerializationTransactionExtension currentTransaction;

		/// <summary>
		///     if paused, it is not safe to run wallet operations and transactions
		/// </summary>
		private bool paused;

		protected bool shutdownRequest;

		public WalletProvider(string chainPath, CENTRAL_COORDINATOR centralCoordinator) {
			this.chainPath = chainPath;
			this.centralCoordinator = centralCoordinator;

			this.globalsService = centralCoordinator.BlockchainServiceSet.GlobalsService;

			this.fileSystem = centralCoordinator.FileSystem;

			this.serviceSet = centralCoordinator.BlockchainServiceSet;
			centralCoordinator.ShutdownRequested += this.CentralCoordinatorOnShutdownRequested;
		}

		protected abstract ICardUtils CardUtils { get; }

		public Task<IUserWallet> WalletBase(LockContext lockContext) {
			return this.WalletFileInfo.WalletBase(lockContext);
		}

		public IWalletSerialisationFal SerialisationFal { get; private set; }

		public IUserWalletFileInfo WalletFileInfo { get; private set; }

		public bool IsWalletLoaded => this.WalletFileInfo?.IsLoaded ?? false;

		public Task<bool> IsWalletEncrypted(LockContext lockContext) {
			return Task.FromResult(this.WalletFileInfo.WalletSecurityDetails.EncryptWallet);
		}

		public async Task<bool> IsWalletAccountLoaded(LockContext lockContext) {
			return this.IsWalletLoaded && ((await this.WalletBase(lockContext).ConfigureAwait(false)).GetActiveAccount() != null);
		}

		public Task<bool> WalletFileExists(LockContext lockContext) {
			return Task.FromResult(this.WalletFileInfo.FileExists);
		}
		
		public Task<bool> WalletFullyCreated(LockContext lockContext) {
			return this.SerialisationFal.WalletFullyCreated(lockContext);
		}

		private Task ClearDamagedWallet(LockContext lockContext) {
			this.CentralCoordinator.Log.Warning("Clearing existing but corrupt (incomplete) wallet.");
			
			return this.SerialisationFal.ClearDamagedWallet(lockContext);
		}
		
		public bool TransactionInProgress(LockContext lockContext) {

			using(this.locker.Lock(lockContext)) {
				return this.currentTransaction != null;
			}
		}

		private bool isXmssBoosted = false;
		private Enums.ThreadMode XmssThreadMode {
			get {
				var mode = GlobalSettings.ApplicationSettings.XmssThreadMode;

				if(this.isXmssBoosted && (mode == Enums.ThreadMode.Half || mode == Enums.ThreadMode.ThreeQuarter)) {
					mode = (Enums.ThreadMode)(((int)mode)+1);
				}

				return mode;
			}
		}

		/// <summary>
		///     Wait for any transaction in progress to complete if any is underway. it will return ONLY when the transaction is
		///     completed safely
		/// </summary>
		public Task WaitTransactionCompleted() {
			LockContext innerLockContext = null;

			if(this.TransactionInProgress(innerLockContext)) {

				DateTime limit = DateTime.Now.AddSeconds(30);
				while(true) {
					if(!this.TransactionInProgress(innerLockContext)  || DateTime.Now > limit) {
						// we are ready to go
						break;
					}

					// we have to wait a little more
					Thread.Sleep(500);
				}
			}

			return Task.CompletedTask;
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

				using(LockHandle handleLocker = await this.locker.LockAsync(innerLockContext).ConfigureAwait(false)) {
					if(this.TransactionInProgress(handleLocker.Context)) {
						throw new NotReadyForProcessingException();
					}

					if(this.paused) {
						throw new NotReadyForProcessingException();
					}

					this.currentTransaction = await this.SerialisationFal.BeginTransaction().ConfigureAwait(false);
				}

				// let's make sure we catch implicit disposes that we did not call for through disposing
				this.currentTransaction.Disposed += async sessionId => {
					if((this.currentTransaction != null) && (this.currentTransaction.SessionId == sessionId)) {
						// ok, thats us, our session is now disposed.

						using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
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

					await this.currentTransaction.CommitTransaction().ConfigureAwait(false);
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
				using(await this.locker.LockAsync().ConfigureAwait(false)) {
					if(this.currentTransaction?.IsInTransaction??false) {
						string message = "A transaction could not be completed or rolled back. This is serious, leaving as is.";
						this.CentralCoordinator.Log.Fatal(message);
						throw new ApplicationException(message);
					}
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

			long? lowestAccountBlockSyncHeight = await this.LowestAccountBlockSyncHeight(lockContext).ConfigureAwait(false);

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

		protected string GetDistilledAppointmentContextPath() {
			return Path.Combine(SerialisationFal.GetWalletFolderPath(), DISTILLED_APPOINTMENT_CONTEXT_FILENAME);
		}
		public async Task<bool> WalletContainsAccount(string accountCode, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).Accounts.ContainsKey(accountCode);

		}

		public Task<IAccountFileInfo> GetAccountFileInfo(string accountCode, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return Task.FromResult(this.WalletFileInfo.Accounts[accountCode]);

		}

		/// <summary>
		///     gets the list of accounts that can be synced since they match the provided block id
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public async Task<List<IWalletAccount>> GetWalletSyncableAccounts(long blockId, long previousSyncedBlockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			List<string> keys = (await this.WalletFileInfo.Accounts.WhereAsync(async a => {

					                  WalletAccountChainState chainState = await a.Value.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

					                  return chainState;
				                  }, e => (e.LastBlockSynced == previousSyncedBlockId) || (e.LastBlockSynced == blockId && e.BlockSyncStatus != (int)WalletAccountChainState.BlockSyncStatuses.FullySynced)).ConfigureAwait(false)).Select(a => a.Key).ToList();

			return (await this.GetAccounts(lockContext).ConfigureAwait(false)).Where(a => keys.Contains(a.AccountCode)).ToList();

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

		public async Task<string> GetAccountCode(LockContext lockContext) {
			return (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).AccountCode;
		}

		public async Task<AccountId> GetPublicAccountId(LockContext lockContext) {
			IWalletAccount account = await this.GetActiveAccount(lockContext).ConfigureAwait(false);

			return await this.GetPublicAccountId(account.AccountCode, lockContext).ConfigureAwait(false);
		}

		public async Task<AccountId> GetPublicAccountId(string accountCode, LockContext lockContext) {

			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			if((account == null) || !await this.IsAccountPublished(account.AccountCode, lockContext).ConfigureAwait(false)) {
				return null;
			}

			return account.PublicAccountId;
		}

		public async Task<AccountId> GetInitiationId(LockContext lockContext) {
			return (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).PresentationId;
		}

		public async Task<bool> IsDefaultAccountPublished(LockContext lockContext) {
			return (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).Status == Enums.PublicationStatus.Published;
		}

		public async Task<bool> IsAccountPublished(string accountCode, LockContext lockContext) {
			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			return (account != null) && (account.Status == Enums.PublicationStatus.Published);
		}

		/// <summary>
		///     Return the base wallet directory, not scoped by chain
		/// </summary>
		/// <returns></returns>
		public string GetSystemFilesDirectoryPath() {

			return GlobalsService.GetGeneralSystemFilesDirectoryPath();
		}

		public async Task<bool> HasAccount(LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).HasAccount;

		}
		
		public async Task<IWalletAccount> GetActiveAccount(LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).GetActiveAccount();

		}
		
		public async Task<bool> SetActiveAccount(string accountCode, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).SetActiveAccount(accountCode);

		}

		public async Task<IWalletAccount> GetWalletAccount(string accountCode, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountCode);

		}

		public async Task<IWalletAccount> GetWalletAccountByName(string name, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccountByName(name);
		}

		public async Task<IWalletAccount> GetWalletAccount(AccountId accountId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			return (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountId);
		}

		public virtual Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, Enums.AccountTypes accountType, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null) {
			return this.CreateNewCompleteWallet(correlationContext, "", accountType, encryptWallet, encryptKey, encryptKeysIndividually, passphrases, lockContext, accountCreatedCallback);
		}

		public async Task<IWalletStandardAccountSnapshot> GetStandardAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return await this.GetAccountSnapshot(accountId, lockContext).ConfigureAwait(false) as IWalletStandardAccountSnapshot;
		}

		public async Task<IWalletJointAccountSnapshot> GetJointAccountSnapshot(AccountId accountId, LockContext lockContext) {
			return await this.GetAccountSnapshot(accountId, lockContext).ConfigureAwait(false) as IWalletJointAccountSnapshot;
		}

		public async Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount localAccount = await this.GetWalletAccount(accountSnapshot.AccountId.ToAccountId(), lockContext).ConfigureAwait(false);

			if(localAccount == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			await this.UpdateWalletSnapshot(accountSnapshot, localAccount.AccountCode, lockContext).ConfigureAwait(false);
		}

		public async Task UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, string accountCode, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			IAccountFileInfo walletAccountInfo = null;

			if(this.WalletFileInfo.Accounts.ContainsKey(accountCode)) {
				walletAccountInfo = this.WalletFileInfo.Accounts[accountCode];
			}

			IWalletAccountSnapshot snapshot = await (walletAccountInfo?.WalletSnapshotInfo.WalletAccountSnapshot(lockContext)).ConfigureAwait(false);

			if(snapshot == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			this.CardUtils.Copy(accountSnapshot, snapshot);

			walletAccountInfo?.WalletSnapshotInfo.SetWalletAccountSnapshot(snapshot);
		}

		public Task UpdateWalletSnapshotFromDigest(IAccountSnapshotDigestChannelCard accountCard, LockContext lockContext) {
			if(accountCard is IStandardAccountSnapshotDigestChannelCard standardAccountSnapshot) {
				return this.UpdateWalletSnapshotFromDigest(standardAccountSnapshot, lockContext);
			}

			if(accountCard is IJointAccountSnapshotDigestChannelCard jointAccountSnapshot) {
				return this.UpdateWalletSnapshotFromDigest(jointAccountSnapshot, lockContext);
			}

			throw new InvalidCastException();
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

			if(!this.WalletFileInfo.Accounts.ContainsKey(localAccount.AccountCode)) {
				await this.CreateNewWalletStandardAccountSnapshot(localAccount, lockContext).ConfigureAwait(false);
			}

			walletAccountInfo = this.WalletFileInfo.Accounts[localAccount.AccountCode];

			IWalletAccountSnapshot snapshot = await (walletAccountInfo?.WalletSnapshotInfo.WalletAccountSnapshot(lockContext)).ConfigureAwait(false);

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

			if(!this.WalletFileInfo.Accounts.ContainsKey(localAccount.AccountCode)) {
				await this.CreateNewWalletJointAccountSnapshot(localAccount, lockContext).ConfigureAwait(false);
			}

			walletAccountInfo = this.WalletFileInfo.Accounts[localAccount.AccountCode];

			IWalletAccountSnapshot snapshot = await (walletAccountInfo?.WalletSnapshotInfo.WalletAccountSnapshot(lockContext)).ConfigureAwait(false);

			if(snapshot == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			this.CardUtils.Copy(accountCard, snapshot);

			walletAccountInfo?.WalletSnapshotInfo.SetWalletAccountSnapshot(snapshot);
		}

		public async Task UpdateWalletChainStateSyncStatus(string accountCode, long BlockId, WalletAccountChainState.BlockSyncStatuses blockSyncStatus, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			WalletAccountChainState chainState = await this.WalletFileInfo.Accounts[accountCode].WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);
			chainState.LastBlockSynced = BlockId;
			chainState.BlockSyncStatus = (int) blockSyncStatus;
		}

		public virtual async Task<bool> CreateNewCompleteWallet(CorrelationContext correlationContext, string accountName, Enums.AccountTypes accountType, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, Action<IWalletAccount> accountCreatedCallback = null) {

			try {
				SystemEventGenerator.WalletCreationStepSet walletCreationStepSet = new SystemEventGenerator.WalletCreationStepSet();

				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletCreationStartedEvent(), correlationContext).ConfigureAwait(false);
				this.CentralCoordinator.Log.Information("Creating a new wallet");

				string walletPassphrase = null;

				if(passphrases?.ContainsKey(0) ?? false) {
					walletPassphrase = passphrases[0];
				}

				await this.CreateNewEmptyWallet(correlationContext, encryptWallet, walletPassphrase, walletCreationStepSet, lockContext).ConfigureAwait(false);

				await this.SerialisationFal.InstallWalletCreatingTag(lockContext).ConfigureAwait(false);

				await this.CreateNewCompleteStandardAccount(correlationContext, accountName, accountType, encryptKey, encryptKeysIndividually, passphrases, lockContext, walletCreationStepSet, accountCreatedCallback).ConfigureAwait(false);

				await this.SaveWallet(lockContext).ConfigureAwait(false);

				await this.SerialisationFal.RemoveWalletCreatingTag(lockContext).ConfigureAwait(false);
				
				this.CentralCoordinator.Log.Information("WalletBase successfully created and loaded");
				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletCreationEndedEvent(), correlationContext).ConfigureAwait(false);

				await this.centralCoordinator.RequestWalletSync().ConfigureAwait(false);

				// lets update the appointment operating mode now that we can
				await this.UpdateAppointmentOperatingMode(lockContext).ConfigureAwait(false);
				
				return true;
			} catch(Exception ex) {

				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletCreationErrorEvent("Failed to generate wallet", ex.ToString()), correlationContext).ConfigureAwait(false);

#if TESTNET
				try {
					// delete the folder
					if(Directory.Exists(this.WalletFileInfo.WalletPath)) {
						Directory.Delete(this.WalletFileInfo.WalletPath, true);
					}

				} catch(Exception ex2) {
					this.CentralCoordinator.Log.Error(ex2, "Failed to delete faulty wallet files.");
				}
#endif
				//Reset the WalletFileInfo
				await WalletFileInfo.Reset(lockContext).ConfigureAwait(false);

				throw new ApplicationException($"Failed to create wallet: {ex.Message}", ex);
			}
		}

		public virtual async Task<bool> CreateNewCompleteStandardAccount(CorrelationContext correlationContext, string accountName, Enums.AccountTypes accountType, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, Action<IWalletAccount> accountCreatedCallback = null) {

			try {
				if(encryptKeys && passphrases.Values.Any(p => string.IsNullOrWhiteSpace(p) || p.Length < MINIMUM_KEY_PASSPHRASE_LENGTH)) {
					throw new InvalidOperationException("Key passphrase is too small");
				}
				SystemEventGenerator.AccountCreationStepSet accountCreationStepSet = new SystemEventGenerator.AccountCreationStepSet();
				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.AccountCreationStartedEvent(), correlationContext).ConfigureAwait(false);
				await this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.AccountCreationStartedStep, correlationContext).ConfigureAwait(false);

				IWalletAccount account = await this.CreateNewStandardAccount(accountName, accountType, encryptKeys, encryptKeysIndividually, correlationContext, walletCreationStepSet, accountCreationStepSet, lockContext, true).ConfigureAwait(false);

				this.CentralCoordinator.Log.Information($"Your new default account Uuid is '{account.AccountCode}'");

				if(accountCreatedCallback != null) {
					accountCreatedCallback(account);
				}

				// now create the keys
				await this.CreateStandardAccountKeys(account.AccountCode, passphrases, correlationContext, walletCreationStepSet, accountCreationStepSet, lockContext).ConfigureAwait(false);

				await this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.AccountCreationEndedStep, correlationContext).ConfigureAwait(false);
				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.AccountCreationEndedEvent(account.AccountCode), correlationContext).ConfigureAwait(false);

				return true;
			} catch(Exception ex) {
				throw new ApplicationException("Failed to create account.", ex);
			}
		}

		public virtual Task<bool> CreateNewCompleteStandardAccount(CorrelationContext correlationContext, string accountName, Enums.AccountTypes accountType, bool encryptKeys, bool encryptKeysIndividually, ImmutableDictionary<int, string> passphrases, LockContext lockContext) {

			return this.CreateNewCompleteStandardAccount(correlationContext, accountName, accountType, encryptKeys, encryptKeysIndividually, passphrases, lockContext, null);
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
				if(await this.WalletFullyCreated(lockContext).ConfigureAwait(false)) {
					throw new ApplicationException("A wallet file already exists. we can not overwrite an existing file. delete it and try again");
				} else {
					// let's clear the damaged wallet
					await this.ClearDamagedWallet(lockContext).ConfigureAwait(false);
				}
			}

			await this.SerialisationFal.InstallWalletCreatingTag(lockContext).ConfigureAwait(false);
			
			this.WalletFileInfo.WalletSecurityDetails.EncryptWallet = encryptWallet;

			if(encryptWallet) {
				if(string.IsNullOrWhiteSpace(passphrase) || passphrase.Length < MINIMUM_KEY_PASSPHRASE_LENGTH) {
					throw new InvalidOperationException($"Passphrase must be {MINIMUM_KEY_PASSPHRASE_LENGTH} characters long");
				}

				this.SetWalletPassphrase(passphrase, lockContext);
			}

			IUserWallet wallet = this.CreateNewWalletEntry(lockContext);

			// set the wallet version

			wallet.Major = GlobalSettings.BlockchainCompatibilityVersion.Major;
			wallet.Minor = GlobalSettings.BlockchainCompatibilityVersion.Minor;
			wallet.Revision = GlobalSettings.BlockchainCompatibilityVersion.Revision;

			wallet.NetworkId = GlobalSettings.Instance.NetworkId;
			wallet.ChainId = this.centralCoordinator.ChainId.Value;

			await this.WalletFileInfo.CreateEmptyFileBase(wallet, lockContext).ConfigureAwait(false);
			
			await this.SerialisationFal.RemoveWalletCreatingTag(lockContext).ConfigureAwait(false);
			
			
			// lets update the appointment operating mode now that we can
			await this.UpdateAppointmentOperatingMode(lockContext).ConfigureAwait(false);
		}

		protected IWalletBackupProcessor CreateWalletBackupProcessor() {
			return new WalletBackupProcessor(this, this.CentralCoordinator.FileSystem,  this.SerialisationFal.GetWalletFolderPath());
		}

		
		
		public async Task<(string path, string passphrase, string salt, string nonce, int iterations)> BackupWallet(WalletProvider.BackupTypes backupType, LockContext lockContext) {

			// first, let's generate a passphrase

			await this.EnsureWalletFileIsPresent(lockContext).ConfigureAwait(false);

			var walletBackupProcessor = this.CreateWalletBackupProcessor();

			var backupInfo = await walletBackupProcessor.BackupWallet(backupType, lockContext).ConfigureAwait(false);
			
			return (backupInfo.Path, backupInfo.Passphrase, backupInfo.Salt, backupInfo.Nonce, backupInfo.Iterations);
		}

		public Task<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, string nonce, int iterations, LockContext lockContext) {
			
			var walletBackupProcessor = this.CreateWalletBackupProcessor();
			return walletBackupProcessor.RestoreWalletFromBackup(backupsPath, passphrase, salt, nonce, iterations, lockContext);
		}

		/// <summary>
		///     Load the wallet
		/// </summary>
		public async Task<bool> LoadWallet(CorrelationContext correlationContext, LockContext lockContext, string passphrase = null) {
			if(this.IsWalletLoaded) {
				this.CentralCoordinator.Log.Warning("Wallet already loaded");

				return false;
			}

			this.CentralCoordinator.Log.Warning("Ensuring PID protection");

			await this.EnsurePIDLock().ConfigureAwait(false);

			this.CentralCoordinator.Log.Information("Loading wallet");
			
			try {
				await this.EnsureWalletFileIsPresent(lockContext).ConfigureAwait(false);
				
				if(!await this.WalletFullyCreated(lockContext).ConfigureAwait(false)) {
					throw new ApplicationException("Failed to load wallet, wallet was not fully created and is corrupt");
				}
				
				await this.EnsureWalletPassphrase(lockContext, passphrase).ConfigureAwait(false);
				
				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletLoadingStartedEvent(), correlationContext).ConfigureAwait(false);

				await this.WalletFileInfo.LoadFileSecurityDetails(lockContext).ConfigureAwait(false);
				
				await this.WalletFileInfo.Load(lockContext).ConfigureAwait(false);

				IUserWallet walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);

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

					this.WalletFileInfo.Accounts.Add(account.AccountCode, await CreateNewAccountFileInfo(account, lockContext).ConfigureAwait(false));
				}

			} catch(FileNotFoundException e) {

				await this.WalletFileInfo.Reset(lockContext).ConfigureAwait(false);
				this.WalletFileInfo = this.SerialisationFal.CreateWalletFileInfo();

				this.CentralCoordinator.Log.Warning("Failed to load wallet, no wallet file exists");

				// for a missing file, we simply return false, so we can create it
				return false;
			} catch(Exception e) {

				await this.WalletFileInfo.Reset(lockContext).ConfigureAwait(false);
				this.WalletFileInfo = this.SerialisationFal.CreateWalletFileInfo();

				this.CentralCoordinator.Log.Error(e, "Failed to load wallet");

				await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletLoadingErrorEvent(), correlationContext).ConfigureAwait(false);

				throw;
			}

			this.CentralCoordinator.Log.Warning("Wallet successfully loaded");

			if(this.WalletIsLoaded != null) {
				await this.WalletIsLoaded().ConfigureAwait(false);
			}

			await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.WalletLoadingEndedEvent(), correlationContext).ConfigureAwait(false);

			await this.centralCoordinator.RequestWalletSync().ConfigureAwait(false);
			
			// lets update the appointment operationg mode now that we can
			await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.CheckOperatingMode(lockContext).ConfigureAwait(false);

			// lets update the appointment operating mode now that we can
			await this.UpdateAppointmentOperatingMode(lockContext).ConfigureAwait(false);
			
			return true;

		}

		private Task UpdateAppointmentOperatingMode(LockContext lockContext){
					
			// lets update the appointment operating mode now that we can
			return this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.CheckOperatingMode(lockContext);

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

				List<IWalletAccount> chaningAccounts = new List<IWalletAccount>();

				foreach(IWalletAccount account in (await this.WalletBase(lockContext).ConfigureAwait(false)).Accounts.Values) {

					AccountPassphraseDetails accountSecurityDetails = this.WalletFileInfo.Accounts[account.AccountCode].AccountSecurityDetails;
					bool keysEncryptionChange = accountSecurityDetails.EncryptWalletKeys != encryptKeys;

					if(keysEncryptionChange) {

						chaningAccounts.Add(account);

						// ensure key files are present
						foreach(KeyInfo keyInfo in account.Keys) {
							this.EnsureKeyFileIsPresent(account.AccountCode, keyInfo, 1, lockContext);
						}
					}
				}

				if(!walletEncryptionChange && !chaningAccounts.Any()) {
					this.CentralCoordinator.Log.Information("No encryption changes for the wallet. Nothing to do.");

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

					AccountPassphraseDetails accountSecurityDetails = this.WalletFileInfo.Accounts[account.AccountCode].AccountSecurityDetails;

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
				this.CentralCoordinator.Log.Verbose("error occured", e);

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
		public virtual async Task<IWalletAccount> CreateNewStandardAccount(string name, Enums.AccountTypes accountType, bool encryptKeys, bool encryptKeysIndividually, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, LockContext lockContext, bool setactive = false) {

			this.EnsureWalletIsLoaded();
			
			if(string.IsNullOrEmpty(name)) {
				name = UserWallet.DEFAULT_ACCOUNT;
			}
			
			IUserWallet walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);

			if(walletbase.GetAccountByName(name) != null) {
				throw new ApplicationException("Account with name already exists");
			}

			IWalletAccount account = this.CreateNewWalletAccountEntry(lockContext);

			await this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.CreatingFiles, correlationContext).ConfigureAwait(false);
			await this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingFiles, correlationContext).ConfigureAwait(false);

			account.InitializeNew(name, accountType, this.centralCoordinator.BlockchainServiceSet);

			account.KeysEncrypted = encryptKeys;
			account.KeysEncryptedIndividually = encryptKeysIndividually;

			if(this.WalletFileInfo.WalletSecurityDetails.EncryptWallet) {
				// generate encryption parameters
				account.InitializeNewEncryptionParameters(this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration);
			}

			// make it active

			if(setactive || (walletbase.Accounts.Count == 0)) {
				walletbase.ActiveAccount = account.AccountCode;
			}

			walletbase.Accounts.Add(account.AccountCode, account);

			// ensure the key holder is created
			IAccountFileInfo accountFileInfo = await this.CreateNewAccountFileInfo(account, lockContext).ConfigureAwait(false);

			// now create the file connection entry to map the new account
			this.WalletFileInfo.Accounts.Add(account.AccountCode, accountFileInfo);

			// and now create the keylog
			await accountFileInfo.WalletKeyLogsInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and now create the key history
			await accountFileInfo.WalletKeyHistoryInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and now create the chainState

			WalletAccountChainState chainState = this.CreateNewWalletAccountChainStateEntry(lockContext);
			chainState.AccountCode = account.AccountCode;

			// its a brand new account, there is nothing to sync until right now.
			chainState.LastBlockSynced = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;

			await accountFileInfo.WalletChainStatesInfo.CreateEmptyFile(chainState, lockContext).ConfigureAwait(false);

			await this.PrepareAccountInfos(accountFileInfo, lockContext).ConfigureAwait(false);

			await this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.SavingWallet, correlationContext).ConfigureAwait(false);

			return account;
		}

		public async Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, LockContext lockContext) {

			return await this.CreateNewWalletStandardAccountSnapshot(account, await this.CreateNewWalletStandardAccountSnapshotEntry(lockContext).ConfigureAwait(false), lockContext).ConfigureAwait(false);
		}

		public async Task<IWalletStandardAccountSnapshot> CreateNewWalletStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IUserWallet walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);

			if(!walletbase.Accounts.ContainsKey(account.AccountCode)) {
				//TODO: what to do here?
				throw new ApplicationException("Newly confirmed account is not in the wallet");
			}

			// lets fill the data from our wallet
			this.FillStandardAccountSnapshot(account, accountSnapshot);

			IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountCode];
			await accountFileInfo.WalletSnapshotInfo.InsertNewSnapshotBase(accountSnapshot, lockContext).ConfigureAwait(false);

			return accountSnapshot;
		}

		public async Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, LockContext lockContext) {

			return await this.CreateNewWalletJointAccountSnapshot(account, await this.CreateNewWalletJointAccountSnapshotEntry(lockContext).ConfigureAwait(false), lockContext).ConfigureAwait(false);
		}

		public async Task<IWalletJointAccountSnapshot> CreateNewWalletJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IUserWallet walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);

			if(!walletbase.Accounts.ContainsKey(account.AccountCode)) {
				//TODO: what to do here?
				throw new ApplicationException("Newly confirmed account is not in the wallet");
			}

			// lets fill the data from our wallet
			this.FillJointAccountSnapshot(account, accountSnapshot);

			IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountCode];
			await accountFileInfo.WalletSnapshotInfo.InsertNewJointSnapshotBase(accountSnapshot, lockContext).ConfigureAwait(false);

			return accountSnapshot;
		}

		public Task InsertKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, IdKeyUseIndexSet idKeyUseIndexSet, byte keyOrdinalId, LockContext lockContext) {
			return this.InsertKeyLogEntry(account, transactionId.ToString(), Enums.BlockchainEventTypes.Transaction, keyOrdinalId, idKeyUseIndexSet, lockContext);
		}

		public Task InsertKeyLogBlockEntry(IWalletAccount account, BlockId blockId, byte keyOrdinalId, IdKeyUseIndexSet keyUseIndex, LockContext lockContext) {
			return this.InsertKeyLogEntry(account, blockId.ToString(), Enums.BlockchainEventTypes.Block, keyOrdinalId, keyUseIndex, lockContext);
		}

		public Task InsertKeyLogDigestEntry(IWalletAccount account, int digestId, byte keyOrdinalId, IdKeyUseIndexSet keyUseIndex, LockContext lockContext) {
			return this.InsertKeyLogEntry(account, digestId.ToString(), Enums.BlockchainEventTypes.Digest, keyOrdinalId, keyUseIndex, lockContext);
		}

		public async Task InsertKeyLogEntry(IWalletAccount account, string eventId, Enums.BlockchainEventTypes eventType, byte keyOrdinalId, IdKeyUseIndexSet keyUseIndex, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletAccountKeyLog walletAccountKeyLog = this.CreateNewWalletAccountKeyLogEntry(lockContext);

				walletAccountKeyLog.Timestamp = DateTimeEx.CurrentTime;
				walletAccountKeyLog.EventId = eventId;
				walletAccountKeyLog.EventType = (byte) eventType;
				walletAccountKeyLog.KeyOrdinalId = keyOrdinalId;
				walletAccountKeyLog.KeyUseIndex = keyUseIndex;

				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletKeyLogsInfo;

				await keyLogFileInfo.InsertKeyLogEntry(walletAccountKeyLog, lockContext).ConfigureAwait(false);
			}
		}

		public async Task ConfirmKeyLogTransactionEntry(IWalletAccount account, TransactionId transactionId, IdKeyUseIndexSet idKeyUseIndexSet, long confirmationBlockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletKeyLogsInfo;
				await keyLogFileInfo.ConfirmKeyLogTransactionEntry(transactionId, idKeyUseIndexSet, confirmationBlockId, lockContext).ConfigureAwait(false);

			}
		}

		public async Task ConfirmKeyLogBlockEntry(IWalletAccount account, BlockId blockId, long confirmationBlockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletKeyLogsInfo;
				await keyLogFileInfo.ConfirmKeyLogBlockEntry(confirmationBlockId, lockContext).ConfigureAwait(false);

			}
		}

		public Task<bool> KeyLogTransactionExists(IWalletAccount account, TransactionId transactionId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletKeyLogsInfo;

				return keyLogFileInfo.KeyLogTransactionExists(transactionId, lockContext);

			}

			throw new ApplicationException("Keylog is not enabled.");
		}

		public IWalletKey CreateBasicKey(string name, CryptographicKeyType keyType) {
			IWalletKey key = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletKey(keyType);

			key.Id = Guid.NewGuid();
			key.Name = name;
			key.CreatedTime = DateTimeEx.CurrentTime.Ticks;

			return key;

		}

		public T CreateBasicKey<T>(string name, CryptographicKeyType keyType)
			where T : IWalletKey {

			T key = (T) this.CreateBasicKey(name, keyType);

			return key;
		}

		public virtual async Task GenerateXmssKeyIndexNodeCache(string accountCode, byte ordinal, long index, LockContext lockContext = null) {

			using(IXmssWalletKey key = await this.LoadKey<IXmssWalletKey>(k => {
				return k;
			}, accountCode, ordinal, lockContext).ConfigureAwait(false)) {

				if(key.Index.KeyUseIndex != index) {
					// key is out of date, so let's just forget it
					return;
				}

				this.CentralCoordinator.Log.Information($"generating xmss key index node cache for index {index} and tree height {key.TreeHeight}");
				using(XMSSProvider provider = new XMSSProvider(key.HashType, key.BackupHashType, key.TreeHeight, this.XmssThreadMode, key.NoncesExponent)) {

					provider.Initialize();

					key.NextKeyNodeCache.Entry = (await provider.GenerateIndexNodeCache(key.PrivateKey, (layer, height, pct) => {

                             if(pct > 0) {
	                             this.CentralCoordinator.Log.Verbose($"node cache generation progress: {pct}% - layer {layer} of {height}");
                             }

                             return Task.CompletedTask;
							                         
						}).ConfigureAwait(false)).Entry;
				}

				await this.UpdateKey(key, lockContext).ConfigureAwait(false);
				
				this.CentralCoordinator.Log.Verbose($"xmss key index node cache generation completed and key updated.");
			}
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

		public Task<bool> AllAccountsWalletKeyLogSet(SynthesizedBlock block, long previousSyncedBlockId, LockContext lockContext) {
			return this.AllAccountsHaveSyncStatus(block, previousSyncedBlockId, WalletAccountChainState.BlockSyncStatuses.KeyLogSynced, lockContext);
		}

		public Task<IWalletAccountSnapshot> GetWalletFileInfoAccountSnapshot(string accountCode, LockContext lockContext) {
			if(!this.WalletFileInfo.Accounts.ContainsKey(accountCode)) {
				return Task.FromResult((IWalletAccountSnapshot) null);
			}

			return this.WalletFileInfo.Accounts[accountCode].WalletSnapshotInfo.WalletAccountSnapshot(lockContext);
		}

		public async Task<bool> AllAccountsUpdatedWalletBlock(SynthesizedBlock block, long previousBlockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			// all accounts have been synced for previous block and if any at current, they have been set for this block
			return !(await this.GetWalletSyncableAccounts(block.BlockId, previousBlockId, lockContext).ConfigureAwait(false)).Any() && await this.AllAccountsHaveSyncStatus(block, previousBlockId, WalletAccountChainState.BlockSyncStatuses.BlockHeightUpdated, lockContext).ConfigureAwait(false);

		}

		public async Task<bool> AllAccountsHaveSyncStatus(SynthesizedBlock block, long previousSyncedBlockId, WalletAccountChainState.BlockSyncStatuses status, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			List<IWalletAccount> syncableAccounts = await this.GetWalletSyncableAccounts(block.BlockId, previousSyncedBlockId, lockContext).ConfigureAwait(false);

			if(!syncableAccounts.Any()) {
				return false;
			}

			foreach(IWalletAccount a in syncableAccounts) {
				WalletAccountChainState chainState = await this.WalletFileInfo.Accounts[a.AccountCode].WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

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

			this.CentralCoordinator.Log.Verbose($"updating wallet blocks for block {synthesizedBlock.BlockId}...");

			// this is where the wallet update happens...  any previous account that is fully synced can be upgraded to.
			List <IWalletAccount> availableAccounts = (await (await this.GetWalletSyncableAccounts(synthesizedBlock.BlockId, previousSyncedBlockId, lockContext).ConfigureAwait(false)).WhereAsync(async a => {

					                                         WalletAccountChainState chainState = await this.WalletFileInfo.Accounts[a.AccountCode].WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

					                                         return new {chainState.BlockSyncStatus, chainState.LastBlockSynced};
				                                         }, e => (e.BlockSyncStatus == (int) WalletAccountChainState.BlockSyncStatuses.FullySynced) || (e.LastBlockSynced == 0)).ConfigureAwait(false)).ToList();

			if (!availableAccounts.Any())
			{
				long? lowestAccountBlockSyncHeight = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LowestAccountBlockSyncHeight(lockContext).ConfigureAwait(false);
				if (lowestAccountBlockSyncHeight.HasValue && lowestAccountBlockSyncHeight.Value != previousSyncedBlockId)
					throw new WalletSyncException(synthesizedBlock.BlockId, "We have no syncable account and are trying to sync a block that would skip important block(s), this should not happen.");
			}
			

			Dictionary<string, WalletAccountChainState> chainStates = new Dictionary<string, WalletAccountChainState>();

			foreach(IWalletAccount account in availableAccounts) {

				AccountId publicAccountId = account.GetAccountId();
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountCode];
				WalletAccountChainState chainState = await accountFileInfo.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

				// we start the new workflow
				chainState.LastBlockSynced = synthesizedBlock.BlockId;
				chainState.BlockSyncStatus = (int) WalletAccountChainState.BlockSyncStatuses.Blank;

				chainStates.Add(account.AccountCode, chainState);

				if(synthesizedBlock.AccountScoped.ContainsKey(publicAccountId)) {
					// get the highest key use in the block for this account

					List<KeyValuePair<TransactionId, ITransaction>> transactionIds = synthesizedBlock.AccountScoped[publicAccountId].ConfirmedLocalTransactions.Where(t => t.Value.TransactionMeta.KeyUseIndex != null).ToList();

					foreach(IGrouping<byte, KeyValuePair<TransactionId, ITransaction>> group in transactionIds.GroupBy(t => t.Value.TransactionMeta.KeyUseIndex.Ordinal)) {
						IdKeyUseIndexSet highestIdKeyUse = null;

						if(group.Any()) {
							highestIdKeyUse = group.Max(t => t.Value.TransactionMeta.KeyUseIndex);
						}

						if(highestIdKeyUse != null) {
							await this.UpdateLocalChainStateTransactionKeyLatestSyncHeight(account.AccountCode, highestIdKeyUse, lockContext).ConfigureAwait(false);
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
				WalletAccountChainState chainState = chainStates[account.AccountCode];

				await this.SetChainStateHeight(chainState, synthesizedBlock.BlockId, lockContext).ConfigureAwait(false);

				// has been fully synced
				if(chainState.BlockSyncStatus != (int) WalletAccountChainState.BlockSyncStatuses.FullySynced) {
					throw new ApplicationException($"Wallet sync was incomplete for block id {synthesizedBlock.BlockId} and accountId {account.AccountCode}");
				}
			}
		}

		public async Task ChangeAccountsCorrelation(ImmutableList<AccountId> enableAccounts, ImmutableList<AccountId> disableAccounts, LockContext lockContext) {

			foreach(AccountId account in enableAccounts) {
				IWalletAccount walletAccount = await this.GetWalletAccount(account, lockContext).ConfigureAwait(false);

				if(walletAccount != null) {
					walletAccount.VerificationLevel = Enums.AccountVerificationTypes.KYC;
				}
			}

			foreach(AccountId account in disableAccounts) {
				IWalletAccount walletAccount = await this.GetWalletAccount(account, lockContext).ConfigureAwait(false);

				if(walletAccount != null) {
					walletAccount.VerificationLevel = Enums.AccountVerificationTypes.KYC;
				}
			}
		}
		
		public Task CacheSynthesizedBlock(SynthesizedBlock synthesizedBlock, LockContext lockContext) {
			if(!this.syncBlockCache.ContainsKey(synthesizedBlock.BlockId)) {
				this.syncBlockCache.AddSafe(synthesizedBlock.BlockId, synthesizedBlock);
			}

			return this.CleanSynthesizedBlockCache(lockContext);
		}

		public void ClearSynthesizedBlocksCache() {
			this.syncBlockCache.Clear();
		}
		
		public async Task CleanSynthesizedBlockCache(LockContext lockContext) {

			long? lowestBlockId = await this.LowestAccountBlockSyncHeight(lockContext).ConfigureAwait(false);

			foreach(long entry in this.syncBlockCache.Keys.ToArray().Where(b => b < (lowestBlockId - 3))) {
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

			IAccountFileInfo walletAccountInfo = this.WalletFileInfo.Accounts[localAccount.AccountCode];

			return await walletAccountInfo.WalletSnapshotInfo.WalletAccountSnapshot(lockContext).ConfigureAwait(false);
		}

		

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

		public event Func<Task> WalletIsLoaded;

		private void CentralCoordinatorOnShutdownRequested(ConcurrentBag<Task> beacons) {
			try {
				this.shutdownRequest = true;

				// ok, if this happens while we are syncing, we ask for a grace period until we are ready to clean exit
				beacons.Add(this.WaitTransactionCompleted());
			} catch {
				
			}
		}

		public string GetPIDFilePath() {
			return Path.Combine(this.GetChainDirectoryPath(), PID_LOCK_FILE);
		}

		protected virtual async Task EnsurePIDLock() {

			if((this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType == AppSettingsBase.SerializationTypes.Secondary) || GlobalSettings.ApplicationSettings.MobileMode) {
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

						if((process?.Id != 0) && !(process?.HasExited ?? true)) {
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

			byte[] bytes = new byte[sizeof(int)];
			TypeSerializer.Serialize(currentPid, in bytes);

			FileExtensions.WriteAllBytes(pidfile, SafeArrayHandle.WrapAndOwn(bytes), this.fileSystem);
		}

		protected virtual async Task PrepareAccountInfos(IAccountFileInfo accountFileInfo, LockContext lockContext) {

			// and the wallet snapshot
			await accountFileInfo.WalletSnapshotInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and the transaction cache
			await accountFileInfo.WalletGenerationCacheInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and the transaction history
			await accountFileInfo.WalletTransactionHistoryInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// and the elections history
			await accountFileInfo.WalletElectionsHistoryInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);
			
			// and the elections history
			await accountFileInfo.WalletElectionsStatisticsInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);
		}

		public async Task<bool> CreateStandardAccountKeys(string accountCode, ImmutableDictionary<int, string> passphrases, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, LockContext lockContext) {

			this.EnsureWalletIsLoaded();
			

			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			// create keys

			IXmssWalletKey mainKey = null;
			IXmssWalletKey messageKey = null;
			IXmssWalletKey changeKey = null;
			IXmssWalletKey superKey = null;

			IXmssWalletKey validatorSignatureKey = null;
			INTRUPrimeWalletKey validatorSecretKey = null;
				
			try {

				// get a bit more juice out of XMSS right now
				this.isXmssBoosted = true;

				// the keys are often heavy on the network, lets pause it
				this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PauseNetwork();

				await this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.CreatingAccountKeys, correlationContext).ConfigureAwait(false);
				await this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingTransactionKey, correlationContext).ConfigureAwait(false);

				int totalKeys = 4;

				if(account.WalletAccountType == Enums.AccountTypes.Server) {
					totalKeys = 6;
				}
				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.TRANSACTION_KEY_NAME, 1, totalKeys}, correlationContext).ConfigureAwait(false);


				mainKey = await this.CreateXmssKey(GlobalsService.TRANSACTION_KEY_NAME, percentage => {
					return this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.KeyGenerationPercentageEvent(GlobalsService.TRANSACTION_KEY_NAME, percentage), correlationContext);
					
				}).ConfigureAwait(false);

				GC.Collect();
				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.TRANSACTION_KEY_NAME, 1, totalKeys}, correlationContext).ConfigureAwait(false);

				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.MESSAGE_KEY_NAME, 2, totalKeys}, correlationContext).ConfigureAwait(false);

				await this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingMessageKey, correlationContext).ConfigureAwait(false);
				
				messageKey = await this.CreateXmssKey(GlobalsService.MESSAGE_KEY_NAME, percentage => {

					return this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.KeyGenerationPercentageEvent(GlobalsService.MESSAGE_KEY_NAME, percentage), correlationContext);
					
				}).ConfigureAwait(false);

				GC.Collect();

				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.MESSAGE_KEY_NAME, 2, totalKeys}, correlationContext).ConfigureAwait(false);

				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.CHANGE_KEY_NAME, 3, totalKeys}, correlationContext).ConfigureAwait(false);

				await this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingChangeKey, correlationContext).ConfigureAwait(false);
				
				changeKey = await this.CreateXmssKey(GlobalsService.CHANGE_KEY_NAME, percentage => {

					return this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.KeyGenerationPercentageEvent(GlobalsService.CHANGE_KEY_NAME, percentage), correlationContext);
					
				}).ConfigureAwait(false);

				GC.Collect();

				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.CHANGE_KEY_NAME, 3, totalKeys}, correlationContext).ConfigureAwait(false);

				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.SUPER_KEY_NAME, 4, totalKeys}, correlationContext).ConfigureAwait(false);

				await this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingSuperKey, correlationContext).ConfigureAwait(false);

				superKey = await this.CreateXmssKey(GlobalsService.SUPER_KEY_NAME, percentage => {

					return this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.KeyGenerationPercentageEvent(GlobalsService.SUPER_KEY_NAME, percentage), correlationContext);

				}).ConfigureAwait(false);

				await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.SUPER_KEY_NAME, 4, totalKeys}, correlationContext).ConfigureAwait(false);

				if(account.WalletAccountType == Enums.AccountTypes.Server) {

					GC.Collect();
					
					await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME, 5, totalKeys}, correlationContext).ConfigureAwait(false);

					await this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingValidatorSignatureKey, correlationContext).ConfigureAwait(false);

					validatorSignatureKey = await this.CreateXmssKey(GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME, percentage => {
						
						return this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.KeyGenerationPercentageEvent(GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME, percentage), correlationContext);

					}).ConfigureAwait(false);

					GC.Collect();
					
					await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME, 5, totalKeys}, correlationContext).ConfigureAwait(false);

					
					await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.VALIDATOR_SECRET_KEY_NAME, 6, totalKeys}, correlationContext).ConfigureAwait(false);

					await this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.CreatingValidatorSecretKey, correlationContext).ConfigureAwait(false);

					validatorSecretKey = await this.CreateValidatorSecretKey().ConfigureAwait(false);

					await this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.VALIDATOR_SECRET_KEY_NAME, 6, totalKeys}, correlationContext).ConfigureAwait(false);

				}

				await this.centralCoordinator.PostSystemEventImmediate(accountCreationStepSet?.KeysCreated, correlationContext).ConfigureAwait(false);
				await this.centralCoordinator.PostSystemEventImmediate(walletCreationStepSet?.AccountKeysCreated, correlationContext).ConfigureAwait(false);

				await Repeater.RepeatAsync(() => {
					return this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(async (provider, token, lc) => {

						await this.AddAccountKey(account.AccountCode, mainKey, passphrases, lc).ConfigureAwait(false);
						await this.AddAccountKey(account.AccountCode, messageKey, passphrases, lc).ConfigureAwait(false);
						await this.AddAccountKey(account.AccountCode, changeKey, passphrases, lc).ConfigureAwait(false);
						await this.AddAccountKey(account.AccountCode, superKey, passphrases, lc).ConfigureAwait(false);

						if(account.WalletAccountType == Enums.AccountTypes.Server) {
							await this.AddAccountKey(account.AccountCode, validatorSignatureKey, passphrases, lc).ConfigureAwait(false);
							await this.AddAccountKey(account.AccountCode, validatorSecretKey, passphrases, lc).ConfigureAwait(false);
						}
					}, lockContext);
				}).ConfigureAwait(false);

				// let's verify and confirm the keys are there
				using(IWalletKey key = await this.LoadKey(GlobalsService.TRANSACTION_KEY_NAME, lockContext).ConfigureAwait(false)) {
					if(key == null) {
						throw new ApplicationException($"Failed to generate and load key {GlobalsService.TRANSACTION_KEY_NAME}.");
					}
				}

				using(IWalletKey key = await this.LoadKey(GlobalsService.MESSAGE_KEY_NAME, lockContext).ConfigureAwait(false)) {
					if(key == null) {
						throw new ApplicationException($"Failed to generate and load key {GlobalsService.MESSAGE_KEY_NAME}.");
					}
				}

				using(IWalletKey key = await this.LoadKey(GlobalsService.CHANGE_KEY_NAME, lockContext).ConfigureAwait(false)) {
					if(key == null) {
						throw new ApplicationException($"Failed to generate and load key {GlobalsService.CHANGE_KEY_NAME}.");
					}
				}

				using(IWalletKey key = await this.LoadKey(GlobalsService.SUPER_KEY_NAME, lockContext).ConfigureAwait(false)) {
					if(key == null) {
						throw new ApplicationException($"Failed to generate and load key {GlobalsService.SUPER_KEY_NAME}.");
					}
				}

				GC.Collect();

			} catch(Exception ex) {
				throw new ApplicationException("Failed to generate wallet keys. this is serious and the wallet remains invalid.", ex);
			} finally {
				this.isXmssBoosted = false;
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
				
				// jsut in case we were not done, lets continue now...
				await this.centralCoordinator.RequestBlockchainSync().ConfigureAwait(false);
			}

			return true;
		}

		protected virtual void FillStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot) {

			accountSnapshot.AccountId = account.PublicAccountId.ToLongRepresentation();
			accountSnapshot.InceptionBlockId = account.ConfirmationBlockId;
			accountSnapshot.Correlated = account.VerificationLevel == Enums.AccountVerificationTypes.KYC;
		}

		protected virtual void FillJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot) {

			accountSnapshot.AccountId = account.PublicAccountId.ToLongRepresentation();
			accountSnapshot.InceptionBlockId = account.ConfirmationBlockId;
			accountSnapshot.Correlated = account.VerificationLevel == Enums.AccountVerificationTypes.KYC;
		}

		protected virtual async Task<IAccountFileInfo> CreateNewAccountFileInfo(IWalletAccount account, LockContext lockContext) {
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
			accountFileInfo.WalletGenerationCacheInfo = this.SerialisationFal.CreateWalletGenerationCacheFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			// and now create the transaction history
			accountFileInfo.WalletTransactionHistoryInfo = this.SerialisationFal.CreateWalletTransactionHistoryFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			// and now create the transaction history
			accountFileInfo.WalletElectionsHistoryInfo = this.SerialisationFal.CreateWalletElectionsHistoryFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			// and now create the transaction history
			accountFileInfo.WalletElectionsStatisticsInfo = this.SerialisationFal.CreateWalletElectionsStatisticsFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

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
			accountFileInfo.WalletKeysFileInfo.Add(GlobalsService.SUPER_KEY_NAME, this.SerialisationFal.CreateWalletKeysFileInfo<IXmssWalletKey>(account, GlobalsService.SUPER_KEY_NAME, GlobalsService.SUPER_KEY_ORDINAL_ID, this.WalletFileInfo.WalletSecurityDetails, accountFileInfo.AccountSecurityDetails));

			if(account.WalletAccountType == Enums.AccountTypes.Server) {
				accountFileInfo.WalletKeysFileInfo.Add(GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME, this.SerialisationFal.CreateWalletKeysFileInfo<IXmssWalletKey>(account, GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME, GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID, this.WalletFileInfo.WalletSecurityDetails, accountFileInfo.AccountSecurityDetails));
				accountFileInfo.WalletKeysFileInfo.Add(GlobalsService.VALIDATOR_SECRET_KEY_NAME, this.SerialisationFal.CreateWalletKeysFileInfo<INTRUPrimeWalletKey>(account, GlobalsService.VALIDATOR_SECRET_KEY_NAME, GlobalsService.VALIDATOR_SECRET_KEY_ORDINAL_ID, this.WalletFileInfo.WalletSecurityDetails, accountFileInfo.AccountSecurityDetails));
			}
		}

		/// <summary>
		///     Find the account and connection or a key in the wallet
		/// </summary>
		/// <param name="accountCode"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		private async Task<(KeyInfo keyInfo, IWalletAccount account)> GetKeyInfo(string accountCode, string name, LockContext lockContext) {

			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			KeyInfo keyInfo = account.Keys.SingleOrDefault(k => k.Name == name);

			return (keyInfo, account);
		}

		private async Task<(KeyInfo keyInfo, IWalletAccount account)> GetKeyInfo(string accountCode, byte ordinal, LockContext lockContext) {
			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			KeyInfo keyInfo = account.Keys.SingleOrDefault(k => k.Ordinal == ordinal);

			return (keyInfo, account);
		}

		protected virtual async Task<bool> UpdateWalletKeyLog(IAccountFileInfo accountFile, IWalletAccount account, SynthesizedBlock synthesizedBlock, LockContext lockContext) {
			bool changed = false;

			this.CentralCoordinator.Log.Verbose($"Update Wallet Key Logs for block {synthesizedBlock.BlockId} and accountId {account.AccountCode}...");

			WalletAccountChainState chainState = await accountFile.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);
			bool keyLogSynced = ((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.KeyLogSynced);

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog && !keyLogSynced) {
				AccountId accountId = account.GetAccountId();

				if(synthesizedBlock.AccountScoped.ContainsKey(accountId)) {
					SynthesizedBlock.SynthesizedBlockAccountSet scopedSynthesizedBlock = synthesizedBlock.AccountScoped[accountId];

					foreach(KeyValuePair<TransactionId, ITransaction> transactionId in scopedSynthesizedBlock.ConfirmedLocalTransactions) {

						ITransaction transaction = transactionId.Value;

						if(transaction.Version.Type == TransactionTypes.Instance.STANDARD_PRESENTATION) {
							// the presentation trnasaction is a special case, which we never sign with a ey in our wallet, so we just ignore it
							continue;
						}

						IdKeyUseIndexSet idKeyUseIndexSet = transaction.TransactionMeta.KeyUseIndex;

						if(transaction is IJointTransaction joinTransaction) {
							// ok, we need to check if we are not the main sender but still a cosinger

						}

						if(!await accountFile.WalletKeyLogsInfo.ConfirmKeyLogTransactionEntry(transaction.TransactionId, transaction.TransactionMeta.KeyUseIndex, synthesizedBlock.BlockId, lockContext).ConfigureAwait(false)) {
							// ok, this transction was not in our key log. this means we might have a bad wallet. this is very serious adn we alert the user
							//TODO: what to do with this?

							string message = $"Block {synthesizedBlock.BlockId} has our transaction {transaction.TransactionId} which belongs to us but is NOT in our keylog. We might have an old wallet.";
							throw new ReportableException(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_NOT_IN_KEYLOG, ReportableException.PriorityLevels.Warning, ReportableException.ReportLevels.Modal, this.centralCoordinator.ChainId, this.centralCoordinator.ChainName, message, new string[]{synthesizedBlock.BlockId.ToString(), transaction.TransactionId.ToString()});
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
			if(!await this.IsWalletEncrypted(lockContext).ConfigureAwait(false) && (this.CopyWalletRequest != null)) {
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
							foreach(IWalletAccount account in await this.GetAccounts(lockContext).ConfigureAwait(false)) {
								this.SetAllKeysPassphrase(account.AccountCode, copyPassphrase, lockContext);
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

		public Task EnsureWalletKeyIsReady(string accountCode, byte ordinal, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			if(!string.IsNullOrWhiteSpace(accountCode)) {
				string keyName = this.WalletFileInfo.Accounts[accountCode].WalletKeysFileInfo.Single(k => k.Value.OrdinalId == ordinal).Key;

				return this.EnsureWalletKeyIsReady(accountCode, keyName, lockContext);
			}

			return Task.CompletedTask;
		}

		public Task EnsureWalletKeyIsReady(string accountCode, KeyInfo keyInfo, LockContext lockContext) {
			return this.EnsureWalletKeyIsReady(accountCode, keyInfo.Name, lockContext);
		}

		public Task EnsureWalletKeyIsReady(string accountCode, string keyName, LockContext lockContext) {
			this.EnsureKeyFileIsPresent(accountCode, keyName, 1, lockContext);
			this.EnsureKeyPassphrase(accountCode, keyName, 1, lockContext);

			return Task.CompletedTask;

		}

		public virtual void EnsureKeyFileIsPresent(string accountCode, byte ordinal, int attempt, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			if(!string.IsNullOrWhiteSpace(accountCode)) {
				string keyName = this.WalletFileInfo.Accounts[accountCode].WalletKeysFileInfo.Single(k => k.Value.OrdinalId == ordinal).Key;

				this.EnsureKeyFileIsPresent(accountCode, keyName, attempt, lockContext);
			}
		}

		public virtual void EnsureKeyFileIsPresent(string accountCode, KeyInfo keyInfo, int attempt, LockContext lockContext) {
			this.EnsureKeyFileIsPresent(accountCode, keyInfo.Name, attempt, lockContext);
		}

		public virtual bool IsKeyFileIsPresent(string accountCode, string keyName, int attempt, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				walletKeyFileInfo.RefreshFile();

				// first, ensure the key is physically present
				return walletKeyFileInfo.FileExists;
			}

			return true;
		}

		public virtual void EnsureKeyFileIsPresent(string accountCode, string keyName, int attempt, LockContext lockContext) {

			// first, ensure the key is physically present
			if(!this.IsKeyFileIsPresent(accountCode, keyName, attempt, lockContext)) {

				throw new KeyFileMissingException(accountCode, keyName, attempt);
			}
		}

		public virtual void EnsureKeyPassphrase(string accountCode, byte ordinal, int attempt, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				this.EnsureWalletIsLoaded();
				string keyName = this.WalletFileInfo.Accounts[accountCode].WalletKeysFileInfo.Single(k => k.Value.OrdinalId == ordinal).Key;

				this.EnsureKeyPassphrase(accountCode, keyName, attempt, lockContext);
			}
		}

		public virtual void EnsureKeyPassphrase(string accountCode, KeyInfo keyInfo, int attempt, LockContext lockContext) {
			this.EnsureKeyPassphrase(accountCode, keyInfo.Name, attempt, lockContext);

		}

		public virtual bool IsKeyPassphraseValid(string accountCode, string keyName, int attempt, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				this.EnsureWalletIsLoaded();

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				// now the passphrase
				return !accountFileInfo.AccountSecurityDetails.EncryptWalletKeys || (accountFileInfo.AccountSecurityDetails.EncryptWalletKeys && accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountCode, keyName));
			}

			return true;
		}

		public virtual void EnsureKeyPassphrase(string accountCode, string keyName, int attempt, LockContext lockContext) {
			if(!this.IsKeyPassphraseValid(accountCode, keyName, attempt, lockContext)) {

				throw new KeyPassphraseMissingException(accountCode, keyName, attempt);
			}
		}

		public async Task RequestCopyKeyFile(CorrelationContext correlationContext, string accountCode, string keyName, int attempt, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				walletKeyFileInfo.RefreshFile();

				// first, ensure the key is physically present
				if(!walletKeyFileInfo.FileExists) {
					if(this.WalletCopyKeyFileRequest != null) {
						await this.WalletCopyKeyFileRequest(correlationContext, accountCode, keyName, attempt, lockContext).ConfigureAwait(false);
					}
				}
			}
		}

		public Task CaptureKeyPassphrase(CorrelationContext correlationContext, string accountCode, KeyInfo keyInfo, int attempt, LockContext lockContext) {
			return this.CaptureKeyPassphrase(correlationContext, accountCode, keyInfo.Name, attempt, lockContext);

		}

		public async Task CaptureKeyPassphrase(CorrelationContext correlationContext, string accountCode, string keyName, int attempt, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				this.EnsureWalletIsLoaded();

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				// now the passphrase
				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeys && !accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountCode, keyName)) {

					if(this.WalletKeyPassphraseRequest == null) {
						throw new ApplicationException("No key passphrase handling callback provided");
					}

					SecureString passphrase = await this.WalletKeyPassphraseRequest(correlationContext, accountCode, keyName, attempt, lockContext).ConfigureAwait(false);

					if(passphrase == null) {
						throw new InvalidOperationException("null passphrase provided. Invalid");
					}

					this.SetKeysPassphrase(accountCode, keyName, passphrase, lockContext);
				}
			}
		}

		/// <summary>
		///     Apply the same passphrase to all keys
		/// </summary>
		/// <param name="correlationContext"></param>
		/// <param name="accountCode"></param>
		/// <param name="attempt"></param>
		/// <param name="lockContext"></param>
		/// <param name="taskStasher"></param>
		/// <exception cref="ApplicationException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public async Task CaptureAllKeysPassphrase(CorrelationContext correlationContext, string accountCode, int attempt, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {

				if(this.WalletKeyPassphraseRequest == null) {
					throw new ApplicationException("The request keys passphrase callback can not be null");

				}

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];

				if(!accountFileInfo.AccountSecurityDetails.EncryptWalletKeys) {
					return;
				}

				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeysIndividually) {
					throw new ApplicationException("Keys are set to be encrypted individually, yet we are about to set them all with the same passphrase");
				}

				if(accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountCode)) {
					return;
				}

				SecureString passphrase = await this.WalletKeyPassphraseRequest(correlationContext, accountCode, "All Keys", attempt, lockContext).ConfigureAwait(false);

				if(this.WalletKeyPassphraseRequest == null) {
					throw new ApplicationException("No key passphrase handling callback provided");
				}

				if(passphrase == null) {
					throw new InvalidOperationException("null passphrase provided. Invalid");
				}

				this.SetAllKeysPassphrase(accountCode, passphrase, lockContext);
			}
		}

		public void SetAllKeysPassphrase(string accountCode, string passphrase, LockContext lockContext) {

			this.SetAllKeysPassphrase(accountCode, passphrase.ConvertToSecureString(), lockContext);
		}

		public void SetAllKeysPassphrase(string accountCode, SecureString passphrase, LockContext lockContext) {

			if(!string.IsNullOrWhiteSpace(accountCode)) {

				if(passphrase == null) {
					throw new InvalidOperationException("null passphrase provided. Invalid");
				}

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];

				if(!accountFileInfo.AccountSecurityDetails.EncryptWalletKeys) {
					return;
				}

				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeysIndividually) {
					throw new ApplicationException("Keys are set to be encrypted individually, yet we are about to set them all with the same passphrase");
				}

				if(accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountCode)) {
					return;
				}

				// set the default key for all keys
				this.SetKeysPassphrase(accountCode, passphrase, lockContext);
			}
		}

		public void SetKeysPassphrase(string accountCode, string passphrase, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];

				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeysIndividually) {
					throw new ApplicationException("Keys are set to be encrypted individually, yet we are about to set them all with the same passphrase");
				}

				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountCode, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void SetKeysPassphrase(string accountCode, SecureString passphrase, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];

				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeysIndividually) {
					throw new ApplicationException("Keys are set to be encrypted individually, yet we are about to set them all with the same passphrase");
				}

				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountCode, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void SetKeysPassphrase(string accountCode, string keyname, string passphrase, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];
				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountCode, keyname, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void SetKeysPassphrase(string accountCode, string keyname, SecureString passphrase, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];
				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountCode, keyname, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void ClearWalletKeyPassphrase(string accountCode, string keyName, LockContext lockContext) {
			if(!string.IsNullOrWhiteSpace(accountCode)) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountCode];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				accountFileInfo.AccountSecurityDetails.ClearKeysPassphrase();
			}
		}

	#endregion

	#region Chain State

		public async Task SetChainStateHeight(string accountCode, long blockId, LockContext lockContext) {
			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountCode);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			await this.SetChainStateHeight(chainState, blockId, lockContext).ConfigureAwait(false);
		}

		public async Task SetChainStateHeight(IWalletAccountChainState chainState, long blockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(blockId < chainState.LastBlockSynced) {
				throw new ApplicationException("The new chain state height can not be lower than the existing value");
			}

			if(!GlobalSettings.ApplicationSettings.SynclessMode && (blockId > (chainState.LastBlockSynced + 1))) {
				this.CentralCoordinator.Log.Warning($"The new chain state height ({blockId}) is higher than the next block id for current chain state height ({chainState.LastBlockSynced}).");
			}

			chainState.LastBlockSynced = blockId;
			chainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.BlockHeightUpdated;
		}

		public async Task<long> GetChainStateHeight(string accountCode, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountCode);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			return chainState.LastBlockSynced;
		}

		public async Task<WalletAccount.WalletAccountChainStateMiningCache> GetAccountMiningCache(AccountId accountId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountId);
			
			return account.MiningCache;
		}
		
		public async Task UpdateAccountMiningCache(AccountId accountId, WalletAccount.WalletAccountChainStateMiningCache miningCache, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			
			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountId);
			
			account.MiningCache = miningCache;
		}
		
		public async Task<IdKeyUseIndexSet> GetChainStateLastSyncedKeyHeight(IWalletKey key, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(key.AccountCode);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			IWalletAccountChainStateKey keyChainState = chainState.Keys[key.KeyAddress.OrdinalId];

			return keyChainState.LatestBlockSyncIdKeyUse;

		}

		public async Task UpdateLocalChainStateKeyHeight(IWalletKey key, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(key.AccountCode);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			IWalletAccountChainStateKey keyChainState = chainState.Keys[key.KeyAddress.OrdinalId];

			if(key.KeyAddress.KeyUseIndex.KeyUseSequenceId < keyChainState.LocalIdKeyUse?.KeyUseSequenceId) {
				throw new ApplicationException("The key sequence is lower than the one we have in the chain state");
			}

			if(key.KeyAddress.KeyUseIndex.KeyUseSequenceId < keyChainState.LatestBlockSyncIdKeyUse?.KeyUseSequenceId) {
				throw new ApplicationException("The key sequence is lower than the lasy synced block value");
			}

			if(key is IXmssWalletKey xmssWalletKey) {

				if(keyChainState.LocalIdKeyUse.IsSet && key.KeyAddress.KeyUseIndex.KeyUseIndexSet < keyChainState.LocalIdKeyUse) {
					throw new ApplicationException("The key sequence is lower than the one we have in the chain state");
				}

				if(keyChainState.LatestBlockSyncIdKeyUse.IsSet && key.KeyAddress.KeyUseIndex.KeyUseIndexSet < keyChainState.LatestBlockSyncIdKeyUse) {
					throw new ApplicationException("The key sequence is lower than the lasy synced block value");
				}

				keyChainState.LocalIdKeyUse.KeyUseIndex = key.KeyAddress.KeyUseIndex.KeyUseIndexSet.KeyUseIndex;
			}

			keyChainState.LocalIdKeyUse.KeyUseSequenceId = key.KeyAddress.KeyUseIndex.KeyUseIndexSet.KeyUseSequenceId;
		}

		/// <summary>
		///     update the key chain state with the highest key use we have found in the block.
		/// </summary>
		/// <param name="accountCode"></param>
		/// <param name="highestIdKeyUse"></param>
		/// <exception cref="ApplicationException"></exception>
		protected async Task UpdateLocalChainStateTransactionKeyLatestSyncHeight(string accountCode, IdKeyUseIndexSet highestIdKeyUse, LockContext lockContext) {

			if(!this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.UseKeyLog) {
				return;
			}

			this.EnsureWalletIsLoaded();
			IWalletAccount account = (await this.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountCode);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			IWalletAccountChainStateKey keyChainState = chainState.Keys[highestIdKeyUse.Ordinal];

			if(keyChainState.LatestBlockSyncIdKeyUse.IsSet && (highestIdKeyUse < keyChainState.LatestBlockSyncIdKeyUse)) {
				string message = "The last synced block transaction key sequence is lower than the value in our wallet. We may have a corrupt wallet and can not use it safely.";
				throw new ReportableException(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER, ReportableException.PriorityLevels.Warning, ReportableException.ReportLevels.Modal, this.centralCoordinator.ChainId, this.centralCoordinator.ChainName, message);
			}

			if(keyChainState.LocalIdKeyUse.IsSet && (highestIdKeyUse > keyChainState.LocalIdKeyUse)) {
				string message = "The last synced block transaction key sequence is higher than the value in our wallet. We may have an out of date wallet and can not use it safely.";
				throw new ReportableException(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_HIGHER, ReportableException.PriorityLevels.Warning, ReportableException.ReportLevels.Modal, this.centralCoordinator.ChainId, this.centralCoordinator.ChainName, message);
			}

			keyChainState.LatestBlockSyncIdKeyUse = highestIdKeyUse;

		}

	#endregion

	#region Elections History

		public virtual async Task<IWalletElectionsHistory> InsertElectionsHistoryEntry(SynthesizedBlock.SynthesizedElectionResult electionResult, SynthesizedBlock synthesizedBlock, AccountId electedAccountId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IUserWallet walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
			IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != default(AccountId)) && a.PublicAccountId.Equals(electedAccountId));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = walletbase.Accounts.Values.SingleOrDefault(a => (a.PresentationId != default(AccountId)) && a.PresentationId.Equals(electedAccountId));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletElectionsHistory walletElectionsHistory = this.CreateNewWalletElectionsHistoryEntry(lockContext);

			this.FillWalletElectionsHistoryEntry(walletElectionsHistory, electionResult, electedAccountId);

			IWalletElectionsHistoryFileInfo electionsHistoryInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletElectionsHistoryInfo;

			await electionsHistoryInfo.InsertElectionsHistoryEntry(walletElectionsHistory, lockContext).ConfigureAwait(false);

			return walletElectionsHistory;

		}

		protected virtual void FillWalletElectionsHistoryEntry(IWalletElectionsHistory walletElectionsHistory, SynthesizedBlock.SynthesizedElectionResult electionResult, AccountId electedAccountId) {

			walletElectionsHistory.BlockId = electionResult.BlockId;
			walletElectionsHistory.Timestamp = electionResult.Timestamp;
			walletElectionsHistory.DelegateAccount = electionResult.ElectedAccounts[electedAccountId].delegateAccountId?.ToString();
			walletElectionsHistory.MiningTier = electionResult.ElectedAccounts[electedAccountId].electedTier;
			walletElectionsHistory.SelectedTransactions = electionResult.ElectedAccounts[electedAccountId].selectedTransactions;
		}

	#endregion

	#region Transaction History

		protected async Task<(IWalletAccount sendingAccount, List<IWalletAccount> recipientAccounts)> GetImpactedLocalAccounts(ITransaction transaction, LockContext lockContext) {

			async Task<IWalletAccount> GetImpactedAccount(AccountId accountId) {

				IUserWallet walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
				IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != default(AccountId)) && a.PublicAccountId.Equals(accountId));

				return account ?? walletbase.Accounts.Values.SingleOrDefault(a => (a.PresentationId != default(AccountId)) && a.PresentationId.Equals(accountId));
			}

			IWalletAccount sendingAccount = await GetImpactedAccount(transaction.TransactionId.Account).ConfigureAwait(false);
			List<IWalletAccount> recipientAccounts = new List<IWalletAccount>();

			foreach(AccountId account in transaction.TargetAccounts) {
				IWalletAccount recipient = await GetImpactedAccount(account).ConfigureAwait(false);

				if(recipient != null) {
					recipientAccounts.Add(recipient);
				}
			}

			return (sendingAccount, recipientAccounts);
		}

		public virtual async Task InsertTransactionHistoryEntry(ITransaction transaction, bool own, string note, BlockId blockId,  WalletTransactionHistory.TransactionStatuses status, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			(IWalletAccount sendingAccount, List<IWalletAccount> recipientAccounts) = await this.GetImpactedLocalAccounts(transaction, lockContext).ConfigureAwait(false);

			List<(IWalletAccount account, bool sender)> accounts = new List<(IWalletAccount account, bool sender)>();

			// if it is our own, we dont include recipients, since they will get it when the trx will be confirmed in the block
			if(!own) {
				//transform them to get all our accounts, and know if it is the sender or recipient
				accounts = recipientAccounts.Where(a => a != null && a.GetAccountId() != sendingAccount?.GetAccountId()).Select(a => (a, false)).ToList();
			} else if(sendingAccount != null) {
				// ad it back manually to the list
				accounts.Add((sendingAccount, true));
			}

			foreach((IWalletAccount account, bool sender) in accounts) {

				IWalletTransactionHistory walletAccountTransactionHistory = this.CreateNewWalletAccountTransactionHistoryEntry(lockContext);

				walletAccountTransactionHistory.Status = status;
				walletAccountTransactionHistory.Local = sender;
				
				await this.FillWalletTransactionHistoryEntry(walletAccountTransactionHistory, transaction, account, sender, note, lockContext).ConfigureAwait(false);

				IWalletTransactionHistoryFileInfo transactionHistoryFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletTransactionHistoryInfo;

				await transactionHistoryFileInfo.InsertTransactionHistoryEntry(walletAccountTransactionHistory, lockContext).ConfigureAwait(false);

				await this.InsertedTransactionHistoryEntry(walletAccountTransactionHistory, account,transaction, blockId,lockContext).ConfigureAwait(false);
			}

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionHistoryUpdated(this.centralCoordinator.ChainId));
		}

		protected virtual Task InsertedTransactionHistoryEntry(IWalletTransactionHistory walletAccountTransactionHistory, IWalletAccount account, ITransaction transaction, BlockId blockId, LockContext lockContext) {
			
			return Task.CompletedTask;
		}
			
		protected virtual Task FillWalletTransactionHistoryEntry(IWalletTransactionHistory walletAccountTransactionHistory, ITransaction transaction, IWalletAccount account, bool local, string note, LockContext lockContext) {
			walletAccountTransactionHistory.TransactionId = transaction.TransactionId.ToString();
			walletAccountTransactionHistory.Version = transaction.Version.ToString();
			walletAccountTransactionHistory.Contents = JsonUtils.SerializeJsonSerializable(transaction);

			walletAccountTransactionHistory.Note = note;
			walletAccountTransactionHistory.Timestamp = this.serviceSet.BlockchainTimeService.GetTransactionDateTime(transaction.TransactionId, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
			walletAccountTransactionHistory.Recipient = transaction.TargetAccountsSerialized;

			if(!local) {
				// incoming transactions are always confirmed
				walletAccountTransactionHistory.Status = WalletTransactionHistory.TransactionStatuses.Confirmed;
			}

			return Task.CompletedTask;
		}

		public virtual async Task<IWalletTransactionHistoryFileInfo> UpdateLocalTransactionHistoryEntry(ITransaction transaction, TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status, BlockId blockId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IUserWallet walletbase = await this.WalletFileInfo.WalletBase(lockContext).ConfigureAwait(false);
			IWalletAccount account = walletbase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != default(AccountId)) && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = walletbase.Accounts.Values.SingleOrDefault(a => (a.PresentationId != default(AccountId)) && a.PresentationId.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionHistoryFileInfo transactionHistoryFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletTransactionHistoryInfo;

			await transactionHistoryFileInfo.UpdateTransactionStatus(transactionId, status, lockContext).ConfigureAwait(false);

			await this.UpdateLocalTransactionHistoryEntry(transactionHistoryFileInfo, transaction, transactionId, status, lockContext).ConfigureAwait(false);
			
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionHistoryUpdated(this.centralCoordinator.ChainId));

			return transactionHistoryFileInfo;

		}

		protected virtual Task UpdateLocalTransactionHistoryEntry(IWalletTransactionHistoryFileInfo transactionHistoryFileInfo, ITransaction transaction, TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status, LockContext lockContext) {
			return Task.CompletedTask;
		}

	#endregion

	#region Transaction Cache

		public virtual async Task InsertGenerationCacheEntry(IWalletGenerationCache entry, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = await this.GetActiveAccount(lockContext).ConfigureAwait(false);

			if(account == null) {
				throw new ApplicationException("No account found for transaction");
			}

			IWalletGenerationCacheFileInfo generationCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletGenerationCacheInfo;

			await generationCacheFileInfo.InsertCacheEntry(entry, lockContext).ConfigureAwait(false);

		}

		public Task<IWalletGenerationCache> GetGenerationCacheEntry<T>(T key, LockContext lockContext) {
			return this.GetGenerationCacheEntry(key.ToString(), lockContext);
		}
		
		public virtual async Task<IWalletGenerationCache> GetGenerationCacheEntry(string key, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = await this.GetActiveAccount(lockContext).ConfigureAwait(false);

			return await this.WalletFileInfo.Accounts[account.AccountCode].WalletGenerationCacheInfo.GetEntryBase(key, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<IWalletGenerationCache> GetGenerationCacheEntry(WalletGenerationCache.DispatchEventTypes type, string subtype, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = await this.GetActiveAccount(lockContext).ConfigureAwait(false);

			return await this.WalletFileInfo.Accounts[account.AccountCode].WalletGenerationCacheInfo.GetEntryBase(type, subtype, lockContext).ConfigureAwait(false);
		}
		
		public virtual async Task<List<IWalletGenerationCache>> GetRetryEntriesBase(LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = await this.GetActiveAccount(lockContext).ConfigureAwait(false);

			return await this.WalletFileInfo.Accounts[account.AccountCode].WalletGenerationCacheInfo.GetRetryEntriesBase(lockContext).ConfigureAwait(false);
		}

		public virtual async Task UpdateGenerationCacheEntry(IWalletGenerationCache entry, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = await this.GetActiveAccount(lockContext).ConfigureAwait(false);

			IWalletGenerationCacheFileInfo generationCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletGenerationCacheInfo;

			await generationCacheFileInfo.UpdateEntryBase(entry, lockContext).ConfigureAwait(false);
		}

		public Task DeleteGenerationCacheEntry<T>(T key, LockContext lockContext) {
			return this.DeleteGenerationCacheEntry(key.ToString(), lockContext);
		}
		
		public virtual async Task DeleteGenerationCacheEntry(string key, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = await this.GetActiveAccount(lockContext).ConfigureAwait(false);

			IWalletGenerationCacheFileInfo generationCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletGenerationCacheInfo;

			await generationCacheFileInfo.RemoveEntry(key, lockContext).ConfigureAwait(false);

		}

	#endregion

	#region Election Cache

		public async Task CreateElectionCacheWalletFile(IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			await this.DeleteElectionCacheWalletFile(account, lockContext).ConfigureAwait(false);

			IAccountFileInfo walletFileInfoAccount = this.WalletFileInfo.Accounts[account.AccountCode];

			walletFileInfoAccount.WalletElectionCacheInfo = this.SerialisationFal.CreateWalletElectionCacheFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			await walletFileInfoAccount.WalletElectionCacheInfo.CreateEmptyFile(lockContext).ConfigureAwait(false);

		}

		public Task DeleteElectionCacheWalletFile(IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			IAccountFileInfo walletFileInfoAccount = this.WalletFileInfo.Accounts[account.AccountCode];

			walletFileInfoAccount.WalletElectionCacheInfo?.DeleteFile();

			walletFileInfoAccount.WalletElectionCacheInfo = null;

			return Task.CompletedTask;
		}

		public async Task<bool> SetSMSConfirmationCode(string accountCode, long confirmationCode, LockContext lockContext) {
			
			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);
			if(account != null) {

				if(account.SMSDetails == null) {
					account.SMSDetails = new WalletAccount.AccountSMSDetails();
				}

				account.SMSDetails.ConfirmationCode = confirmationCode;
				account.SMSDetails.ConfirmationCodeExpiration = DateTimeEx.CurrentTime.AddDays(2);

				return true;
			}

			return false;
		}

		public Task<List<TransactionId>> GetElectionCacheTransactions(IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletElectionCacheInfo;

			if(electionCacheFileInfo == null) {
				return Task.FromResult(new List<TransactionId>());
			}
			return electionCacheFileInfo.GetAllTransactions(lockContext);

		}

		public Task InsertElectionCacheTransactions(List<TransactionId> transactionIds, long blockId, IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			List<WalletElectionCache> entries = new List<WalletElectionCache>();

			foreach(TransactionId transactionId in transactionIds) {

				WalletElectionCache entry = this.CreateNewWalletAccountElectionCacheEntry(lockContext);
				entry.TransactionId = transactionId.ToString();
				entry.BlockId = blockId;

				entries.Add(entry);
			}

			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletElectionCacheInfo;
			if(electionCacheFileInfo == null) {
				return Task.CompletedTask;
			}
			return electionCacheFileInfo.InsertElectionCacheEntries(entries, lockContext);

		}

		public Task RemoveBlockElection(long blockId, IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletElectionCacheInfo;

			if(electionCacheFileInfo == null) {
				return Task.CompletedTask;
			}
			return electionCacheFileInfo.RemoveBlockElection(blockId, lockContext);

		}

		public Task RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactionIds, IWalletAccount account, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletElectionCacheInfo;

			if(electionCacheFileInfo == null) {
				return Task.CompletedTask;
			}
			return electionCacheFileInfo.RemoveBlockElectionTransactions(blockId, transactionIds, lockContext);
		}

	#endregion

	#region Keys

		/// <summary>
		///     here we add a new key to the account
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public async Task AddAccountKey<KEY>(string accountCode, KEY key, ImmutableDictionary<int, string> passphrases, LockContext lockContext, KEY nextKey = null)
			where KEY : class, IWalletKey {
			this.EnsureWalletIsLoaded();

			key.AccountCode = accountCode;

			if(nextKey != null) {
				nextKey.AccountCode = accountCode;
			}

			(KeyInfo keyInfo1, IWalletAccount account) = await this.GetKeyInfo(accountCode, key.Name, lockContext).ConfigureAwait(false);

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
			key.KeyAddress.KeyUseIndex.KeyUseSequenceId = 0;

			if(nextKey != null) {
				nextKey.KeyAddress.OrdinalId = ordinal;
				nextKey.KeyAddress.KeyUseIndex.KeyUseSequenceId = key.KeyAddress.KeyUseIndex.KeyUseSequenceId;
				nextKey.KeyAddress.KeyUseIndex.IncrementSequence();
			}

			// we add this new key
			account.Keys.Add(keyInfo);

			IAccountFileInfo walletAccountFileInfo = this.WalletFileInfo.Accounts[accountCode];

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
					this.SetKeysPassphrase(accountCode, keyInfo.Name, passphrase, lockContext);
				}

				this.EnsureKeyPassphrase(accountCode, keyInfo.Name, 1, lockContext);
			}

			// ensure we create the key file
			await walletAccountFileInfo.WalletKeysFileInfo[key.Name].Reset(lockContext).ConfigureAwait(false);
			walletAccountFileInfo.WalletKeysFileInfo[key.Name].DeleteFile();
			await walletAccountFileInfo.WalletKeysFileInfo[key.Name].CreateEmptyFile(key, nextKey, lockContext).ConfigureAwait(false);

			// add the key chainstate
			IWalletAccountChainStateKey chainStateKey = this.CreateNewWalletAccountChainStateKeyEntry(lockContext);
			chainStateKey.Ordinal = key.KeyAddress.OrdinalId;

			IdKeyUseIndexSet keyUseIndex = new IdKeyUseIndexSet(key.KeyAddress.KeyUseIndex.KeyUseSequenceId, -1, chainStateKey.Ordinal);

			if(key is XmssWalletKey xmssWalletKey) {
				keyUseIndex.KeyUseIndex = xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex;
			}

			chainStateKey.LocalIdKeyUse = keyUseIndex;
			chainStateKey.LatestBlockSyncIdKeyUse = new IdKeyUseIndexSet(0, 0, chainStateKey.Ordinal);

			WalletAccountChainState chainState = await walletAccountFileInfo.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

			if(chainState.Keys.ContainsKey(chainStateKey.Ordinal)) {
				chainState.Keys.Remove(chainStateKey.Ordinal);
			}

			chainState.Keys.Add(chainStateKey.Ordinal, chainStateKey);
		}

		/// <summary>
		///     this method can be called to create and set the next XMSS key. This can be useful to pre create large keys as the
		///     next key, save some time at key change time.
		/// </summary>
		/// <param name="accountCode"></param>
		/// <param name="keyName"></param>
		public async Task CreateNextXmssKey(string accountCode, string keyName, LockContext lockContext) {
			this.EnsureWalletIsLoaded();
			await this.EnsureWalletKeyIsReady(accountCode, keyName, lockContext).ConfigureAwait(false);
			
			using IXmssWalletKey nextKey = await this.CreateXmssKey(keyName).ConfigureAwait(false);

			await this.SetNextKey(accountCode, nextKey, lockContext).ConfigureAwait(false);
		}

		public async Task CreateNextXmssKey(string accountCode, byte ordinal, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(accountCode, ordinal, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(accountCode, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			await this.CreateNextXmssKey(accountCode, keyMeta.keyInfo.Name, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<bool> IsKeyEncrypted(string accountCode, LockContext lockContext) {

			return (await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false)).KeysEncrypted;
		}

		/// <summary>
		///     determine if the next key has already been created and set
		/// </summary>
		/// <param name="taskStasher"></param>
		/// <param name="accountCode"></param>
		/// <param name="ordinal"></param>
		/// <returns></returns>
		public virtual async Task<bool> IsNextKeySet(string accountCode, string keyName, LockContext lockContext) {

			this.EnsureWalletIsLoaded();
			await this.EnsureWalletKeyIsReady(accountCode, keyName, lockContext).ConfigureAwait(false);

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(accountCode, keyName, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			Repeater.Repeat(index => {
				// ensure the key files are present
				this.EnsureKeyFileIsPresent(accountCode, keyMeta.keyInfo, index, lockContext);
			});

			bool isNextKeySet = false;

			await this.EnsureWalletKeyIsReady(accountCode, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountCode].WalletKeysFileInfo[keyName];

			isNextKeySet = await walletKeyInfo.IsNextKeySet(lockContext).ConfigureAwait(false);

			return isNextKeySet;

		}

		public virtual Task SetNextKey(IWalletKey nextKey, LockContext lockContext) {
			if(string.IsNullOrWhiteSpace(nextKey.AccountCode)) {
				throw new ApplicationException("Key account code is not set");
			}

			return SetNextKey(nextKey.AccountCode, nextKey, lockContext);
		}

		public virtual async Task SetNextKey(string accountCode, IWalletKey nextKey, LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			nextKey.AccountCode = accountCode;

			(KeyInfo keyInfo, _) = await this.GetKeyInfo(accountCode, nextKey.Name, lockContext).ConfigureAwait(false);

			if(keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to set");
			}

			await this.EnsureWalletKeyIsReady(accountCode, keyInfo, lockContext).ConfigureAwait(false);
			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountCode].WalletKeysFileInfo[nextKey.Name];

			await walletKeyInfo.SetNextKey(nextKey, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<IWalletKey> LoadKey(string keyName, LockContext lockContext) {
			return await this.LoadKey<IWalletKey>(await this.GetAccountCode(lockContext).ConfigureAwait(false), keyName, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<IWalletKey> LoadKey(byte ordinal, LockContext lockContext) {
			return await this.LoadKey<IWalletKey>(await this.GetAccountCode(lockContext).ConfigureAwait(false), ordinal, lockContext).ConfigureAwait(false);
		}

		public virtual Task<IWalletKey> LoadKey(string accountCode, string keyName, LockContext lockContext) {
			return this.LoadKey<IWalletKey>(accountCode, keyName, lockContext);
		}

		public virtual Task<IWalletKey> LoadKey(string accountCode, byte ordinal, LockContext lockContext) {
			return this.LoadKey<IWalletKey>(accountCode, ordinal, lockContext);
		}

		public virtual async Task<T> LoadKey<T>(string keyName, LockContext lockContext)
			where T : class, IWalletKey {

			return await this.LoadKey<T>(await this.GetAccountCode(lockContext).ConfigureAwait(false), keyName, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<T> LoadKey<T>(byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {

			return await this.LoadKey<T>(await this.GetAccountCode(lockContext).ConfigureAwait(false), ordinal, lockContext).ConfigureAwait(false);
		}

		public virtual Task<T> LoadKey<T>(string accountCode, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			T Selector(T key) {
				return key;
			}

			return this.LoadKey<T>(Selector, accountCode, keyName, lockContext);

		}

		public virtual Task<T> LoadKey<T>(string accountCode, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {
			T Selector(T key) {
				return key;
			}

			return this.LoadKey<T>(Selector, accountCode, ordinal, lockContext);

		}

		public virtual Task<T> LoadKey<T>(Func<T, T> selector, string accountCode, string name, LockContext lockContext)
			where T : class, IWalletKey {
			return this.LoadKey<T, T>(selector, accountCode, name, lockContext);
		}

		public virtual Task<T> LoadKey<T>(Func<T, T> selector, string accountCode, byte ordinal, LockContext lockContext)
			where T : class, IWalletKey {

			return this.LoadKey<T, T>(selector, accountCode, ordinal, lockContext);
		}

		/// <summary>
		///     Load a key with a custom selector
		/// </summary>
		/// <param name="selector"></param>
		/// <param name="accountCode"></param>
		/// <param name="name"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<T> LoadKey<K, T>(Func<K, T> selector, string accountCode, string name, LockContext lockContext)
			where T : class
			where K : class, IWalletKey {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, _) = await this.GetKeyInfo(accountCode, name, lockContext).ConfigureAwait(false);

			if(keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			// ensure the key files are present
			await this.EnsureWalletKeyIsReady(accountCode, keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountCode].WalletKeysFileInfo[keyInfo.Name];

			return await walletKeyInfo.LoadKey(selector, accountCode, keyInfo.Name, lockContext).ConfigureAwait(false);
		}

		public virtual async Task<T> LoadKey<K, T>(Func<K, T> selector, string accountCode, byte ordinal, LockContext lockContext)
			where T : class
			where K : class, IWalletKey {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(accountCode, ordinal, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(accountCode, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountCode].WalletKeysFileInfo[keyMeta.keyInfo.Name];

			return await walletKeyInfo.LoadKey(selector, accountCode, keyMeta.keyInfo.Name, lockContext).ConfigureAwait(false);
		}

		public virtual Task<IWalletKey> LoadNextKey(string accountCode, string keyName, LockContext lockContext) {
			return this.LoadNextKey<IWalletKey>(accountCode, keyName, lockContext);
		}

		public virtual async Task<T> LoadNextKey<T>(string accountCode, string keyName, LockContext lockContext)
			where T : class, IWalletKey {
			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(accountCode, keyName, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(accountCode, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountCode].WalletKeysFileInfo[keyMeta.keyInfo.Name];

			T key = await walletKeyInfo.LoadNextKey<T>(accountCode, keyMeta.keyInfo.Name, lockContext).ConfigureAwait(false);

			// this might not have been set
			key.AccountCode = accountCode;

			return key;
		}

		public virtual async Task UpdateKey(IWalletKey key, LockContext lockContext) {

			if((key.PrivateKey == null) || (key.PrivateKey.Length == 0)) {
				throw new ApplicationException("Private key is not set");
			}

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(key.AccountCode, key.Name, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(key.AccountCode, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[key.AccountCode].WalletKeysFileInfo[key.Name];

			await walletKeyInfo.UpdateKey(key, lockContext).ConfigureAwait(false);
		}

		/// <summary>
		///     Swap the next key with the current key. the old key is placed in the key history for archiving
		/// </summary>
		/// <param name="key"></param>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task SwapNextKey(IWalletKey key, LockContext lockContext, bool storeHistory = true) {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = await this.GetKeyInfo(key.AccountCode, key.Name, lockContext).ConfigureAwait(false);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			await this.EnsureWalletKeyIsReady(key.AccountCode, keyMeta.keyInfo, lockContext).ConfigureAwait(false);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[key.AccountCode].WalletKeysFileInfo[key.Name];

			if(!await walletKeyInfo.IsNextKeySet(lockContext).ConfigureAwait(false)) {
				throw new ApplicationException("Next private key is not set");
			}

			if(storeHistory) {
				WalletKeyHistoryFileInfo walletKeyHistoryInfo = this.WalletFileInfo.Accounts[key.AccountCode].WalletKeyHistoryInfo;

				await walletKeyHistoryInfo.InsertKeyHistoryEntry(key, this.CreateNewWalletKeyHistoryEntry(lockContext), lockContext).ConfigureAwait(false);
			}

			await walletKeyInfo.SwapNextKey(keyMeta.keyInfo, key.AccountCode, lockContext).ConfigureAwait(false);

			using IWalletKey newKey = await walletKeyInfo.LoadKey<IWalletKey>(key.AccountCode, key.Name, lockContext).ConfigureAwait(false);

			// we swapped our key, we must update the chain state
			await this.UpdateLocalChainStateKeyHeight(newKey, lockContext).ConfigureAwait(false);

		}

		public virtual async Task SwapNextKey(string accountCode, string keyName, LockContext lockContext, bool storeHistory = true) {

			using IWalletKey key = await this.LoadKey(accountCode, keyName, lockContext).ConfigureAwait(false);

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

			this.WalletKeyPassphraseRequest += (correlationContext, accountCode, keyName, attempt, lc) => this.RequestKeysPassphraseByConsole(accountCode, keyName, lc);

			this.WalletCopyKeyFileRequest += (correlationContext, accountCode, keyName, attempt, lc) => this.RequestKeysCopyFileByConsole(accountCode, keyName, lc);

			this.CopyWalletRequest += (correlationContext, attempt, lc) => this.RequestCopyWalletByConsole(lc);

			return Task.CompletedTask;
		}

		public Task<(SecureString passphrase, bool keysToo)> RequestWalletPassphraseByConsole(LockContext lockContext, int maxTryCount = 10) {
			return this.RequestPassphraseByConsole(lockContext, "wallet", maxTryCount);
		}

		public async Task<SecureString> RequestKeysPassphraseByConsole(string accountCode, string keyName, LockContext lockContext, int maxTryCount = 10) {
			return (await this.RequestPassphraseByConsole(lockContext, $"wallet key (account: {accountCode}, key name: {keyName})", maxTryCount).ConfigureAwait(false)).passphrase;
		}

		public Task RequestKeysCopyFileByConsole(string accountCode, string keyName, LockContext lockContext, int maxTryCount = 10) {
			this.CentralCoordinator.Log.Warning($"Wallet key file (account: {accountCode}, key name: {keyName}) is not present. Please copy it.", maxTryCount);

			Console.ReadKey();

			return Task.CompletedTask;
		}

		public Task RequestCopyWalletByConsole(LockContext lockContext) {
			this.CentralCoordinator.Log.Information("Please ensure the wallet file is in the wallets baseFolder");
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
				this.CentralCoordinator.Log.Verbose("");
				this.CentralCoordinator.Log.Verbose($"Enter your {passphraseType} passphrase (ESC to skip):");
				SecureString temp = await this.RequestConsolePassphrase(lockContext).ConfigureAwait(false);

				if(temp == null) {
					this.CentralCoordinator.Log.Verbose("Entry has been skipped.");

					return (null, false);
				}

				this.CentralCoordinator.Log.Verbose($"Enter your {passphraseType} passphrase again:");
				SecureString pass2 = await this.RequestConsolePassphrase(lockContext).ConfigureAwait(false);

				valid = temp.SecureStringEqual(pass2);

				if(!valid) {
					this.CentralCoordinator.Log.Verbose("Passphrases are different.");
				} else {
					// its valid!
					pass = temp;
				}

				counter++;
			} while((valid == false) && (counter < maxTryCount));

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
				if(((int) key.Key >= 65) && ((int) key.Key <= 90)) {
					// Append the character to the password.
					securePwd.AppendChar(key.KeyChar);
					Console.Write("*");
				}

				// Exit if Enter key is pressed.
			} while((key.Key != ConsoleKey.Enter) || (securePwd.Length == 0));

			this.CentralCoordinator.Log.Verbose("");

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

		protected virtual IWalletGenerationCache CreateNewWalletAccountGenerationCacheEntry(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountGenerationCache();
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

		public Task<IXmssWalletKey> CreateXmssKey(string name, Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			byte treeHeight = WalletProvider.MINIMAL_XMSS_KEY_HEIGHT;
			float xmssKeyWarningLevel = 0.7F;
			float xmssKeyChangeLevel = 0.9F;
			int keyHashType = 0;
			int? seedSize = null;

			byte noncesExponent = 4;
			WalletProvider.HashTypes hashType = WalletProvider.HashTypes.Sha2;
			
			int keyBackupHashType = 0;
			WalletProvider.HashTypes backupHashType = WalletProvider.HashTypes.Sha3;

			WalletProvider.HashTypes GetHashType(ChainConfigurations.HashTypes source) {
				switch(source) {
					case ChainConfigurations.HashTypes.Sha2:
						return WalletProvider.HashTypes.Sha2;
					case ChainConfigurations.HashTypes.Sha3:
						return WalletProvider.HashTypes.Sha3;
				}

				return WalletProvider.HashTypes.Sha3;
			}

			if(name == GlobalsService.TRANSACTION_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.TransactionXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.TransactionXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.TransactionXmssKeyChangeLevel;
				hashType = GetHashType(chainConfiguration.TransactionXmssKeyHashType);
				
				keyHashType = WalletProvider.TRANSACTION_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.TransactionXmssKeyBackupHashType); 
				keyBackupHashType = WalletProvider.TRANSACTION_KEY_HASH_BITS;
				seedSize = chainConfiguration.TransactionXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.TransactionXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.TRANSACTION_KEY_NONCES_EXPONENT;
			}
			
			if(name == GlobalsService.MESSAGE_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.MessageXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.MessageXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.MessageXmssKeyChangeLevel;
				hashType = GetHashType(chainConfiguration.MessageXmssKeyHashType);
				keyHashType = WalletProvider.MESSAGE_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.MessageXmssKeyBackupHashType);
				keyBackupHashType = WalletProvider.MESSAGE_KEY_HASH_BITS;
				seedSize = chainConfiguration.MessageXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.MessageXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.MESSAGE_KEY_NONCES_EXPONENT;
			}

			if(name == GlobalsService.CHANGE_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.ChangeXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.ChangeXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.ChangeXmssKeyChangeLevel;

				hashType = GetHashType(chainConfiguration.ChangeXmssKeyHashType);
				keyHashType = WalletProvider.CHANGE_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.ChangeXmssKeyBackupHashType);
				keyBackupHashType = WalletProvider.CHANGE_KEY_HASH_BITS;
				seedSize = chainConfiguration.ChangeXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.ChangeXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.CHANGE_KEY_NONCES_EXPONENT;
			}
			
			if(name == GlobalsService.SUPER_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.SuperXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.SuperXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.SuperXmssKeyChangeLevel;

				hashType = GetHashType(chainConfiguration.SuperXmssKeyHashType);
				keyHashType = WalletProvider.SUPER_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.SuperXmssKeyBackupHashType);
				keyBackupHashType = WalletProvider.SUPER_KEY_HASH_BITS;
				seedSize = chainConfiguration.SuperXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.SuperXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.SUPER_KEY_NONCES_EXPONENT;
			}
			
			if(name == GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.ValidatorSignatureXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.ValidatorSignatureXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.ValidatorSignatureXmssKeyChangeLevel;

				hashType = GetHashType(chainConfiguration.ValidatorSignatureXmssKeyHashType);
				keyHashType = WalletProvider.VALIDATOR_SIGNATURE_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.ValidatorSignatureXmssKeyBackupHashType);
				keyBackupHashType = WalletProvider.VALIDATOR_SIGNATURE_KEY_HASH_BITS;
				seedSize = chainConfiguration.ValidatorSignatureXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.ValidatorSignatureXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.VALIDATOR_SIGNATURE_NONCES_EXPONENT;
			}

			DateTime start = DateTime.Now;
			int lastTenth = -1;
			
			return this.CreateXmssKey(name, treeHeight, keyHashType, hashType, keyBackupHashType, backupHashType, xmssKeyWarningLevel, xmssKeyChangeLevel, async percentage => {
				TimeSpan remaining = TimeSpan.Zero;
				var passed = DateTime.Now - start;
				
				if(percentage > 0) {
					var total = TimeSpan.FromSeconds(100 * passed.TotalSeconds / percentage);
					remaining = total - passed;
				}

				int tenth = percentage / 10;

				string message = $"Generation {percentage}% completed for key {name}.  {passed:hh\\:mm\\:ss} elapsed and {remaining:hh\\:mm\\:ss} remaining";

				if(lastTenth != tenth) {
					lastTenth = tenth;
					this.CentralCoordinator.Log.Information(message);
				}
				else{
					this.CentralCoordinator.Log.Verbose(message);
				}
				
				if(progressCallback != null) {
					await progressCallback(percentage).ConfigureAwait(false);
				}

			}, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			return this.CreateXmssKey(name, XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT, WalletProvider.DEFAULT_KEY_HASH_BITS, WalletProvider.HashTypes.Sha2, WalletProvider.DEFAULT_KEY_BACKUP_HASH_BITS, WalletProvider.HashTypes.Sha3, warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}
		
		public Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, int hashBits, WalletProvider.HashTypes hashType, int backupHashBits, WalletProvider.HashTypes backupHashType, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			IXmssWalletKey key = this.CreateBasicKey<IXmssWalletKey>(name, CryptographicKeyTypes.Instance.XMSS);

			Enums.KeyHashType fullHashbits = WalletProvider.ConvertFullHashType(hashBits, hashType);
			Enums.KeyHashType fullBackupHashbits = WalletProvider.ConvertFullHashType(backupHashBits, backupHashType);
			
			return this.CreateXmssKey(name, treeHeight, fullHashbits, fullBackupHashbits,  warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}
		
		public async Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, Enums.KeyHashType hashbits, Enums.KeyHashType backupHashbits, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			IXmssWalletKey key = this.CreateBasicKey<IXmssWalletKey>(name, CryptographicKeyTypes.Instance.XMSS);
			
			using(XMSSProvider provider = new XMSSProvider(hashbits, backupHashbits, treeHeight, this.XmssThreadMode, noncesExponent)) {

				if(enableCache.HasValue) {
					provider.EnableCache = enableCache.Value;
				}

				provider.Initialize();

				if(prepare != null) {
					prepare(provider);
				}
				this.CentralCoordinator.Log.Information($"Creating a new XMSS key named '{name}' with tree height {treeHeight} and hashType {provider.HashType} and good for {provider.MaximumHeight} signatures.");

				(SafeArrayHandle privateKey, SafeArrayHandle publicKey) = await provider.GenerateKeys(seedSize, progressCallback, cacheMode, cacheLevels).ConfigureAwait(false);

				key.HashType = provider.HashTypeEnum;
				key.BackupHashType = provider.BackupHashTypeEnum;
				key.TreeHeight = provider.TreeHeight;
				key.NoncesExponent = provider.NoncesExponent;
				key.WarningHeight = provider.GetKeyUseThreshold(warningLevel);
				key.ChangeHeight = provider.GetKeyUseThreshold(changeLevel);
				key.MaximumHeight = provider.MaximumHeight;

				key.PrivateKey.Entry = privateKey.Entry;
				key.PublicKey.Entry = publicKey.Entry;
				
				// now set right away the next index cache
				key.NextKeyNodeCache.Entry = (await provider.GenerateIndexNodeCache(privateKey, (layer, height, pct) => {
							
                         if(pct > 0) {
                             this.CentralCoordinator.Log.Verbose($"node cache generation progress: {pct}% - layer {layer} of {height}");
                         }                   
                         return Task.CompletedTask;
                         
                     }).ConfigureAwait(false)).Entry;

				privateKey.Return();
				publicKey.Return();
			}

			this.HashKey(key);

			this.CentralCoordinator.Log.Information($"XMSS Key '{name}' created");

			return key;
		}
		
		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null) {

			float warningLevel = 0;
			float changeLevel = 0;
			DateTime start = DateTime.Now;
			return this.CreateXmssmtKey(name, warningLevel, changeLevel, async (treePct, layerPct, totalPct, tree, layerTrees, totalTrees, layer, totalLayers) => {
				
				TimeSpan remaining = TimeSpan.Zero;
				var passed = DateTime.Now - start;
				
				if(totalPct > 0) {
					var total = TimeSpan.FromSeconds((100 * passed.TotalSeconds / totalPct));
					remaining = total - passed;
				}
				
				Console.WriteLine($"key name: {name}, current tree {treePct}%, tree: {tree} of {layerTrees} trees in layer and out of {totalTrees} total trees, layer: {layer} of {totalLayers} layers: {layerPct}% in layer, {totalPct}% remaining. {passed:hh\\:mm\\:ss} elapsed and {remaining:hh\\:mm\\:ss} remaining");
				
				if(progressCallback != null) {
					await progressCallback(treePct, layerPct, totalPct, tree, layerTrees, totalTrees, layer, totalLayers).ConfigureAwait(false);
				}
			}, seedSize, cacheMode, cacheLevels, noncesExponent,enableCache, prepare);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="warningLevel"></param>
		/// <param name="changeLevel"></param>
		/// <param name="progressCallback">layerPct, totalPct, tree, layerTrees, totalTrees, layer, totalLayers</param>
		/// <param name="seedSize"></param>
		/// <param name="cacheMode"></param>
		/// <param name="cacheLevels"></param>
		/// <param name="noncesExponent"></param>
		/// <returns></returns>
		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null) {
			return this.CreateXmssmtKey(name, XMSSMTProvider.DEFAULT_XMSSMT_TREE_HEIGHT, XMSSMTProvider.DEFAULT_XMSSMT_TREE_LAYERS, XMSSProvider.DEFAULT_HASH_BITS, XMSSProvider.DEFAULT_BACKUP_HASH_BITS, warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="treeHeight"></param>
		/// <param name="treeLayers"></param>
		/// <param name="hashType"></param>
		/// <param name="backupHashType"></param>
		/// <param name="warningLevel"></param>
		/// <param name="changeLevel"></param>
		/// <param name="progressCallback">layerPct, totalPct, tree, layerTrees, totalTrees, layer, totalLayers</param>
		/// <param name="seedSize"></param>
		/// <param name="cacheMode"></param>
		/// <param name="cacheLevels"></param>
		/// <param name="noncesExponent"></param>
		/// <returns></returns>

		public async Task<IXmssMTWalletKey> CreateXmssmtKey(string name, byte treeHeight, byte treeLayers, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null) {
			IXmssMTWalletKey key = this.CreateBasicKey<IXmssMTWalletKey>(name, CryptographicKeyTypes.Instance.XMSSMT);

			using(XMSSMTProvider provider = new XMSSMTProvider(hashType, backupHashType, treeHeight, treeLayers, this.XmssThreadMode, noncesExponent)) {
				
				if(enableCache.HasValue) {
					provider.EnableCache = enableCache.Value;
				}
				
				provider.Initialize();

				if(prepare != null) {
					prepare(provider);
				}
				
				this.CentralCoordinator.Log.Information($"Creating a new XMSS^MT key named '{name}' with tree height {treeHeight}, tree layers {treeLayers} and hashType {provider.HashType} and good for {provider.MaximumHeight} signatures.");

				(SafeArrayHandle privateKey, SafeArrayHandle publicKey) = await provider.GenerateKeys(seedSize, true, progressCallback, cacheMode, cacheLevels).ConfigureAwait(false);

				key.HashType = provider.HashTypeEnum;
				key.BackupHashType = provider.BackupHashTypeEnum;
				key.TreeHeight = provider.TreeHeight;
				key.TreeLayers = provider.TreeLayers;
				key.NoncesExponent = provider.NoncesExponent;
				key.WarningHeight = provider.GetKeyUseThreshold(warningLevel);
				key.ChangeHeight = provider.GetKeyUseThreshold(changeLevel);
				key.MaximumHeight = provider.MaximumHeight;

				key.PrivateKey.Entry = privateKey.Entry;
				key.PublicKey.Entry = publicKey.Entry;

				privateKey.Return();
				publicKey.Return();
			}

			this.HashKey(key);

			this.CentralCoordinator.Log.Information("XMSS^MT Key created");

			return key;
		}

		public async Task<SafeArrayHandle> CreateNTRUPrimeAppointmentRequestKey(LockContext lockContext) {
			
			var account = await this.GetActiveAccount(lockContext).ConfigureAwait(false);

			NTRUPrimeEncryptor ntruDecryptor = new NTRUPrimeEncryptor(NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_761);
			var keyPair = ntruDecryptor.GenerateKeyPair();
			
			// set the private key in the wallet
			account.AccountAppointment.AppointmentPrivateKey = keyPair.privateKey;

			return keyPair.publicKey;
		}
		
		public async Task<INTRUPrimeWalletKey> CreateNTRUPrimeKey(string name, NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes strength) {
			INTRUPrimeWalletKey key = this.CreateBasicKey<INTRUPrimeWalletKey>(name, CryptographicKeyTypes.Instance.NTRUPrime);
			
			using(NTRUPrimeEncryptor ntruPrimeEncryptor = new NTRUPrimeEncryptor(strength)) {
				this.CentralCoordinator.Log.Information($"Creating a new NTRU Prime key named '{name} of strength {strength}'");

				(SafeArrayHandle publicKey, SafeArrayHandle privateKey) = await Task.Run(() => {
					return ntruPrimeEncryptor.GenerateKeyPair();
				}).ConfigureAwait(false);

				key.PrivateKey.Entry = privateKey.Entry;
				key.PublicKey.Entry = publicKey.Entry;
			}

			key.Strength = strength;
			
			this.HashKey(key);

			this.CentralCoordinator.Log.Information("NTRU Prime Key created");

			return key;
		}
		
		public Task<INTRUPrimeWalletKey> CreateValidatorSecretKey() {

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
			
			return this.CreateNTRUPrimeKey(GlobalsService.VALIDATOR_SECRET_KEY_NAME, NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_761);		
		}
		
		// public ISecretWalletKey CreateSecretKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null) {
		//
		// 	this.CentralCoordinator.Log.Information($"Creating a new Secret key named '{name}'. generating qTesla base.");
		//
		// 	ISecretWalletKey key = this.CreateBasicKey<ISecretWalletKey>(name, CryptographicKeyTypes.Instance.Secret);
		//
		// 	this.PrepareQTeslaKey(key, securityCategorySecret);
		//
		// 	key.KeyType = CryptographicKeyTypes.Instance.Secret;
		//
		// 	// since secret keys are often chained, here we ensure the new key contains the same general parameters as its previous one
		// 	if(previousKey != null) {
		// 		key.KeyAddress = previousKey.KeyAddress;
		// 		key.AccountCode = previousKey.AccountCode;
		// 	}
		//
		// 	this.CentralCoordinator.Log.Information("Secret Key created");
		//
		// 	return key;
		// }
		//
		// public ISecretComboWalletKey CreateSecretComboKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null) {
		//
		// 	this.CentralCoordinator.Log.Information($"Creating a new Secret combo key named '{name}'. generating qTesla base.");
		//
		// 	ISecretComboWalletKey key = this.CreateBasicKey<ISecretComboWalletKey>(name, CryptographicKeyTypes.Instance.SecretCombo);
		//
		// 	this.PrepareQTeslaKey(key, securityCategorySecret);
		//
		// 	key.KeyType = CryptographicKeyTypes.Instance.SecretCombo;
		//
		// 	key.PromisedNonce1 = GlobalRandom.GetNextLong();
		// 	key.PromisedNonce2 = GlobalRandom.GetNextLong();
		//
		// 	// since secret keys are often chained, here we ensure the new key contains the same general parameters as its previous one
		// 	if(previousKey != null) {
		// 		key.KeyAddress = previousKey.KeyAddress;
		// 		key.AccountCode = previousKey.AccountCode;
		// 	}
		//
		// 	this.CentralCoordinator.Log.Information("Secret combo Key created");
		//
		// 	return key;
		// }

		// public ISecretDoubleWalletKey CreateSecretDoubleKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, QTESLASecurityCategory.SecurityCategories securityCategorySecond, ISecretDoubleWalletKey previousKey = null) {
		//
		// 	this.CentralCoordinator.Log.Information($"Creating a new Secret double key named '{name}'. generating qTesla base.");
		// 	ISecretDoubleWalletKey key = this.CreateBasicKey<ISecretDoubleWalletKey>(name, CryptographicKeyTypes.Instance.SecretDouble);
		//
		// 	this.PrepareQTeslaKey(key, securityCategorySecret);
		//
		// 	key.KeyType = CryptographicKeyTypes.Instance.SecretDouble;
		//
		// 	key.PromisedNonce1 = GlobalRandom.GetNextLong();
		// 	key.PromisedNonce2 = GlobalRandom.GetNextLong();
		//
		// 	key.SecondKey = (QTeslaWalletKey) this.CreateQTeslaKey(name, securityCategorySecond);
		//
		// 	// since secret keys are often chained, here we ensure the new key contains the same general parameters as its previous one
		// 	if(previousKey != null) {
		// 		key.KeyAddress = previousKey.KeyAddress;
		// 		key.AccountCode = previousKey.AccountCode;
		// 	}
		//
		// 	this.CentralCoordinator.Log.Information("Secret double Key created");
		//
		// 	return key;
		// }

		/// <summary>
		///     Here, we sign a message with the
		/// </summary>
		/// <param name="key"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<SafeArrayHandle> PerformCryptographicSignature(string accountCode, string keyName, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {

			this.EnsureWalletIsLoaded();
			await this.EnsureWalletKeyIsReady(accountCode, keyName, lockContext).ConfigureAwait(false);

			IWalletKey key = await this.LoadKey<IWalletKey>(k => {
				return k;
			}, accountCode, keyName, lockContext).ConfigureAwait(false);

			if(key == null) {
				throw new ApplicationException($"The key named '{keyName}' could not be loaded. Make sure it is available before progressing.");
			}

			return await this.PerformCryptographicSignature(key, message, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}

		public virtual async Task<SafeArrayHandle> PerformCryptographicSignature(IWalletKey key, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {

			this.EnsureWalletIsLoaded();
			await this.EnsureWalletKeyIsReady(key.AccountCode, key.Name, lockContext).ConfigureAwait(false);

			SafeArrayHandle signature = null;

			if(key is IXmssWalletKey xmssWalletKey) {

				// check if we reached the maximum use of our key
				bool keyStillUsable = (xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex < xmssWalletKey.ChangeHeight) || allowPassKeyLimit;
				bool keyMaxedOut = xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex > xmssWalletKey.MaximumHeight;

				if(keyStillUsable && !keyMaxedOut) {

					(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey) result;
					
					if(key is IXmssMTWalletKey xmssMTWalletKey && (key.Version.Type == CryptographicKeyTypes.Instance.XMSSMT)) {
						result = await this.PerformXmssmtCryptographicSignature(xmssMTWalletKey, message, lockContext).ConfigureAwait(false);
					}
					else{
						result = await this.PerformXmssCryptographicSignature(xmssWalletKey, message, lockContext).ConfigureAwait(false);
					}
					
					signature = result.signature;

					// now we increment our private key
					xmssWalletKey.PrivateKey.Entry = result.nextPrivateKey.Entry;
					
					// reset the next node cache entry, we did not generate it yet
					xmssWalletKey.NextKeyNodeCache.Entry = null;
					
					xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex += 1;

					result.nextPrivateKey.Return();

					// save the key change
					await this.UpdateKey(key, lockContext).ConfigureAwait(false);
				}

				List<(string accountCode, string name)> forcedKeys = new List<(string accountCode, string name)>();

				// we are about to use this key, let's make sure we check it to eliminate any applicable timeouts
				forcedKeys.Add((key.AccountCode, key.Name));

				await this.ResetAllTimedOut(lockContext, forcedKeys).ConfigureAwait(false);

				if(key.Status != Enums.KeyStatus.Changing) {
					// Here we trigger the key change workflow, we must change the key, its time adn we wont trust the user to do it in time at this point. they were warned already

					if(keyMaxedOut) {
						this.CentralCoordinator.Log.Fatal($"Key named {key.Name} has reached end of life. It must be changed with a super key.");
					} else if(xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex >= xmssWalletKey.ChangeHeight) {
						this.CentralCoordinator.Log.Warning($"Key named {key.Name} has reached end of life. An automatic key change is being performed. You can not use the key until the change is fully confirmed.");

						this.KeyUseMaximumLevelReached(key.KeyAddress.OrdinalId, xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex, xmssWalletKey.WarningHeight, xmssWalletKey.ChangeHeight, new CorrelationContext());
					} else if(xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex >= xmssWalletKey.WarningHeight) {
						this.CentralCoordinator.Log.Warning($"Key named {key.Name}  nearing its end of life. An automatic key change is being performed. You can keep using it until the change is fully confirmed.");
						this.KeyUseWarningLevelReached(key.KeyAddress.OrdinalId, xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex, xmssWalletKey.WarningHeight, xmssWalletKey.ChangeHeight, new CorrelationContext());
					}
				}

				if(!keyStillUsable) {
					// we have reached the maximum use amount for this key. we can't sign anything else until a key change happens
					throw new ApplicationException("Your xmss key has reached it's full use. A key change must now be performed!");
				}

				// ok, now we add an asynchronous prefetch for the key nodes caching
				List<Func<LockContext, Task>> transactionalSuccessActions = new List<Func<LockContext, Task>>();
				transactionalSuccessActions.Add(lc => {
					
					var factory = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase;
					var workflow = factory.CreateGenerateXmssKeyIndexNodeCacheWorkflow(xmssWalletKey.AccountCode, xmssWalletKey.Ordinal, xmssWalletKey.Index.KeyUseIndex, new CorrelationContext());
					this.CentralCoordinator.PostWorkflow(workflow);
					
					return Task.CompletedTask;
				});

				await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.AddTransactionSuccessActions(transactionalSuccessActions, lockContext).ConfigureAwait(false);
			}  else {
				throw new ApplicationException("Invalid key type provided");
			}

			return signature;
		}

		public virtual Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssCryptographicSignature(IXmssWalletKey keyxmssWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null, SafeArrayHandle extraNodeCache = null, Action<XMSSProvider> callback = null, Func<int, int ,int, Task> progressCallback = null) {

			using XMSSProvider provider = new XMSSProvider(keyxmssWalletKey.HashType, keyxmssWalletKey.BackupHashType, keyxmssWalletKey.TreeHeight, this.XmssThreadMode, keyxmssWalletKey.NoncesExponent);
			provider.Initialize();

			if(callback != null) {
				callback(provider);
			}
			return provider.Sign(message, keyxmssWalletKey.PrivateKey, buildOptimizedSignature, xmssSignaturePathCache, extraNodeCache, progressCallback);
		}
		
		public virtual Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssmtCryptographicSignature(IXmssMTWalletKey xmssMTWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {

			using XMSSMTProvider provider = new XMSSMTProvider(xmssMTWalletKey.HashType, xmssMTWalletKey.BackupHashType, xmssMTWalletKey.TreeHeight, xmssMTWalletKey.TreeLayers, this.XmssThreadMode, xmssMTWalletKey.NoncesExponent);
			provider.Initialize();
			
			return provider.Sign(message, xmssMTWalletKey.PrivateKey);
		}

		protected virtual void KeyUseWarningLevelReached(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			// do nothing
			this.LaunchChangeKeyWorkflow(changeKeyOrdinal, keyUseIndex, warningHeight, maximumHeight, correlationContext);
		}

		protected virtual void KeyUseMaximumLevelReached(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			this.LaunchChangeKeyWorkflow(changeKeyOrdinal, keyUseIndex, warningHeight, maximumHeight, correlationContext);
		}

		protected virtual void LaunchChangeKeyWorkflow(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> changeKeyTransactionWorkflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateChangeKeyTransactionWorkflow(changeKeyOrdinal, "automatically triggered keychange", correlationContext);

			this.centralCoordinator.PostWorkflow(changeKeyTransactionWorkflow);
		}

		public virtual async Task<bool> ResetAllTimedOut(LockContext lockContext, List<(string accountCode, string name)> forcedKeys = null) {

			this.EnsureWalletIsLoaded();

			bool? synced = await this.Synced(lockContext).ConfigureAwait(false);

			if(!synced.HasValue || !synced.Value) {
				// we cant do it if not synced, we will give a chance for the transactions to arrive
				return false;
			}

			bool changed = await this.ResetTimedOutWalletEntries(lockContext, forcedKeys).ConfigureAwait(false);

			Dictionary<AccountId, int> result = await this.ClearTimedOutTransactions(lockContext).ConfigureAwait(false);

			if(result.Any(e => e.Value != 0)) {
				changed = true;
			}

			return changed;
		}

		/// <summary>
		///     update wallet accounts adn keys for any timeout in transaction operations.
		/// </summary>
		/// <param name="forcedKeys"></param>
		public virtual async Task<bool> ResetTimedOutWalletEntries(LockContext lockContext, List<(string accountCode, string name)> forcedKeys = null) {
			this.EnsureWalletIsLoaded();

			bool? synced = await this.Synced(lockContext).ConfigureAwait(false);

			if(!synced.HasValue || !synced.Value) {
				// we cant do it if not synced, we will give a chance for the transactions to arrive
				return false;
			}

			// let's use the last block timestamp as a limit, in case its not up to date
			DateTime lastBlockTimestamp = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockTimestamp;

			List<IWalletAccount> accounts = await this.GetAllAccounts(lockContext).ConfigureAwait(false);

			bool changed = false;

			foreach(IWalletAccount account in accounts) {
				//now we take care of presentation transactions
#if MAINNET_LAUNCH_CODE
				if(DateTimeEx.CurrentTime > GlobalsService.MainnetLauchTime.AddDays(2)) {
					if((account.Status == Enums.PublicationStatus.Dispatched) && account.PresentationTransactionTimeout.HasValue && (account.PresentationTransactionTimeout.Value < lastBlockTimestamp)) {
						// ok, this is a timeout, we reset it
						account.PresentationTransactionTimeout = null;
						account.PresentationTransactionId = null;
						account.Status = Enums.PublicationStatus.New;
						changed = true;
					}
				}
#else
clean above
				if((account.Status == Enums.PublicationStatus.Dispatched) && account.PresentationTransactionTimeout.HasValue && (account.PresentationTransactionTimeout.Value < lastBlockTimestamp)) {
					// ok, this is a timeout, we reset it
					account.PresentationTransactionTimeout = null;
					account.PresentationTransactionId = null;
					account.Status = Enums.PublicationStatus.New;
					changed = true;
				}
#endif

			}

			changed = false;

			foreach(IWalletAccount account in accounts) {
				// and finally keys if we can
				foreach(KeyInfo key in account.Keys) {

					if((forcedKeys != null) && forcedKeys.Contains((account.AccountCode, key.Name))) {
						// ok, this key MUST be checked
						this.EnsureKeyFileIsPresent(account.AccountCode, key.Name, 1, lockContext);
						this.EnsureKeyPassphrase(account.AccountCode, key.Name, 1, lockContext);
					}

					if(this.IsKeyFileIsPresent(account.AccountCode, key.Name, 1, lockContext) && this.IsKeyPassphraseValid(account.AccountCode, key.Name, 1, lockContext)) {
						using IWalletKey walletKey = await this.LoadKey(account.AccountCode, key.Name, lockContext).ConfigureAwait(false);

						if((walletKey.Status == Enums.KeyStatus.Changing) && walletKey.KeyChangeTimeout.HasValue && (walletKey.KeyChangeTimeout.Value < lastBlockTimestamp)) {

							walletKey.Status = Enums.KeyStatus.Ready;
							walletKey.KeyChangeTimeout = null;
							walletKey.ChangeTransactionId = null;
							changed = true;
						}

					}
				}
			}

			return changed;
		}

		public void ClearDistilledAppointmentContextFile() {
			string path = this.GetDistilledAppointmentContextPath();

			if(this.fileSystem.FileExists(path)) {
				this.fileSystem.DeleteFile(path);
			}
		}

		public Task WriteDistilledAppointmentContextFile(DistilledAppointmentContext distilledAppointmentContext) {
			
			string path = this.GetDistilledAppointmentContextPath();
			
			FileExtensions.WriteAllText(path, JsonSerializer.Serialize(distilledAppointmentContext), this.fileSystem);

			return Task.CompletedTask;
		}
		
		public Task<DistilledAppointmentContext> GetDistilledAppointmentContextFile() {

			DistilledAppointmentContext distilledAppointmentContext = null;

			string path = this.GetDistilledAppointmentContextPath();

			if(this.fileSystem.FileExists(path)) {
				
				distilledAppointmentContext = JsonSerializer.Deserialize<DistilledAppointmentContext>(FileExtensions.ReadAllText(path, this.fileSystem));
			}

			return Task.FromResult(distilledAppointmentContext);
		}

		public async Task UpdateMiningStatistics(AccountId accountId, Enums.MiningTiers miningTiers, Action<WalletElectionsMiningSessionStatistics> sessionCallback, Action<WalletElectionsMiningAggregateStatistics> totalCallback, LockContext lockContext, bool resetSession = false) {

			var configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if((sessionCallback == null && totalCallback == null) || configuration.MiningStatistics == ChainConfigurations.MiningStatisticsModes.None) {
				return;
			}
			
			var account = await this.GetWalletAccount(accountId, lockContext).ConfigureAwait(false);
			var statisticsInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletElectionsStatisticsInfo;
			
			if(configuration.MiningStatistics.HasFlag(ChainConfigurations.MiningStatisticsModes.Session) && sessionCallback != null) {
				try {
					if(resetSession) {
						await statisticsInfo.CloseSessionStatistics(lockContext).ConfigureAwait(false);
					}
					var statistic = await statisticsInfo.SessionStatisticsBase(miningTiers, lockContext).ConfigureAwait(false);

					if(statistic == null) {
						throw new ArgumentNullException(nameof(statistic));
					}
					sessionCallback(statistic);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to update session wallet mining statistics.");
				}
			}
			
			if(configuration.MiningStatistics.HasFlag(ChainConfigurations.MiningStatisticsModes.Total) && totalCallback != null) {
				try {
					var statistic = await statisticsInfo.AggregateStatisticsBase(miningTiers, lockContext).ConfigureAwait(false);

					if(statistic == null) {
						throw new ArgumentNullException(nameof(statistic));
					}
					totalCallback(statistic);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to update total wallet mining statistics.");
				}
			}
		}

		public async Task StopSessionMiningStatistics(AccountId accountId, LockContext lockContext) {
			var configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
			
			if(configuration.MiningStatistics.HasFlag(ChainConfigurations.MiningStatisticsModes.Session)) {
				
				try {
					var account = await this.GetWalletAccount(accountId, lockContext).ConfigureAwait(false);
					var statisticsInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletElectionsStatisticsInfo;

					await statisticsInfo.CloseSessionStatistics(lockContext).ConfigureAwait(false);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to update total wallet mining statistics.");
				}
			}
		}

		public async Task<(MiningStatisticSessionAPI session, MiningStatisticAggregateAPI aggregate)> QueryMiningStatistics(AccountId miningAccountId, Enums.MiningTiers miningTiers, LockContext lockContext) {
			
			var configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
			var account = await this.GetWalletAccount(miningAccountId, lockContext).ConfigureAwait(false);
			var statisticsInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletElectionsStatisticsInfo;

			var aggregateStatisticsEntry = await statisticsInfo.AggregateStatisticsBase(miningTiers, lockContext).ConfigureAwait(false);

			if(aggregateStatisticsEntry == null) {
				throw new ArgumentNullException(nameof(aggregateStatisticsEntry));
			}
			
			(var miningStatisticSessionSet, var miningStatisticAggregateSet) = this.CreateMiningStatisticSet();
			
			this.PrepareMiningStatisticAggregateSet(miningStatisticAggregateSet, aggregateStatisticsEntry);

			if(configuration.MiningStatistics.HasFlag(ChainConfigurations.MiningStatisticsModes.Session)) {
				
				// dont load or start anything. just query as it is
				var sessionStatisticsEntry = statisticsInfo.SessionStatisticsBaseAsIs(miningTiers, lockContext);

				if(sessionStatisticsEntry != null) {
					this.PrepareMiningStatisticSessionSet(miningStatisticSessionSet, sessionStatisticsEntry);
				}
			}

			return (miningStatisticSessionSet, miningStatisticAggregateSet);
		}

		protected virtual void PrepareMiningStatisticAggregateSet(MiningStatisticAggregateAPI miningStatisticAggregateSet, WalletElectionsMiningAggregateStatistics aggregateStatisticsEntry) {
			miningStatisticAggregateSet.BlocksProcessed = aggregateStatisticsEntry.BlocksProcessed;
			miningStatisticAggregateSet.BlocksElected = aggregateStatisticsEntry.BlocksElected;
			miningStatisticAggregateSet.MiningSessions = aggregateStatisticsEntry.MiningSessions;
			miningStatisticAggregateSet.PercentElected = 0;
			if(aggregateStatisticsEntry.BlocksProcessed != 0){
				miningStatisticAggregateSet.PercentElected = (double)aggregateStatisticsEntry.BlocksElected / aggregateStatisticsEntry.BlocksProcessed;
			}
			miningStatisticAggregateSet.LastBlockElected = aggregateStatisticsEntry.LastBlockElected;
		}
		
		protected virtual void PrepareMiningStatisticSessionSet(MiningStatisticSessionAPI miningStatisticSessionSet, WalletElectionsMiningSessionStatistics sessionStatisticsEntry) {
			miningStatisticSessionSet.BlocksProcessed = sessionStatisticsEntry.BlocksProcessed;
			miningStatisticSessionSet.BlocksElected = sessionStatisticsEntry.BlocksElected;
			miningStatisticSessionSet.BlockStarted = sessionStatisticsEntry.BlockStarted;
			miningStatisticSessionSet.Start = sessionStatisticsEntry.Start;
			miningStatisticSessionSet.PercentElected = 0;
			if(miningStatisticSessionSet.BlocksProcessed != 0){
				miningStatisticSessionSet.PercentElected = (double)sessionStatisticsEntry.BlocksElected / sessionStatisticsEntry.BlocksProcessed;
			}
			miningStatisticSessionSet.LastBlockElected = sessionStatisticsEntry.LastBlockElected;
		}
		
		protected abstract (MiningStatisticSessionAPI session, MiningStatisticAggregateAPI aggregate) CreateMiningStatisticSet();
		
		/// <summary>
		///     here we remove all timed out transactions from the wallet
		/// </summary>
		public virtual async Task<Dictionary<AccountId, int>> ClearTimedOutTransactions(LockContext lockContext) {

			this.EnsureWalletIsLoaded();

			bool? synced = await this.Synced(lockContext).ConfigureAwait(false);

			if(!synced.HasValue || !synced.Value) {
				// we cant do it if not synced, we will give a chance for the transactions to arrive
				return new Dictionary<AccountId, int>();
			}

			// let's use the last block timestamp as a limit, in case its not up to date
			DateTime lastBlockTimestamp = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockTimestamp;

			List<IWalletAccount> accounts = await this.GetAllAccounts(lockContext).ConfigureAwait(false);

			bool changed = false;

			Dictionary<AccountId, int> totals = new Dictionary<AccountId, int>();

			foreach(IWalletAccount account in accounts) {

				int total = 0;
				IWalletGenerationCacheFileInfo generationCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountCode].WalletGenerationCacheInfo;

				total = await generationCacheFileInfo.ClearTimedOut(lastBlockTimestamp, lockContext).ConfigureAwait(false);

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
		public virtual async Task<List<WalletTransactionHistoryHeaderAPI>> APIQueryWalletTransactionHistory(string accountCode, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			WalletTransactionHistoryHeaderAPI[] results = await this.WalletFileInfo.Accounts[accountCode].WalletTransactionHistoryInfo.RunQuery<WalletTransactionHistoryHeaderAPI, WalletTransactionHistory>(caches => caches.Select(t => {

				TransactionId transactionId = new TransactionId(t.TransactionId);
				ComponentVersion<TransactionType> version = new ComponentVersion<TransactionType>(t.Version);

				return new WalletTransactionHistoryHeaderAPI {
					TransactionId = t.TransactionId, Sender = transactionId.Account.ToString(), Timestamp = TimeService.FormatDateTimeStandardUtc(t.Timestamp), Status = (byte)t.Status,
					Version = new VersionAPI {TransactionType = version.Type.Value.Value, Major = version.Major.Value, Minor = version.Minor}, Local = t.Local, Note = t.Note, Recipient = t.Recipient
				};
			}).OrderByDescending(t => t.Timestamp).ToList(), lockContext).ConfigureAwait(false);

			return results.ToList();

		}

		public virtual async Task<WalletTransactionHistoryDetailsAPI> APIQueryWalletTransactionHistoryDetails(string accountCode, string transactionId, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			if(string.IsNullOrWhiteSpace(accountCode)) {
				accountCode = await this.GetAccountCode(lockContext).ConfigureAwait(false);
			}

			WalletTransactionHistoryDetailsAPI[] results = await this.WalletFileInfo.Accounts[accountCode].WalletTransactionHistoryInfo.RunQuery<WalletTransactionHistoryDetailsAPI, WalletTransactionHistory>(caches => caches.Where(t => t.TransactionId == transactionId).Select(t => {

				ComponentVersion<TransactionType> version = new ComponentVersion<TransactionType>(t.Version);

				return new WalletTransactionHistoryDetailsAPI {
					TransactionId = t.TransactionId, Sender = new TransactionId(t.TransactionId).Account.ToString(), Timestamp = TimeService.FormatDateTimeStandardUtc(t.Timestamp), Status = (byte)t.Status,
					Version = new VersionAPI {TransactionType = version.Type.Value.Value, Major = version.Major.Value, Minor = version.Minor}, Recipient = t.Recipient, Contents = t.Contents, Local = t.Local,
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
			walletInfoApi.WalletFullyCreated = walletInfoApi.WalletExists && await this.WalletFullyCreated(lockContext).ConfigureAwait(false);
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

			string activeAccountCode = (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).AccountCode;

			List<WalletAccountAPI> apiAccounts = new List<WalletAccountAPI>();

			foreach(IWalletAccount account in await this.GetAccounts(lockContext).ConfigureAwait(false)) {

				apiAccounts.Add(new WalletAccountAPI {
					AccountCode = account.AccountCode, AccountId = account.GetAccountId().ToString(), FriendlyName = account.FriendlyName, Status = (int) account.Status,
					IsActive = account.AccountCode == activeAccountCode
				});
			}

			return apiAccounts;

		}

		/// <summary>
		///     Get the details of an account
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<WalletAccountDetailsAPI> APIQueryWalletAccountDetails(string accountCode, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			string activeAccountCode = (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).AccountCode;
			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			int verified = (int) account.VerificationLevel;
			
			bool verificationExpiring = false;
			bool verificationExpired = false;

			DateTime? verificationExpirationWarning = null;

			if(account.VerificationExpirationDate.HasValue) {
				verificationExpirationWarning = account.VerificationExpirationDate.Value.AddDays(-7);
			}
			if(account.VerificationLevel == Enums.AccountVerificationTypes.Appointment || account.VerificationLevel == Enums.AccountVerificationTypes.SMS) {
				if(!account.VerificationExpirationDate.HasValue || account.VerificationExpirationDate.Value < DateTimeEx.CurrentTime) {
					verified = (int)Enums.AccountVerificationTypes.Expired;
					verificationExpired = true;
				}
				else if(!account.VerificationExpirationDate.HasValue || verificationExpirationWarning < DateTimeEx.CurrentTime) {
					verified = (int)Enums.AccountVerificationTypes.Expired;
					verificationExpiring = true;
				}
			}

			
			return new WalletAccountDetailsAPI {
				AccountCode = account.AccountCode, AccountId = account.PublicAccountId?.ToString(), AccountHash = account.PresentationId?.ToString(), FriendlyName = account.FriendlyName,
				Status = (int) account.Status, IsActive = account.AccountCode == activeAccountCode, AccountType = (int) account.WalletAccountType, TrustLevel = account.TrustLevel,
				DeclarationBlockId = account.ConfirmationBlockId, KeysEncrypted = account.KeysEncrypted, Verification = verified, InAppointment = account.AccountAppointment != null,VerificationExpirationWarning = verificationExpirationWarning,  VerificationExpiration = account.VerificationExpirationDate.HasValue?account.VerificationExpirationDate.Value.ToUniversalTime():(DateTime?)null,
				VerificationExpiring = verificationExpiring, VerificationExpired = verificationExpired
			};

		}
		
		/// <summary>
		///     Get the details of an account
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<WalletAccountAppointmentDetailsAPI> APIQueryWalletAccountAppointmentDetails(string accountCode, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			string activeAccountCode = (await this.GetActiveAccount(lockContext).ConfigureAwait(false)).AccountCode;
			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			if(account.AccountAppointment == null) {
				return new WalletAccountAppointmentDetailsAPI() {Status = (int) Enums.AppointmentStatus.None};
			}

			return new WalletAccountAppointmentDetailsAPI {
				Status = (int) account.AccountAppointment.AppointmentStatus, AppointmentRequestTimeStamp = account.AccountAppointment.AppointmentRequestTimeStamp, AppointmentConfirmationId = account.AccountAppointment.AppointmentConfirmationCode
				, AppointmentTime = account.AccountAppointment.AppointmentTime, AppointmentContextTime = account.AccountAppointment.AppointmentContextTime, 
				AppointmentVerificationTime = account.AccountAppointment.AppointmentVerificationTime, 
				AppointmentWindow = account.AccountAppointment.AppointmentWindow, AppointmentConfirmationIdExpiration = account.AccountAppointment.AppointmentConfirmationCodeExpiration
			};
		}

		public async Task<TransactionId> APIQueryWalletAccountPresentationTransactionId(string accountCode, LockContext lockContext) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			if(account == null) {
				throw new ApplicationException($"Failed to load account with Uuid {accountCode}");
			}

			return account.PresentationTransactionId.Clone;

		}

		public async Task<AccountAppointmentConfirmationResultAPI> APIQueryAppointmentConfirmationResult(string accountCode, LockContext lockContext) {
			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			if(account == null) {
				throw new ApplicationException($"Failed to load account with Uuid {accountCode}");
			}
			
			AccountAppointmentConfirmationResultAPI result = new AccountAppointmentConfirmationResultAPI();

			if(account.AccountAppointment != null && account.AccountAppointment?.AppointmentStatus == Enums.AppointmentStatus.AppointmentCompleted) {
				if(account.AccountAppointment.AppointmentConfirmationCodeExpiration.HasValue && account.AccountAppointment.AppointmentConfirmationCodeExpiration.Value >= DateTimeEx.CurrentTime) {
					result.Verified = account.AccountAppointment.AppointmentVerified;
					result.ConfirmationCode = account.AccountAppointment.AppointmentConfirmationCode?.ToString();
				}
			}

			return result;
		}

		public async Task<bool> ClearAppointment(string accountCode, LockContext lockContext, bool force = false) {
			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			if(account == null) {
				throw new ApplicationException($"Failed to load account with Uuid {accountCode}");
			}

			if(account.AccountAppointment != null) {

				if(force || account.AccountAppointment.AppointmentTime < DateTimeEx.CurrentTime) {

					account.AccountAppointment = null;
					this.CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = Enums.OperationStatus.None;
					return true;
				}
			}

			return false;
		}
		
		public async Task<AccountCanPublishAPI> APICanPublishAccount(string accountCode, LockContext lockContext) {
			IWalletAccount account = await this.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			if(account == null) {
				throw new ApplicationException($"Failed to load account with Uuid {accountCode}");
			}
			
			AccountCanPublishAPI result = new AccountCanPublishAPI();

			if(account.Status != Enums.PublicationStatus.New && account.AccountAppointment == null) {
				return result;
			}
			
			// first is always appointments
			if(account.AccountAppointment != null) {
				if(account.AccountAppointment?.AppointmentStatus == Enums.AppointmentStatus.AppointmentCompleted) {
					if(account.AccountAppointment.AppointmentConfirmationCode.HasValue && account.AccountAppointment.AppointmentConfirmationCodeExpiration.HasValue && account.AccountAppointment.AppointmentConfirmationCodeExpiration.Value >= DateTimeEx.CurrentTime) {

						if(account.AccountAppointment.AppointmentVerified.HasValue) {
							if(account.AccountAppointment.AppointmentVerified.Value) {
								result.CanPublish = true;
								result.PublishMode = (int) Enums.AccountPublicationModes.Appointment;
								result.ConfirmationCode = account.AccountAppointment.AppointmentConfirmationCode.Value.ToString();
								result.RequesterId = account.AccountAppointment.RequesterId.ToString();

								return result;
							} else {
								// we failed verification but perhaps SMS can do it
							}
						}
					}
				}
			} else {
				if(account.SMSDetails != null && account.SMSDetails.ConfirmationCodeExpiration >= DateTimeEx.CurrentTime) {
					result.CanPublish = true;
					result.PublishMode = (int) Enums.AccountPublicationModes.SMS;
					result.ConfirmationCode = account.SMSDetails.ConfirmationCode.ToString();
					result.RequesterId = account.SMSDetails.RequesterId.ToString();

					return result;
				}

				if(account.WalletAccountType == Enums.AccountTypes.Server) {
					result.CanPublish = true;
					result.PublishMode = (int) Enums.AccountPublicationModes.Server;
				
					return result;
				}
			}
			
			return result;
		}


	#endregion

	#region walletservice methods

		public async Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false) {
			this.EnsureWalletIsLoaded();

			//TODO: make sure we confirm our signature height in the wallet with the recorded one on chain. To prevent mistaken wallet copies.
			IWalletAccount activeAccount = await this.GetActiveAccount(lockContext).ConfigureAwait(false);

			using IWalletKey key = await this.LoadKey<IWalletKey>(k => k, activeAccount.AccountCode, keyName, lockContext).ConfigureAwait(false);

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
				IdKeyUseIndexSet lastSyncedIdKeyUse = await this.GetChainStateLastSyncedKeyHeight(key, lockContext).ConfigureAwait(false);

				if(lastSyncedIdKeyUse.IsSet && key.KeyAddress.KeyUseIndex < lastSyncedIdKeyUse) {
					string message =$"Your key height for your key named {key.Name} is lower than the blockchain key use height. This is a very serious security issue. You might be using an older copy of your regular wallet.";
					throw new ReportableException(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER_THAN_DETECTED, ReportableException.PriorityLevels.Warning, ReportableException.ReportLevels.Modal, this.centralCoordinator.ChainId, this.centralCoordinator.ChainName, message, new string[]{key.Name, key.Ordinal.ToString()});
					
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
			await this.UpdateLocalChainStateTransactionKeyLatestSyncHeight(key.AccountCode, key.KeyAddress.KeyUseIndex, lockContext).ConfigureAwait(false);

			return results;
		}

		public async Task<SafeArrayHandle> SignMessageXmss(string accountCode, SafeArrayHandle message, LockContext lockContext) {

			if(string.IsNullOrWhiteSpace(accountCode)) {
				accountCode = await this.GetAccountCode(lockContext).ConfigureAwait(false);
			}

			using IXmssWalletKey key = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(accountCode, GlobalsService.MESSAGE_KEY_NAME, lockContext).ConfigureAwait(false);

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
			synthesizedBlock.ModeratorKeyOrdinal = synthesizedBlockApi.ModeratorKeyOrdinal;

			BrotliCompression compressor = new BrotliCompression();

			synthesizedBlock.RejectedTransactions.AddRange(synthesizedBlockApi.RejectedTransactions.Select(t => new RejectedTransaction {TransactionId = new TransactionId(t.Key), Reason = (RejectionCode) t.Value}));

			AccountId accountId = null;

			bool hasPublicAccount = !string.IsNullOrWhiteSpace(synthesizedBlockApi.AccountId);
			bool hasAcountCode = !string.IsNullOrWhiteSpace(synthesizedBlockApi.AccountCode);

			//If it's the genesis, then we need to add all transactions even if we have no account.
			bool isGenesis = synthesizedBlockApi.BlockId == 1;
			bool hasAccount = hasPublicAccount || hasAcountCode;
			if (hasAccount || isGenesis) {
				if (hasAccount)
				{
					List<IWalletAccount> accounts = await this.GetAccounts(lockContext).ConfigureAwait(false);

					if (hasPublicAccount)
					{
						accountId = new AccountId(synthesizedBlockApi.AccountId);

						if (accounts.All(a => a.PublicAccountId != accountId))
						{
							throw new ApplicationException();
						}

						synthesizedBlock.AccountType = SynthesizedBlock.AccountIdTypes.Public;
					}
					else if (hasAcountCode)
					{
						accountId = new AccountId(synthesizedBlockApi.AccountCode);

						if (accounts.All(a => a.PresentationId != accountId))
						{
							throw new ApplicationException();
						}

						synthesizedBlock.AccountType = SynthesizedBlock.AccountIdTypes.Code;
					}
				}

				SynthesizedBlock.SynthesizedBlockAccountSet synthesizedBlockAccountSet = new SynthesizedBlock.SynthesizedBlockAccountSet();
				
				ITransaction Rehydrate(byte[] bytes) {
					IDehydratedTransaction dehydratedTransaction = new DehydratedTransaction();

					using SafeArrayHandle wrappedBytes = compressor.Decompress(SafeArrayHandle.Wrap(bytes));
					dehydratedTransaction.Rehydrate(wrappedBytes);

					ITransaction transaction = dehydratedTransaction.Rehydrate(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

					if (hasAccount)
					{
						if (transaction.TransactionId.Account == accountId)
						{
							synthesizedBlockAccountSet.ConfirmedLocalTransactions.Add(transaction.TransactionId, transaction);
						}
						else if (transaction.TargetType == Enums.TransactionTargetTypes.All || (transaction.TargetType == Enums.TransactionTargetTypes.Range && transaction.ImpactedAccounts.Contains(accountId)))
						{
							synthesizedBlockAccountSet.ConfirmedExternalsTransactions.Add(transaction.TransactionId, transaction);
						}
					}

					return transaction;
				}
				
				foreach(var apiTransaction in synthesizedBlockApi.ConfirmedIndexedTransactions) {
					
					ITransaction transaction = Rehydrate(apiTransaction.Value.Bytes);
					synthesizedBlock.ConfirmedIndexedTransactions.Add(transaction.TransactionId, new SynthesizedBlock.IndexedTransaction(transaction, apiTransaction.Value.Index));
				}
				foreach(KeyValuePair<string, byte[]> apiTransaction in synthesizedBlockApi.ConfirmedTransactions) {
					
					ITransaction transaction = Rehydrate(apiTransaction.Value);
					synthesizedBlock.ConfirmedTransactions.Add(transaction.TransactionId, transaction);
				}

				foreach(var apiRejectedTransaction in synthesizedBlockApi.RejectedTransactions) {

					if(TransactionId.IsValid(apiRejectedTransaction.Key)) {
						var transactionId = new TransactionId(apiRejectedTransaction.Key);
						if (hasAccount)
						{
							if (transactionId.Account == accountId)
							{
								synthesizedBlockAccountSet.RejectedTransactions.Add(new RejectedTransaction(transactionId, apiRejectedTransaction.Value));
							}
						}
					}
				}

				if (hasAccount)
				{
					synthesizedBlock.AccountScoped.Add(accountId, synthesizedBlockAccountSet);
					synthesizedBlock.Accounts.Add(accountId);
				}
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
				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		protected virtual void DisposeAll() {

			this.centralCoordinator.ShutdownRequested -= this.CentralCoordinatorOnShutdownRequested;

			this.WalletFileInfo?.Dispose();
		}
		
		~WalletProvider() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}

}