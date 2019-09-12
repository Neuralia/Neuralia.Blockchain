using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
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
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
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
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public static class WalletProvider {
		public enum HashTypes {
			Sha2,
			Sha3
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

	public interface IUtilityWalletProvider {
		bool IsWalletLoaded { get; }
		string GetChainDirectoryPath();
		string GetChainStorageFilesPath();
		string GetSystemFilesDirectoryPath();
		SynthesizedBlockAPI DeserializeSynthesizedBlockAPI(string synthesizedBlock);
		SynthesizedBlock ConvertApiSynthesizedBlock(SynthesizedBlockAPI synthesizedBlockApi);

		void EnsureWalletIsLoaded();

		void RemovePIDLock();

		void HashKey(IWalletKey key);

		T CreateBasicKey<T>(string name, Enums.KeyTypes keyType)
			where T : IWalletKey;

		IWalletKey CreateBasicKey(string name, Enums.KeyTypes keyType);
		IXmssWalletKey CreateXmssKey(string name);
		IXmssWalletKey CreateXmssKey(string name, float warningLevel, float changeLevel);
		IXmssWalletKey CreateXmssKey(string name, int treeHeight, int hashBits, WalletProvider.HashTypes HashType, float warningLevel, float changeLevel);
		IXmssMTWalletKey CreateXmssmtKey(string name, float warningLevel, float changeLevel);
		IXmssMTWalletKey CreateXmssmtKey(string name, int treeHeight, int treeLayers, Enums.KeyHashBits hashBits, float warningLevel, float changeLevel);
		IQTeslaWalletKey CreateQTeslaKey(string name, QTESLASecurityCategory.SecurityCategories securityCategory);

		void PrepareQTeslaKey<T>(T key, QTESLASecurityCategory.SecurityCategories securityCategory)
			where T : IQTeslaWalletKey;

		IQTeslaWalletKey CreatePresentationQTeslaKey(string name);
		ISecretWalletKey CreateSuperKey();
		ISecretWalletKey CreateSecretKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null);
		ISecretComboWalletKey CreateSecretComboKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, ISecretWalletKey previousKey = null);
		ISecretDoubleWalletKey CreateSecretDoubleKey(string name, QTESLASecurityCategory.SecurityCategories securityCategorySecret, QTESLASecurityCategory.SecurityCategories securityCategorySecond, ISecretDoubleWalletKey previousKey = null);
	}

	public interface IReadonlyWalletProvider {
		bool IsWalletEncrypted { get; }
		bool IsWalletAccountLoaded { get; }
		bool WalletFileExists { get; }

		/// <summary>
		///     This is the lowest common denominator between the wallet accounts syncing block height
		/// </summary>
		long LowestAccountBlockSyncHeight { get; }

		bool Synced { get; }

		bool WalletContainsAccount(Guid accountUuid);
		List<IWalletAccount> GetWalletSyncableAccounts(long blockId);
		IAccountFileInfo GetAccountFileInfo(Guid accountUuid);
		List<IWalletAccount> GetAccounts();
		List<IWalletAccount> GetAllAccounts();
		Guid GetAccountUuid();
		AccountId GetPublicAccountId();
		AccountId GetAccountUuidHash();

		IWalletAccount GetActiveAccount();
		IWalletAccount GetWalletAccount(Guid id);
		IWalletAccount GetWalletAccount(string name);
		IWalletAccount GetWalletAccount(AccountId accountId);

		List<WalletTransactionHistoryHeaderAPI> APIQueryWalletTransactionHistory(Guid accountUuid);
		WalletTransactionHistoryDetailsAPI APIQueryWalletTransationHistoryDetails(Guid accountUuid, string transactionId);
		List<WalletAccountAPI> APIQueryWalletAccounts();
		WalletAccountDetailsAPI APIQueryWalletAccountDetails(Guid accountUuid);
		TransactionId APIQueryWalletAccountPresentationTransactionId(Guid accountUuid);
		List<TransactionId> GetElectionCacheTransactions(IWalletAccount account);

		SynthesizedBlock ExtractCachedSynthesizedBlock(long blockId);
		List<SynthesizedBlock> GetCachedSynthesizedBlocks(long minimumBlockId);

		IWalletStandardAccountSnapshot CreateNewWalletStandardAccountSnapshot(IWalletAccount account);
		IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshot(IWalletAccount account);

		IWalletStandardAccountSnapshot CreateNewWalletStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot);
		IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot);
		IWalletStandardAccountSnapshot CreateNewWalletStandardAccountSnapshotEntry();
		IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshotEntry();

		IWalletAccountSnapshot GetWalletFileInfoAccountSnapshot(Guid accountUuid);

		IWalletAccountSnapshot GetAccountSnapshot(AccountId accountId);
	}

	public interface IWalletProviderWrite {

		/// <summary>
		///     since this provider is created before the central coordinator is fully created, we must be initialized after.
		/// </summary>
		void Initialize();

		void CacheSynthesizedBlock(SynthesizedBlock synthesizedBlock);
		void CleanSynthesizedBlockCache();
		event Delegates.RequestCopyWalletFileDelegate CopyWalletRequest;
		event Delegates.RequestPassphraseDelegate WalletPassphraseRequest;
		event Delegates.RequestKeyPassphraseDelegate WalletKeyPassphraseRequest;
		event Delegates.RequestCopyKeyFileDelegate WalletCopyKeyFileRequest;

		void CreateNewEmptyWallet(CorrelationContext correlationContext, bool encryptWallet, string passphrase, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet);

		bool AllAccountsHaveSyncStatus(SynthesizedBlock block, WalletAccountChainState.BlockSyncStatuses status);
		bool AllAccountsUpdatedWalletBlock(SynthesizedBlock block);
		bool AllAccountsUpdatedWalletBlock(SynthesizedBlock block, long previousBlockId);
		void UpdateWalletBlock(SynthesizedBlock block);
		void UpdateWalletBlock(SynthesizedBlock block, long previousBlockId);
		void UpdateWalletKeyLogs(SynthesizedBlock block);
		bool AllAccountsWalletKeyLogSet(SynthesizedBlock block);
		bool SetActiveAccount(string name);
		bool SetActiveAccount(Guid accountUuid);

		bool CreateNewCompleteWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, Dictionary<int, string> passphrases, Action<IWalletAccount> accountCreatedCallback = null);
		bool CreateNewCompleteWallet(CorrelationContext correlationContext, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, Dictionary<int, string> passphrases, Action<IWalletAccount> accountCreatedCallback = null);

		void UpdateWalletSnapshotFromDigest(IAccountSnapshotDigestChannelCard accountCard);
		void UpdateWalletSnapshotFromDigest(IStandardAccountSnapshotDigestChannelCard accountCard);
		void UpdateWalletSnapshotFromDigest(IJointAccountSnapshotDigestChannelCard accountCard);

		void UpdateWalletSnapshot(IAccountSnapshot accountSnapshot);
		void UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, Guid AccountUuid);

		/// <summary>
		///     Load the wallet
		/// </summary>
		bool LoadWallet(CorrelationContext correlationContext);

		/// <summary>
		///     Change the wallet encryption
		/// </summary>
		/// <param name="encryptWallet"></param>
		/// <param name="encryptKeys"></param>
		void ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, Dictionary<int, string> passphrases);

		void SaveWallet();

		IWalletAccount CreateNewAccount(string name, bool encryptKeys, bool encryptKeysIndividually, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, bool setactive = false);
		bool CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, Dictionary<int, string> passphrases, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, Action<IWalletAccount> accountCreatedCallback = null);
		bool CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, Dictionary<int, string> passphrases);

		void InsertKeyLogTransactionEntry(IWalletAccount account, TransactionIdExtended transactionId, byte keyOrdinalId);
		void InsertKeyLogBlockEntry(IWalletAccount account, BlockId blockId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex);
		void InsertKeyLogDigestEntry(IWalletAccount account, int digestId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex);

		void InsertKeyLogEntry(IWalletAccount account, string eventId, Enums.BlockchainEventTypes eventType, byte keyOrdinalId, KeyUseIndexSet keyUseIndex);
		void ConfirmKeyLogBlockEntry(IWalletAccount account, BlockId blockId, long confirmationBlockId);
		void ConfirmKeyLogTransactionEntry(IWalletAccount account, TransactionIdExtended transactionId, long confirmationBlockId);

		bool KeyLogTransactionExists(IWalletAccount account, TransactionId transactionId);

		void SetChainStateHeight(Guid accountUuid, long blockId);
		long GetChainStateHeight(Guid accountUuid);
		KeyUseIndexSet GetChainStateLastSyncedKeyHeight(IWalletKey key);
		void UpdateLocalChainStateKeyHeight(IWalletKey key);

		IWalletElectionsHistory InsertElectionsHistoryEntry(SynthesizedBlock.SynthesizedElectionResult electionResult, AccountId electedAccountId);

		void InsertLocalTransactionCacheEntry(ITransactionEnvelope transactionEnvelope);
		IWalletTransactionHistory InsertTransactionHistoryEntry(ITransaction transaction, AccountId targetAccountId, string note);
		void UpdateLocalTransactionCacheEntry(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash);
		IWalletTransactionHistoryFileInfo UpdateLocalTransactionHistoryEntry(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status);
		IWalletTransactionCache GetLocalTransactionCacheEntry(TransactionId transactionId);
		void RemoveLocalTransactionCacheEntry(TransactionId transactionId);
		void CreateElectionCacheWalletFile(IWalletAccount account);
		void DeleteElectionCacheWalletFile(IWalletAccount account);

		void InsertElectionCacheTransactions(List<TransactionId> transactionIds, long blockId, IWalletAccount account);
		void RemoveBlockElection(long blockId, IWalletAccount account);
		void RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactionIds, IWalletAccount account);

		/// <summary>
		///     here we add a new key to the account
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		void AddAccountKey<KEY>(Guid accountUuid, KEY key, Dictionary<int, string> passphrases)
			where KEY : IWalletKey;

		void SetNextKey(Guid accountUuid, IWalletKey nextKey);
		void UpdateNextKey(Guid accountUuid, IWalletKey nextKey);

		void CreateNextXmssKey(Guid accountUuid, string keyName);
		void CreateNextXmssKey(Guid accountUuid, byte ordinal);
		bool IsNextKeySet(Guid accountUuid, string keyName);
		IWalletKey LoadKey(Guid AccountUuid, string keyName);
		IWalletKey LoadKey(Guid AccountUuid, byte ordinal);
		IWalletKey LoadKey(string keyName);
		IWalletKey LoadKey(byte ordinal);

		/// <summary>
		///     Load a key with a custom selector
		/// </summary>
		/// <param name="selector"></param>
		/// <param name="accountUuid"></param>
		/// <param name="name"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		T LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string name)
			where T : class
			where K : class, IWalletKey;

		T LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, byte ordinal)
			where T : class
			where K : class, IWalletKey;

		T LoadKey<T>(Func<T, T> selector, Guid accountUuid, string name)
			where T : class, IWalletKey;

		T LoadKey<T>(Func<T, T> selector, Guid accountUuid, byte ordinal)
			where T : class, IWalletKey;

		T LoadKey<T>(Guid accountUuid, string keyName)
			where T : class, IWalletKey;

		T LoadKey<T>(Guid accountUuid, byte ordinal)
			where T : class, IWalletKey;

		T LoadKey<T>(string keyName)
			where T : class, IWalletKey;

		T LoadKey<T>(byte ordinal)
			where T : class, IWalletKey;

		void UpdateKey(IWalletKey key);

		/// <summary>
		///     Swap the next key7 with the current key. the old key is placed in the key history for archiving
		/// </summary>
		/// <param name="key"></param>
		/// <exception cref="ApplicationException"></exception>
		void SwapNextKey(IWalletKey key, bool storeHistory = true);

		void EnsureWalletLoaded();

		/// <summary>
		///     here we will raise events when we need the passphrases, and external providers can provide us with what we need.
		/// </summary>
		void SetExternalPassphraseHandlers(Delegates.RequestPassphraseDelegate requestPassphraseDelegate, Delegates.RequestKeyPassphraseDelegate requestKeyPassphraseDelegate, Delegates.RequestCopyKeyFileDelegate requestKeyCopyFileDelegate, Delegates.RequestCopyWalletFileDelegate copyWalletDelegate);

		/// <summary>
		///     Set the default passphrase request handling to the console
		/// </summary>
		void SetConsolePassphraseHandlers();

		SecureString RequestWalletPassphraseByConsole(int maxTryCount = 10);
		SecureString RequestKeysPassphraseByConsole(Guid accountUUid, string keyName, int maxTryCount = 10);

		/// <summary>
		///     a utility method to request for the passphrase via the console. This only works in certain situations, not for RPC
		///     calls for sure.
		/// </summary>
		/// <param name="passphraseType"></param>
		/// <returns>the secure string or null if error occured</returns>
		SecureString RequestPassphraseByConsole(string passphraseType = "wallet", int maxTryCount = 10);

		/// <summary>
		///     Here, we sign a message with the
		/// </summary>
		/// <param name="key"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		IByteArray PerformCryptographicSignature(Guid accountUuid, string keyName, IByteArray message);

		IByteArray PerformCryptographicSignature(IWalletKey key, IByteArray message);

		IWalletStandardAccountSnapshot GetStandardAccountSnapshot(AccountId accountId);
		IWalletJointAccountSnapshot GetJointAccountSnapshot(AccountId accountId);

		(string path, string passphrase, string salt, int iterations) BackupWallet();

		void UpdateWalletChainStateSyncStatus(Guid accountUuid, long BlockId, WalletAccountChainState.BlockSyncStatuses blockSyncStatus);

		IByteArray SignTransaction(IByteArray transactionHash, string keyName);
		IByteArray SignTransactionXmss(IByteArray transactionHash, IXmssWalletKey key);
		IByteArray SignTransaction(IByteArray transactionHash, IWalletKey key);

		IByteArray SignMessageXmss(IByteArray messageHash, IXmssWalletKey key);
		IByteArray SignMessageXmss(Guid accountUuid, IByteArray messageHash);
		IByteArray SignMessage(IByteArray messageHash, IWalletKey key);
	}

	public interface IWalletProvider : IWalletProviderWrite, IReadonlyWalletProvider, IUtilityWalletProvider {
	}

	public interface IWalletProviderInternal : IWalletProvider {

		IUserWalletFileInfo WalletFileInfo { get; }
		IUserWallet WalletBase { get; }
		IWalletSerialisationFal SerialisationFal { get; }
		bool TransactionInProgress { get; }
		void RequestCopyWallet(CorrelationContext correlationContext, int attempt);
		void CaptureWalletPassphrase(CorrelationContext correlationContext, int attempt);
		void EnsureKeyFileIsPresent(Guid accountUuid, string keyName, int attempt);
		void EnsureKeyFileIsPresent(Guid accountUuid, byte ordinal, int attempt);
		void ClearWalletPassphrase();
		void CaptureKeyPassphrase(CorrelationContext correlationContext, Guid accountUuid, string keyName, int attempt);
		void PerformWalletTransaction(Action<IWalletProvider, CancellationToken> transactionAction, CancellationToken token, Action<IWalletProvider, Action<IWalletProvider>, CancellationToken> commitWrapper = null, Action<IWalletProvider, Action<IWalletProvider>, CancellationToken> rollbackWrapper = null);
		void PerformWalletTransaction(Func<IWalletProvider, CancellationToken, Task> transactionAction, CancellationToken token, Action<IWalletProvider, Action<IWalletProvider>, CancellationToken> commitWrapper = null, Action<IWalletProvider, Action<IWalletProvider>, CancellationToken> rollbackWrapper = null);
		void EnsureKeyPassphrase(Guid accountUuid, string keyName, int attempt);
		void EnsureKeyPassphrase(Guid accountUuid, byte ordinal, int attempt);

		void SetKeysPassphrase(Guid accountUuid, string keyname, string passphrase);
		void SetKeysPassphrase(Guid accountUuid, string keyname, SecureString passphrase);
		void SetWalletPassphrase(string passphrase);
		void SetWalletPassphrase(SecureString passphrase);
		void ClearWalletKeyPassphrase(Guid accountUuid, string keyName);

		void EnsureWalletKeyIsReady(Guid accountUuid, string keyname);
		void EnsureWalletFileIsPresent();
		void EnsureWalletPassphrase();
	}

	public abstract class WalletProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWalletProviderInternal
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

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		protected readonly string chainPath;

		protected readonly IFileSystem fileSystem;

		protected readonly IGlobalsService globalsService;

		protected readonly object locker = new object();

		protected readonly BlockchainServiceSet serviceSet;

		/// <summary>
		///     the synthetized blocks to know which transactions concern us
		/// </summary>
		private readonly ConcurrentDictionary<long, SynthesizedBlock> syncBlockCache = new ConcurrentDictionary<long, SynthesizedBlock>();

		protected IWalletSerializationTransactionExtension currentTransaction;

		public WalletProvider(string chainPath, CENTRAL_COORDINATOR centralCoordinator) {
			this.chainPath = chainPath;
			this.centralCoordinator = centralCoordinator;

			this.globalsService = centralCoordinator.BlockchainServiceSet.GlobalsService;

			this.fileSystem = centralCoordinator.FileSystem;

			this.serviceSet = centralCoordinator.BlockchainServiceSet;
		}

		protected abstract ICardUtils CardUtils { get; }

		public IUserWallet WalletBase => this.WalletFileInfo.WalletBase;

		public IWalletSerialisationFal SerialisationFal { get; private set; }

		public IUserWalletFileInfo WalletFileInfo { get; private set; }

		public bool IsWalletLoaded => this.WalletFileInfo?.IsLoaded ?? false;

		public bool IsWalletEncrypted => this.WalletFileInfo.WalletSecurityDetails.EncryptWallet;

		public bool IsWalletAccountLoaded => this.IsWalletLoaded && (this.WalletBase.GetActiveAccount() != null);

		public bool WalletFileExists => this.WalletFileInfo.FileExists;

		public bool TransactionInProgress {
			get {
				lock(this.locker) {
					return this.currentTransaction != null;
				}
			}
		}

		public void PerformWalletTransaction(Action<IWalletProvider, CancellationToken> transactionAction, CancellationToken token, Action<IWalletProvider, Action<IWalletProvider>, CancellationToken> commitWrapper = null, Action<IWalletProvider, Action<IWalletProvider>, CancellationToken> rollbackWrapper = null) {
			if(this.TransactionInProgress) {
				throw new NotReadyForProcessingException();
			}

			token.ThrowIfCancellationRequested();

			try {
				lock(this.locker) {
					this.currentTransaction = this.SerialisationFal.BeginTransaction();
				}

				// let's make sure we catch implicit disposes that we did not call for through disposing
				this.currentTransaction.Disposed += sessionId => {
					if((this.currentTransaction != null) && (this.currentTransaction.SessionId == sessionId)) {
						// ok, thats us, our session is now disposed.
						lock(this.locker) {
							this.currentTransaction = null;

							// reset the files, since the underlying files have probably changed
							Repeater.Repeat(() => {
								this.WalletFileInfo.ReloadFileBytes();
							}, 2);
						}
					}
				};

				token.ThrowIfCancellationRequested();

				transactionAction(this, token);

				token.ThrowIfCancellationRequested();

				this.SaveWallet();

				token.ThrowIfCancellationRequested();

				if(commitWrapper != null) {
					commitWrapper(this, prov => {

						token.ThrowIfCancellationRequested();

						this.currentTransaction.CommitTransaction();
					}, token);
				} else {
					this.currentTransaction.CommitTransaction();
				}
			} catch {
				if(rollbackWrapper != null) {
					rollbackWrapper(this, prov => {
						this.currentTransaction?.RollbackTransaction();
					}, token);
				} else {
					this.currentTransaction?.RollbackTransaction();
				}

				throw;

				// just end here
			} finally {
				lock(this.locker) {
					this.currentTransaction = null;
				}
			}
		}

		public async void PerformWalletTransaction(Func<IWalletProvider, CancellationToken, Task> transactionAction, CancellationToken token, Action<IWalletProvider, Action<IWalletProvider>, CancellationToken> commitWrapper = null, Action<IWalletProvider, Action<IWalletProvider>, CancellationToken> rollbackWrapper = null) {
			if(this.TransactionInProgress) {
				throw new NotReadyForProcessingException();
			}

			token.ThrowIfCancellationRequested();

			try {
				lock(this.locker) {
					this.currentTransaction = this.SerialisationFal.BeginTransaction();
				}

				// let's make sure we catch implicit disposes that we did not call for through disposing
				this.currentTransaction.Disposed += sessionId => {
					if((this.currentTransaction != null) && (this.currentTransaction.SessionId == sessionId)) {
						// ok, thats us, our session is now disposed.

						lock(this.locker) {
							this.currentTransaction = null;

							// reset the files, since the underlying files have probably changed
							Repeater.Repeat(() => {
								this.WalletFileInfo.ReloadFileBytes();
							}, 2);
						}
					}
				};

				token.ThrowIfCancellationRequested();

				await transactionAction(this, token);

				token.ThrowIfCancellationRequested();

				this.SaveWallet();

				token.ThrowIfCancellationRequested();

				if(commitWrapper != null) {
					commitWrapper(this, prov => {
						token.ThrowIfCancellationRequested();

						this.currentTransaction.CommitTransaction();
					}, token);
				} else {
					this.currentTransaction.CommitTransaction();
				}
			} catch {
				if(rollbackWrapper != null) {
					rollbackWrapper(this, prov => {
						this.currentTransaction?.RollbackTransaction();
					}, token);
				} else {
					this.currentTransaction?.RollbackTransaction();
				}

				// just end here
			} finally {
				lock(this.locker) {
					this.currentTransaction = null;
				}
			}

		}

		/// <summary>
		///     This is the lowest common denominator between the wallet accounts syncing block height
		/// </summary>
		public long LowestAccountBlockSyncHeight {

			get { return this.WalletFileInfo.Accounts.Any() ? this.WalletFileInfo.Accounts.Values.Min(a => a.WalletChainStatesInfo.ChainState.LastBlockSynced) : 0; }
		}

		public bool Synced => this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight == this.LowestAccountBlockSyncHeight;

		/// <summary>
		///     since this provider is created before the central coordinator is fully created, we must be initialized after.
		/// </summary>
		public virtual void Initialize() {
			this.SerialisationFal = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainDalCreationFactoryBase.CreateWalletSerialisationFal(this.centralCoordinator, this.GetChainDirectoryPath(), this.fileSystem);

			this.WalletFileInfo = this.SerialisationFal.CreateWalletFileInfo();

			//this.WalletFileInfo.WalletSecurityDetails.EncryptWallet = this.encryptWallet;
			//this.WalletFileInfo.WalletSecurityDetails.EncryptWalletKeys = this.encryptKeys;
			//this.WalletFileInfo.WalletSecurityDetails.EncryptWalletKeysIndividually = this.;

			// decide by which method we will request the wallet passphrases
			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().PassphraseCaptureMethod == AppSettingsBase.PassphraseQueryMethod.Console) {
				this.SetConsolePassphraseHandlers();
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

		public bool WalletContainsAccount(Guid accountUuid) {

			this.EnsureWalletIsLoaded();

			return this.WalletBase.Accounts.ContainsKey(accountUuid);

		}

		public IAccountFileInfo GetAccountFileInfo(Guid accountUuid) {
			this.EnsureWalletIsLoaded();

			return this.WalletFileInfo.Accounts[accountUuid];

		}

		/// <summary>
		///     gets the list of accounts that can be synced since they match the provided block idx
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public List<IWalletAccount> GetWalletSyncableAccounts(long blockId) {
			this.EnsureWalletIsLoaded();
			var keys = this.WalletFileInfo.Accounts.Where(a => a.Value.WalletChainStatesInfo?.ChainState.LastBlockSynced == blockId).Select(a => a.Key).ToList();

			return this.GetAccounts().Where(a => keys.Contains(a.AccountUuid)).ToList();

		}

		/// <summary>
		///     get all accounts, including rejected ones
		/// </summary>
		/// <returns></returns>
		public List<IWalletAccount> GetAllAccounts() {
			this.EnsureWalletIsLoaded();

			return this.WalletBase.GetAccounts();

		}

		/// <summary>
		///     get all active acounts
		/// </summary>
		/// <returns></returns>
		public List<IWalletAccount> GetAccounts() {
			return this.GetAllAccounts().Where(a => a.Status != Enums.PublicationStatus.Rejected).ToList();

		}

		public Guid GetAccountUuid() {
			return this.GetActiveAccount().AccountUuid;
		}

		public AccountId GetPublicAccountId() {
			return this.GetActiveAccount().PublicAccountId;
		}

		public AccountId GetAccountUuidHash() {
			return this.GetActiveAccount().AccountUuidHash;
		}

		/// <summary>
		///     Return the base wallet directory, not scopped by chain
		/// </summary>
		/// <returns></returns>
		public string GetSystemFilesDirectoryPath() {

			return GlobalsService.GetGeneralSystemFilesDirectoryPath();
		}

		public IWalletAccount GetActiveAccount() {
			this.EnsureWalletIsLoaded();

			return this.WalletBase.GetActiveAccount();

		}

		public bool SetActiveAccount(string name) {
			this.EnsureWalletIsLoaded();

			if(this.WalletBase.SetActiveAccount(name)) {
				this.SaveWallet();

				return true;
			}

			return false;

		}

		public bool SetActiveAccount(Guid accountUuid) {
			this.EnsureWalletIsLoaded();

			if(this.WalletBase.SetActiveAccount(accountUuid)) {
				this.SaveWallet();

				return true;
			}

			return false;

		}

		public IWalletAccount GetWalletAccount(Guid id) {
			this.EnsureWalletIsLoaded();

			return this.WalletBase.GetAccount(id);

		}

		public IWalletAccount GetWalletAccount(string name) {
			this.EnsureWalletIsLoaded();

			return this.WalletBase.GetAccount(name);

		}

		public IWalletAccount GetWalletAccount(AccountId accountId) {
			this.EnsureWalletIsLoaded();

			return this.WalletBase.GetAccount(accountId);

		}

		public virtual bool CreateNewCompleteWallet(CorrelationContext correlationContext, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, Dictionary<int, string> passphrases, Action<IWalletAccount> accountCreatedCallback = null) {
			return this.CreateNewCompleteWallet(correlationContext, "", encryptWallet, encryptKey, encryptKeysIndividually, passphrases, accountCreatedCallback);
		}

		public IWalletStandardAccountSnapshot GetStandardAccountSnapshot(AccountId accountId) {
			return this.GetAccountSnapshot(accountId) as IWalletStandardAccountSnapshot;
		}

		public IWalletJointAccountSnapshot GetJointAccountSnapshot(AccountId accountId) {
			return this.GetAccountSnapshot(accountId) as IWalletJointAccountSnapshot;
		}

		public void UpdateWalletSnapshot(IAccountSnapshot accountSnapshot) {
			this.EnsureWalletIsLoaded();

			IWalletAccount localAccount = this.GetWalletAccount(accountSnapshot.AccountId.ToAccountId());

			if(localAccount == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			this.UpdateWalletSnapshot(accountSnapshot, localAccount.AccountUuid);
		}

		public void UpdateWalletSnapshot(IAccountSnapshot accountSnapshot, Guid AccountUuid) {
			this.EnsureWalletIsLoaded();
			IAccountFileInfo walletAccountInfo = null;

			if(this.WalletFileInfo.Accounts.ContainsKey(AccountUuid)) {
				walletAccountInfo = this.WalletFileInfo.Accounts[AccountUuid];
			}

			if(walletAccountInfo?.WalletSnapshotInfo.WalletAccountSnapshot == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			this.CardUtils.Copy(accountSnapshot, walletAccountInfo.WalletSnapshotInfo.WalletAccountSnapshot);

			walletAccountInfo.WalletSnapshotInfo.Save();
		}

		public void UpdateWalletSnapshotFromDigest(IAccountSnapshotDigestChannelCard accountCard) {
			if(accountCard is IStandardAccountSnapshotDigestChannelCard simpleAccountSnapshot) {
				this.UpdateWalletSnapshotFromDigest(simpleAccountSnapshot);
			} else if(accountCard is IJointAccountSnapshotDigestChannelCard jointAccountSnapshot) {
				this.UpdateWalletSnapshotFromDigest(jointAccountSnapshot);
			} else {
				throw new InvalidCastException();
			}
		}

		/// <summary>
		///     ok, we update our wallet snapshot from the digest
		/// </summary>
		/// <param name="accountCard"></param>
		public void UpdateWalletSnapshotFromDigest(IStandardAccountSnapshotDigestChannelCard accountCard) {
			this.EnsureWalletIsLoaded();

			IWalletAccount localAccount = this.GetWalletAccount(accountCard.AccountId.ToAccountId());

			if(localAccount == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			IAccountFileInfo walletAccountInfo = null;

			if(!this.WalletFileInfo.Accounts.ContainsKey(localAccount.AccountUuid)) {
				this.CreateNewWalletStandardAccountSnapshot(localAccount);
			}

			walletAccountInfo = this.WalletFileInfo.Accounts[localAccount.AccountUuid];

			if(walletAccountInfo?.WalletSnapshotInfo.WalletAccountSnapshot == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			this.CardUtils.Copy(accountCard, walletAccountInfo.WalletSnapshotInfo.WalletAccountSnapshot);

			walletAccountInfo.WalletSnapshotInfo.Save();
		}

		/// <summary>
		///     ok, we update our wallet snapshot from the digest
		/// </summary>
		/// <param name="accountCard"></param>
		public void UpdateWalletSnapshotFromDigest(IJointAccountSnapshotDigestChannelCard accountCard) {
			this.EnsureWalletIsLoaded();

			IWalletAccount localAccount = this.GetAccounts().SingleOrDefault(a => a.GetAccountId() == accountCard.AccountId.ToAccountId());

			if(localAccount == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			IAccountFileInfo walletAccountInfo = null;

			if(!this.WalletFileInfo.Accounts.ContainsKey(localAccount.AccountUuid)) {
				this.CreateNewWalletJointAccountSnapshot(localAccount);
			}

			walletAccountInfo = this.WalletFileInfo.Accounts[localAccount.AccountUuid];

			if(walletAccountInfo?.WalletSnapshotInfo.WalletAccountSnapshot == null) {
				throw new ApplicationException("Account snapshot does not exist");
			}

			this.CardUtils.Copy(accountCard, walletAccountInfo.WalletSnapshotInfo.WalletAccountSnapshot);

			walletAccountInfo.WalletSnapshotInfo.Save();
		}

		public void UpdateWalletChainStateSyncStatus(Guid accountUuid, long BlockId, WalletAccountChainState.BlockSyncStatuses blockSyncStatus) {
			this.EnsureWalletIsLoaded();
			this.WalletFileInfo.Accounts[accountUuid].WalletChainStatesInfo.ChainState.LastBlockSynced = BlockId;
			this.WalletFileInfo.Accounts[accountUuid].WalletChainStatesInfo.ChainState.BlockSyncStatus = (int) blockSyncStatus;
		}

		public virtual bool CreateNewCompleteWallet(CorrelationContext correlationContext, string accountName, bool encryptWallet, bool encryptKey, bool encryptKeysIndividually, Dictionary<int, string> passphrases, Action<IWalletAccount> accountCreatedCallback = null) {

			SystemEventGenerator.WalletCreationStepSet walletCreationStepSet = new SystemEventGenerator.WalletCreationStepSet();

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.WalletCreationStartedEvent(), correlationContext);
			Log.Information("Creating a new wallet");

			string walletPassphrase = null;

			if(passphrases?.ContainsKey(0) ?? false) {
				walletPassphrase = passphrases[0];
			}

			this.CreateNewEmptyWallet(correlationContext, encryptWallet, walletPassphrase, walletCreationStepSet);

			this.CreateNewCompleteAccount(correlationContext, accountName, encryptKey, encryptKeysIndividually, passphrases, walletCreationStepSet, accountCreatedCallback);

			Log.Information("WalletBase successfully created and loaded");
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.WalletCreationEndedEvent(), correlationContext);

			return true;

		}

		public virtual bool CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, Dictionary<int, string> passphrases, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, Action<IWalletAccount> accountCreatedCallback = null) {

			SystemEventGenerator.AccountCreationStepSet accountCreationStepSet = new SystemEventGenerator.AccountCreationStepSet();
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.AccountCreationStartedEvent(), correlationContext);
			this.centralCoordinator.PostSystemEvent(walletCreationStepSet?.AccountCreationStartedStep, correlationContext);

			IWalletAccount account = this.CreateNewAccount(accountName, encryptKeys, encryptKeysIndividually, correlationContext, walletCreationStepSet, accountCreationStepSet, true);

			Log.Information($"Your new default account Uuid is '{account.AccountUuid}'");

			accountCreatedCallback?.Invoke(account);

			// now create the keys
			this.CreateStandardAccountKeys(account.AccountUuid, passphrases, correlationContext, walletCreationStepSet, accountCreationStepSet);

			this.centralCoordinator.PostSystemEvent(walletCreationStepSet?.AccountCreationEndedStep, correlationContext);
			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.AccountCreationEndedEvent(account.AccountUuid), correlationContext);

			return true;
		}

		public virtual bool CreateNewCompleteAccount(CorrelationContext correlationContext, string accountName, bool encryptKeys, bool encryptKeysIndividually, Dictionary<int, string> passphrases) {

			return this.CreateNewCompleteAccount(correlationContext, accountName, encryptKeys, encryptKeysIndividually, passphrases, null);
		}

		/// <summary>
		///     This method will create a brand new empty wallet
		/// </summary>
		/// <param name="encrypt"></param>
		/// <exception cref="ApplicationException"></exception>
		public void CreateNewEmptyWallet(CorrelationContext correlationContext, bool encryptWallet, string passphrase, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet) {
			if(this.IsWalletLoaded) {
				throw new ApplicationException("WalletBase is already created");
			}

			if(this.WalletFileExists) {
				throw new ApplicationException("A wallet file already exists. we can not overwrite an existing file. delete it and try again");
			}

			this.WalletFileInfo.WalletSecurityDetails.EncryptWallet = encryptWallet;

			if(encryptWallet) {
				if(string.IsNullOrWhiteSpace(passphrase)) {
					throw new InvalidOperationException();
				}

				this.SetWalletPassphrase(passphrase);
			}

			IUserWallet wallet = this.CreateNewWalletEntry();

			// set the wallet version

			wallet.Major = GlobalSettings.SoftwareVersion.Major;
			wallet.Minor = GlobalSettings.SoftwareVersion.Minor;
			wallet.Revision = GlobalSettings.SoftwareVersion.Revision;

			wallet.NetworkId = GlobalSettings.Instance.NetworkId;
			wallet.ChainId = this.centralCoordinator.ChainId.Value;

			this.WalletFileInfo.CreateEmptyFileBase(wallet);
		}

		public (string path, string passphrase, string salt, int iterations) BackupWallet() {

			// first, let's generate a passphrase

			this.EnsureWalletFileIsPresent();

			string passphrase = string.Join(" ", WordsGenerator.GenerateRandomWords(4));

			(string path, string salt, int iterations) results = this.SerialisationFal.BackupWallet(passphrase);

			return (results.path, passphrase, results.salt, results.iterations);
		}

		/// <summary>
		///     Load the wallet
		/// </summary>
		public bool LoadWallet(CorrelationContext correlationContext) {
			if(this.IsWalletLoaded) {
				Log.Warning("WalletBase already loaded");

				return false;
			}

			Log.Warning("Ensuring PID protection");

			this.EnsurePIDLock();

			Log.Warning("Loading wallet");

			if(!this.WalletFileInfo.FileExists) {
				Log.Warning("Failed to load wallet, no wallet file exists");

				return false;
			}

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.WalletLoadingStartedEvent(), correlationContext);

			this.WalletFileInfo.LoadFileSecurityDetails();

			try {

				this.EnsureWalletFileIsPresent();
				this.EnsureWalletPassphrase();

				this.WalletFileInfo.Load();

				if(this.WalletFileInfo.WalletBase == null) {
					throw new ApplicationException("The wallet is corrupt. please recreate or fix.");
				}

				if(this.WalletFileInfo.WalletBase.ChainId != this.centralCoordinator.ChainId) {

					throw new ApplicationException("The wallet was created for a different blockchain");
				}

				if(this.WalletFileInfo.WalletBase.NetworkId != GlobalSettings.Instance.NetworkId) {

					throw new ApplicationException("The wallet was created for a different network Id. Can not be used");
				}

				// now restore the skeleton of the unloaded file infos for each accounts
				foreach(IWalletAccount account in this.WalletFileInfo.WalletBase.GetAccounts()) {

					this.WalletFileInfo.Accounts.Add(account.AccountUuid, this.CreateNewAccountFileInfo(account));
				}

			} catch(FileNotFoundException e) {

				this.WalletFileInfo.Reset();
				this.WalletFileInfo = null;

				Log.Warning("Failed to load wallet, no wallet file exists");

				// for a missing file, we simply return false, so we can create it
				return false;
			} catch(Exception e) {

				this.WalletFileInfo.Reset();
				this.WalletFileInfo = null;

				Log.Error(e, "Failed to load wallet");

				throw;
			}

			Log.Warning("Wallet successfully loaded");

			this.centralCoordinator.PostSystemEvent(SystemEventGenerator.WalletLoadingEndedEvent(), correlationContext);

			return true;

		}

		public void RemovePIDLock() {
			try {
				string pidfile = this.GetPIDFilePath();

				if(this.fileSystem.File.Exists(pidfile)) {

					this.fileSystem.File.Delete(pidfile);
				}
			} catch {
				// do nothing
			}
		}

		/// <summary>
		///     Change the wallet encryption
		/// </summary>
		/// <param name="encryptWallet"></param>
		/// <param name="encryptKeys"></param>
		public void ChangeWalletEncryption(CorrelationContext correlationContext, bool encryptWallet, bool encryptKeys, bool encryptKeysIndividually, Dictionary<int, string> passphrases) {

			this.WalletFileInfo.LoadFileSecurityDetails();

			bool walletEncryptionChange = this.WalletFileInfo.WalletSecurityDetails.EncryptWallet != encryptWallet;

			try {
				if(encryptWallet && walletEncryptionChange) {
					this.EnsureWalletFileIsPresent();
					this.EnsureWalletPassphrase();
				}

				var chaningAccounts = new List<IWalletAccount>();

				foreach(IWalletAccount account in this.WalletBase.Accounts.Values) {

					AccountPassphraseDetails accountSecurityDetails = this.WalletFileInfo.Accounts[account.AccountUuid].AccountSecurityDetails;
					bool keysEncryptionChange = accountSecurityDetails.EncryptWalletKeys != encryptKeys;

					if(keysEncryptionChange) {

						chaningAccounts.Add(account);

						// ensure key files are present
						foreach(KeyInfo keyInfo in account.Keys) {
							this.EnsureKeyFileIsPresent(account.AccountUuid, keyInfo, 1);
						}
					}
				}

				if(!walletEncryptionChange && !chaningAccounts.Any()) {
					Log.Information("No encryption changes for the wallet. Nothing to do.");

					return;
				}

				// load the complete structures that will be changed
				this.WalletFileInfo.LoadComplete(walletEncryptionChange, chaningAccounts.Any());

				if(walletEncryptionChange) {
					this.WalletFileInfo.WalletSecurityDetails.EncryptWallet = encryptWallet;

					// now we ensure accounts are set up correctly
					foreach(IWalletAccount account in this.WalletBase.Accounts.Values) {
						if(this.WalletFileInfo.WalletSecurityDetails.EncryptWallet) {

							account.InitializeNewEncryptionParameters(this.centralCoordinator.BlockchainServiceSet);

						} else {

							account.ClearEncryptionParameters();

						}
					}

					this.WalletFileInfo.ChangeEncryption();
				}

				// now we ensure accounts are set up correctly
				foreach(IWalletAccount account in chaningAccounts) {

					AccountPassphraseDetails accountSecurityDetails = this.WalletFileInfo.Accounts[account.AccountUuid].AccountSecurityDetails;

					accountSecurityDetails.EncryptWalletKeys = encryptKeys;
					accountSecurityDetails.EncryptWalletKeysIndividually = encryptKeysIndividually;

					foreach(KeyInfo keyInfo in account.Keys) {
						keyInfo.EncryptionParameters = null;

						if(accountSecurityDetails.EncryptWalletKeys) {

							keyInfo.EncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(GlobalSettings.ApplicationSettings);
						}
					}
				}

				this.WalletFileInfo.ChangeKeysEncryption();

			} catch(Exception e) {
				Log.Verbose("error occured", e);

				//TODO: what to do here?
				throw;
			}

		}

		public void SaveWallet() {
			this.EnsureWalletIsLoaded();
			this.EnsureWalletPassphrase();

			this.WalletFileInfo.Save();

		}

		/// <summary>
		///     Create a new account and keys
		/// </summary>
		public virtual IWalletAccount CreateNewAccount(string name, bool encryptKeys, bool encryptKeysIndividually, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet, bool setactive = false) {

			this.EnsureWalletIsLoaded();

			if(this.WalletFileInfo.WalletBase.GetAccount(name) != null) {
				throw new ApplicationException("Account with name already exists");
			}

			IWalletAccount account = this.CreateNewWalletAccountEntry();

			if(string.IsNullOrEmpty(name)) {
				name = UserWallet.DEFAULT_ACCOUNT;
			}

			this.centralCoordinator.PostSystemEvent(walletCreationStepSet?.CreatingFiles, correlationContext);
			this.centralCoordinator.PostSystemEvent(accountCreationStepSet?.CreatingFiles, correlationContext);

			account.InitializeNew(name, this.centralCoordinator.BlockchainServiceSet, Enums.AccountTypes.Standard);

			account.KeysEncrypted = encryptKeys;
			account.KeysEncryptedIndividually = encryptKeysIndividually;

			if(this.WalletFileInfo.WalletSecurityDetails.EncryptWallet) {
				// generate encryption parameters
				account.InitializeNewEncryptionParameters(this.centralCoordinator.BlockchainServiceSet);
			}

			// make it active
			if(setactive || (this.WalletFileInfo.WalletBase.Accounts.Count == 0)) {
				this.WalletFileInfo.WalletBase.ActiveAccount = account.AccountUuid;
			}

			this.WalletFileInfo.WalletBase.Accounts.Add(account.AccountUuid, account);

			// ensure the key holder is created
			IAccountFileInfo accountFileInfo = this.CreateNewAccountFileInfo(account);

			// now create the file connection entry to map the new account
			this.WalletFileInfo.Accounts.Add(account.AccountUuid, accountFileInfo);

			// and now create the keylog
			accountFileInfo.WalletKeyLogsInfo.CreateEmptyFile();

			// and now create the key history
			accountFileInfo.WalletKeyHistoryInfo.CreateEmptyFile();

			// and now create the chainState

			WalletAccountChainState chainState = this.CreateNewWalletAccountChainStateEntry();
			chainState.AccountUuid = account.AccountUuid;

			// its a brand new account, there is nothing to sync until right now.
			chainState.LastBlockSynced = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;

			accountFileInfo.WalletChainStatesInfo.CreateEmptyFile(chainState);

			this.PrepareAccountInfos(accountFileInfo);

			this.centralCoordinator.PostSystemEvent(walletCreationStepSet?.SavingWallet, correlationContext);

			this.SaveWallet();

			return account;
		}

		public IWalletStandardAccountSnapshot CreateNewWalletStandardAccountSnapshot(IWalletAccount account) {

			return this.CreateNewWalletStandardAccountSnapshot(account, this.CreateNewWalletStandardAccountSnapshotEntry());
		}

		public IWalletStandardAccountSnapshot CreateNewWalletStandardAccountSnapshot(IWalletAccount account, IWalletStandardAccountSnapshot accountSnapshot) {
			this.EnsureWalletIsLoaded();

			if(!this.WalletFileInfo.WalletBase.Accounts.ContainsKey(account.AccountUuid)) {
				//TODO: what to do here?
				throw new ApplicationException("Newly confirmed account is not in the wallet");
			}

			// lets fill the data from our wallet
			this.FillStandardAccountSnapshot(account, accountSnapshot);

			IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid];
			accountFileInfo.WalletSnapshotInfo.InsertNewSnapshotBase(accountSnapshot);

			accountFileInfo.WalletSnapshotInfo.Save();

			return accountSnapshot;
		}

		public IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshot(IWalletAccount account) {

			return this.CreateNewWalletJointAccountSnapshot(account, this.CreateNewWalletJointAccountSnapshotEntry());
		}

		public IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot) {
			this.EnsureWalletIsLoaded();

			if(!this.WalletFileInfo.WalletBase.Accounts.ContainsKey(account.AccountUuid)) {
				//TODO: what to do here?
				throw new ApplicationException("Newly confirmed account is not in the wallet");
			}

			// lets fill the data from our wallet
			this.FillJointAccountSnapshot(account, accountSnapshot);

			IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid];
			accountFileInfo.WalletSnapshotInfo.InsertNewJointSnapshotBase(accountSnapshot);

			accountFileInfo.WalletSnapshotInfo.Save();

			return accountSnapshot;
		}

		public void InsertKeyLogTransactionEntry(IWalletAccount account, TransactionIdExtended transactionId, byte keyOrdinalId) {
			this.InsertKeyLogEntry(account, transactionId.ToEssentialString(), Enums.BlockchainEventTypes.Transaction, keyOrdinalId, transactionId.KeyUseIndex);
		}

		public void InsertKeyLogBlockEntry(IWalletAccount account, BlockId blockId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex) {
			this.InsertKeyLogEntry(account, blockId.ToString(), Enums.BlockchainEventTypes.Block, keyOrdinalId, keyUseIndex);
		}

		public void InsertKeyLogDigestEntry(IWalletAccount account, int digestId, byte keyOrdinalId, KeyUseIndexSet keyUseIndex) {
			this.InsertKeyLogEntry(account, digestId.ToString(), Enums.BlockchainEventTypes.Digest, keyOrdinalId, keyUseIndex);
		}

		public void InsertKeyLogEntry(IWalletAccount account, string eventId, Enums.BlockchainEventTypes eventType, byte keyOrdinalId, KeyUseIndexSet keyUseIndex) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletAccountKeyLog walletAccountKeyLog = this.CreateNewWalletAccountKeyLogEntry();

				walletAccountKeyLog.Timestamp = DateTime.Now;
				walletAccountKeyLog.EventId = eventId;
				walletAccountKeyLog.EventType = (byte) eventType;
				walletAccountKeyLog.KeyOrdinalId = keyOrdinalId;
				walletAccountKeyLog.KeyUseIndex = keyUseIndex;

				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletKeyLogsInfo;

				keyLogFileInfo.InsertKeyLogEntry(walletAccountKeyLog);
			}

		}

		public void ConfirmKeyLogTransactionEntry(IWalletAccount account, TransactionIdExtended transactionId, long confirmationBlockId) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletKeyLogsInfo;
				keyLogFileInfo.ConfirmKeyLogTransactionEntry(transactionId, confirmationBlockId);

			}
		}

		public void ConfirmKeyLogBlockEntry(IWalletAccount account, BlockId blockId, long confirmationBlockId) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletKeyLogsInfo;
				keyLogFileInfo.ConfirmKeyLogBlockEntry(confirmationBlockId);

			}
		}

		public bool KeyLogTransactionExists(IWalletAccount account, TransactionId transactionId) {
			this.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog) {
				WalletKeyLogFileInfo keyLogFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletKeyLogsInfo;

				return keyLogFileInfo.KeyLogTransactionExists(transactionId);

			}

			throw new ApplicationException("Keylog is not enabled.");
		}

		public IWalletKey CreateBasicKey(string name, Enums.KeyTypes keyType) {
			IWalletKey key = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletKey(keyType);

			key.Id = Guid.NewGuid();
			key.Name = name;
			key.CreatedTime = DateTime.Now.Ticks;
			key.EncryptionParameters = AESFileEncryptor.GenerateEncryptionParameters();

			// now we generate a secret key, to encrypt public data about this key
			key.Secret = new byte[333];
			GlobalRandom.GetNextBytes(key.Secret);

			return key;

		}

		public T CreateBasicKey<T>(string name, Enums.KeyTypes keyType)
			where T : IWalletKey {

			T key = (T) this.CreateBasicKey(name, keyType);

			return key;
		}

		public void HashKey(IWalletKey key) {

			// lets generate the hash of this key. this hash can be used as a unique key in public uses. Still, data must be encrypted!

			HashNodeList nodeList = new HashNodeList();

			// lets add three random nonces
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());

			nodeList.Add(key.GetStructuresArray());

			// lets add three random nonces
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());

			key.Hash = HashingUtils.XxhasherTree.HashLong(nodeList);
		}

		public bool AllAccountsWalletKeyLogSet(SynthesizedBlock block) {
			return this.AllAccountsHaveSyncStatus(block, WalletAccountChainState.BlockSyncStatuses.KeyLogSynced);
		}

		public virtual void UpdateWalletKeyLogs(SynthesizedBlock block) {
			this.EnsureWalletIsLoaded();

			bool changed = false;

			// this is where the wallet update happens...
			foreach(IWalletAccount account in this.GetWalletSyncableAccounts(block.BlockId)) {

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid];

				if(this.UpdateWalletKeyLog(accountFileInfo, account, block)) {
					changed = true;
				}
			}

			if(changed) {
				this.SaveWallet();
			}

		}

		public IWalletAccountSnapshot GetWalletFileInfoAccountSnapshot(Guid accountUuid) {
			if(!this.WalletFileInfo.Accounts.ContainsKey(accountUuid)) {
				return null;
			}

			return this.WalletFileInfo.Accounts[accountUuid].WalletSnapshotInfo.WalletAccountSnapshot;
		}

		public bool AllAccountsUpdatedWalletBlock(SynthesizedBlock block) {
			return this.AllAccountsUpdatedWalletBlock(block, block.BlockId - 1);
		}

		public bool AllAccountsUpdatedWalletBlock(SynthesizedBlock block, long previousBlockId) {
			this.EnsureWalletIsLoaded();

			// all accounts have been synced for previous block and if any at current, they have been set for this block
			return !this.GetWalletSyncableAccounts(previousBlockId).Any() && (!this.GetWalletSyncableAccounts(block.BlockId).Any() || this.AllAccountsHaveSyncStatus(block, WalletAccountChainState.BlockSyncStatuses.BlockHeightUpdated));

		}

		public bool AllAccountsHaveSyncStatus(SynthesizedBlock block, WalletAccountChainState.BlockSyncStatuses status) {
			this.EnsureWalletIsLoaded();

			var syncableAccounts = this.GetWalletSyncableAccounts(block.BlockId);

			if(!syncableAccounts.Any()) {
				return false;
			}

			return syncableAccounts.All(a => ((WalletAccountChainState.BlockSyncStatuses) this.WalletFileInfo.Accounts[a.AccountUuid].WalletChainStatesInfo.ChainState.BlockSyncStatus).HasFlag(status));

		}

		/// <summary>
		///     now we have a block we must interpret and update our wallet
		/// </summary>
		/// <param name="block"></param>
		public virtual void UpdateWalletBlock(SynthesizedBlock block) {
			this.UpdateWalletBlock(block, block.BlockId - 1);
		}

		/// <summary>
		///     now we have a block we must interpret and update our wallet
		/// </summary>
		/// <param name="block"></param>
		public virtual void UpdateWalletBlock(SynthesizedBlock block, long previousBlockId) {
			this.EnsureWalletIsLoaded();

			bool changed = false;

			// this is where the wallet update happens...  any previous account that is fully synced can be upgraded now
			var availableAccounts = this.GetWalletSyncableAccounts(previousBlockId).Where(a => ((WalletAccountChainState.BlockSyncStatuses) this.WalletFileInfo.Accounts[a.AccountUuid].WalletChainStatesInfo.ChainState.BlockSyncStatus == WalletAccountChainState.BlockSyncStatuses.FullySynced) || (this.WalletFileInfo.Accounts[a.AccountUuid].WalletChainStatesInfo.ChainState.LastBlockSynced == 0)).ToList();
			availableAccounts.AddRange(this.GetWalletSyncableAccounts(block.BlockId).Where(a => ((WalletAccountChainState.BlockSyncStatuses) this.WalletFileInfo.Accounts[a.AccountUuid].WalletChainStatesInfo.ChainState.BlockSyncStatus == WalletAccountChainState.BlockSyncStatuses.Blank) || (this.WalletFileInfo.Accounts[a.AccountUuid].WalletChainStatesInfo.ChainState.LastBlockSynced == 0)));

			foreach(IWalletAccount account in availableAccounts) {

				AccountId publicAccountId = account.GetAccountId();

				long chainStateHeight = this.GetChainStateHeight(account.AccountUuid);

				this.SetChainStateHeight(account.AccountUuid, block.BlockId);

				if(block.AccountScopped.ContainsKey(publicAccountId)) {
					// get the highest key use in the block for this account

					var transactionIds = block.AccountScopped[publicAccountId].ConfirmedLocalTransactions.Keys.Where(t => t.KeyUseIndex != null).ToList();

					foreach(var group in transactionIds.GroupBy(t => t.KeyUseIndex.Ordinal)) {
						KeyUseIndexSet highestKeyUse = null;

						if(group.Any()) {
							highestKeyUse = group.Max(t => t.KeyUseIndex);
						}

						if(highestKeyUse != null) {
							this.UpdateLocalChainStateTransactionKeyLatestSyncHeight(account.AccountUuid, highestKeyUse, false);
						}
					}
				}

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid];
				accountFileInfo.WalletChainStatesInfo.ChainState.BlockSyncStatus = (int) WalletAccountChainState.BlockSyncStatuses.BlockHeightUpdated;

				changed = true;
			}

			if(changed) {
				this.SaveWallet();
			}

		}

		public void CacheSynthesizedBlock(SynthesizedBlock synthesizedBlock) {
			if(!this.syncBlockCache.ContainsKey(synthesizedBlock.BlockId)) {
				this.syncBlockCache.AddSafe(synthesizedBlock.BlockId, synthesizedBlock);
			}

			this.CleanSynthesizedBlockCache();
		}

		public void CleanSynthesizedBlockCache() {
			foreach(long entry in this.syncBlockCache.Keys.Where(e => e < (this.LowestAccountBlockSyncHeight - 3))) {
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

		/// <summary>
		///     obtain all cached block ids above or equal to the request id
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public List<SynthesizedBlock> GetCachedSynthesizedBlocks(long minimumBlockId) {

			return this.syncBlockCache.Where(e => e.Key >= minimumBlockId).Select(e => e.Value).OrderBy(e => e.BlockId).ToList();

		}

		public IWalletAccountSnapshot GetAccountSnapshot(AccountId accountId) {
			this.EnsureWalletIsLoaded();

			IWalletAccount localAccount = this.GetWalletAccount(accountId);

			if(localAccount == null) {
				return null;
			}

			IAccountFileInfo walletAccountInfo = this.WalletFileInfo.Accounts[localAccount.AccountUuid];

			return walletAccountInfo.WalletSnapshotInfo.WalletAccountSnapshot;
		}

		public string GetPIDFilePath() {
			return Path.Combine(this.GetChainDirectoryPath(), PID_LOCK_FILE);
		}

		private void EnsurePIDLock() {

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType == AppSettingsBase.SerializationTypes.Feeder) {
				// feeders dont need to worry about this
				return;
			}

			string directory = this.GetChainDirectoryPath();

			FileExtensions.EnsureDirectoryStructure(directory, this.fileSystem);

			string pidfile = this.GetPIDFilePath();

			int currentPid = Process.GetCurrentProcess().Id;

			if(this.fileSystem.File.Exists(pidfile)) {
				try {
					IByteArray pidBytes = FileExtensions.ReadAllBytes(pidfile, this.fileSystem);

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
					GlobalsService.AppRemote.Shutdown();

					throw new ApplicationException("Failed to read pid lock file. invalid contents. shutting down.", ex);
				}

				this.fileSystem.File.Delete(pidfile);
			}

			var bytes = new byte[sizeof(int)];
			TypeSerializer.Serialize(currentPid, bytes);

			FileExtensions.WriteAllBytes(pidfile, (ByteArray) bytes, this.fileSystem);
		}

		protected virtual void PrepareAccountInfos(IAccountFileInfo accountFileInfo) {

			// and the wallet snapshot
			accountFileInfo.WalletSnapshotInfo.CreateEmptyFile();

			// and the transaction cache
			accountFileInfo.WalletTransactionCacheInfo.CreateEmptyFile();

			// and the transaction history
			accountFileInfo.WalletTransactionHistoryInfo.CreateEmptyFile();

			// and the elections history
			accountFileInfo.WalletElectionsHistoryInfo.CreateEmptyFile();
		}

		public bool CreateStandardAccountKeys(Guid accountUuid, Dictionary<int, string> passphrases, CorrelationContext correlationContext, SystemEventGenerator.WalletCreationStepSet walletCreationStepSet, SystemEventGenerator.AccountCreationStepSet accountCreationStepSet) {

			this.EnsureWalletIsLoaded();

			IWalletAccount account = this.GetWalletAccount(accountUuid);

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			// create keys

			IXmssWalletKey mainKey = null;
			IXmssWalletKey messageKey = null;
			IXmssWalletKey changeKey = null;
			ISecretWalletKey superKey = null;

			try {

				// the keys are often heavy on the network, lets pause it
				this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PauseNetwork();
				
				this.centralCoordinator.PostSystemEvent(walletCreationStepSet?.CreatingAccountKeys, correlationContext);
				this.centralCoordinator.PostSystemEvent(accountCreationStepSet?.CreatingTransactionKey, correlationContext);

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.TRANSACTION_KEY_NAME, 1, 4}, correlationContext);

				Thread.Sleep(1000);
				mainKey = this.CreateXmssKey(GlobalsService.TRANSACTION_KEY_NAME);

				GC.Collect();
				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.TRANSACTION_KEY_NAME, 1, 4}, correlationContext);

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.MESSAGE_KEY_NAME, 2, 4}, correlationContext);

				this.centralCoordinator.PostSystemEvent(accountCreationStepSet?.CreatingMessageKey, correlationContext);

				Thread.Sleep(1000);
				messageKey = this.CreateXmssKey(GlobalsService.MESSAGE_KEY_NAME);
				
				GC.Collect();

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.MESSAGE_KEY_NAME, 2, 4}, correlationContext);

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.CHANGE_KEY_NAME, 3, 4}, correlationContext);

				this.centralCoordinator.PostSystemEvent(accountCreationStepSet?.CreatingChangeKey, correlationContext);

				Thread.Sleep(1000);
				changeKey = this.CreateXmssKey(GlobalsService.CHANGE_KEY_NAME);
				
				GC.Collect();

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.CHANGE_KEY_NAME, 3, 4}, correlationContext);

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.KeyGenerationStarted, new object[] {GlobalsService.SUPER_KEY_NAME, 4, 4}, correlationContext);

				this.centralCoordinator.PostSystemEvent(accountCreationStepSet?.CreatingSuperKey, correlationContext);

				Thread.Sleep(1000);
				superKey = this.CreateSuperKey();
				
				GC.Collect();

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.KeyGenerationEnded, new object[] {GlobalsService.SUPER_KEY_NAME, 4, 4}, correlationContext);

				this.centralCoordinator.PostSystemEvent(accountCreationStepSet?.KeysCreated, correlationContext);
				this.centralCoordinator.PostSystemEvent(walletCreationStepSet?.AccountKeysCreated, correlationContext);

				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(t => {

					this.AddAccountKey(account.AccountUuid, mainKey, passphrases);
					this.AddAccountKey(account.AccountUuid, messageKey, passphrases);
					this.AddAccountKey(account.AccountUuid, changeKey, passphrases);
					this.AddAccountKey(account.AccountUuid, superKey, passphrases);
				});
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
			accountSnapshot.CorrelationId = account.CorrelationId;
		}

		protected virtual void FillJointAccountSnapshot(IWalletAccount account, IWalletJointAccountSnapshot accountSnapshot) {

			accountSnapshot.AccountId = account.PublicAccountId.ToLongRepresentation();
			accountSnapshot.InceptionBlockId = account.ConfirmationBlockId;
			accountSnapshot.CorrelationId = account.CorrelationId;
		}

		protected virtual IAccountFileInfo CreateNewAccountFileInfo(IWalletAccount account) {
			this.EnsureWalletIsLoaded();

			IAccountFileInfo accountFileInfo = this.CreateNewAccountFileInfo(new AccountPassphraseDetails(account.KeysEncrypted, account.KeysEncryptedIndividually, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout));

			this.CreateNewAccountInfoContents(accountFileInfo, account);

			// now create the keys
			this.AddNewAccountFileInfoKeys(accountFileInfo, account);

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
		protected virtual void AddNewAccountFileInfoKeys(IAccountFileInfo accountFileInfo, IWalletAccount account) {

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
		private (KeyInfo keyInfo, IWalletAccount account) GetKeyInfo(Guid accountUuid, string name) {

			IWalletAccount account = this.GetWalletAccount(accountUuid);

			KeyInfo keyInfo = account.Keys.SingleOrDefault(k => k.Name == name);

			return (keyInfo, account);
		}

		private (KeyInfo keyInfo, IWalletAccount account) GetKeyInfo(Guid accountUuid, byte ordinal) {
			IWalletAccount account = this.GetWalletAccount(accountUuid);

			KeyInfo keyInfo = account.Keys.SingleOrDefault(k => k.Ordinal == ordinal);

			return (keyInfo, account);
		}

		protected virtual bool UpdateWalletKeyLog(IAccountFileInfo accountFile, IWalletAccount account, SynthesizedBlock block) {
			bool changed = false;

			bool keyLogSynced = ((WalletAccountChainState.BlockSyncStatuses) accountFile.WalletChainStatesInfo.ChainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.KeyLogSynced);

			if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().UseKeyLog && !keyLogSynced) {
				AccountId accountId = account.GetAccountId();

				if(block.AccountScopped.ContainsKey(accountId)) {
					SynthesizedBlock.SynthesizedBlockAccountSet scoppedSynthesizedBlock = block.AccountScopped[accountId];

					foreach(var transactionId in scoppedSynthesizedBlock.ConfirmedLocalTransactions) {

						ITransaction transaction = transactionId.Value;

						if(transaction.Version.Type == TransactionTypes.Instance.SIMPLE_PRESENTATION) {
							// the presentation trnasaction is a special case, which we never sign with a ey in our wallet, so we just ignore it
							continue;
						}

						if(!accountFile.WalletKeyLogsInfo.ConfirmKeyLogTransactionEntry(transaction.TransactionId, block.BlockId)) {
							// ok, this transction was not in o8ur key log. this means we might have a bad wallet. this is very serious adn we alert the user
							//TODO: what to do with this?
							throw new ApplicationException($"Block {block.BlockId} has our transaction {transaction} which belongs to us but is NOT in our keylog. We might have an old wallet.");
						}

					}
				}
			}

			if(!keyLogSynced) {
				accountFile.WalletChainStatesInfo.ChainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.KeyLogSynced;
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

		public void EnsureWalletFileIsPresent() {
			if(!this.WalletFileExists) {

				throw new WalletFileMissingException();
			}
		}

		public void EnsureWalletPassphrase() {

			this.WalletFileInfo.LoadFileSecurityDetails();

			if(this.IsWalletEncrypted && !this.WalletFileInfo.WalletSecurityDetails.WalletPassphraseValid) {

				throw new WalletPassphraseMissingException();
			}
		}

		public void RequestCopyWallet(CorrelationContext correlationContext, int attempt) {
			if(!this.WalletFileExists) {
				this.CopyWalletRequest?.Invoke(correlationContext, attempt);
			}
		}

		public void CaptureWalletPassphrase(CorrelationContext correlationContext, int attempt) {

			this.WalletFileInfo.LoadFileSecurityDetails();

			if(this.IsWalletEncrypted && !this.WalletFileInfo.WalletSecurityDetails.WalletPassphraseValid) {

				if(this.WalletPassphraseRequest == null) {
					throw new ApplicationException("No passphrase handling callback provided");
				}

				SecureString passphrase = this.WalletPassphraseRequest(correlationContext, attempt);

				if(passphrase == null) {
					throw new InvalidOperationException("null passphrase provided. Invalid");
				}

				this.SetWalletPassphrase(passphrase);
			}
		}

		public void SetWalletPassphrase(string passphrase) {
			this.WalletFileInfo.WalletSecurityDetails.SetWalletPassphrase(passphrase);

		}

		public void SetWalletPassphrase(SecureString passphrase) {
			this.WalletFileInfo.WalletSecurityDetails.SetWalletPassphrase(passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultWalletPassphraseTimeout);

		}

		public void ClearWalletPassphrase() {

			this.WalletFileInfo.WalletSecurityDetails.ClearWalletPassphrase();
		}

		public void EnsureWalletKeyIsReady(Guid accountUuid, byte ordinal) {

			this.EnsureWalletLoaded();

			if(accountUuid != Guid.Empty) {
				string keyName = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo.Single(k => k.Value.OrdinalId == ordinal).Key;

				this.EnsureWalletKeyIsReady(accountUuid, keyName);
			}
		}

		public void EnsureWalletKeyIsReady(Guid accountUuid, KeyInfo keyInfo) {
			this.EnsureWalletKeyIsReady(accountUuid, keyInfo.Name);
		}

		public void EnsureWalletKeyIsReady(Guid accountUuid, string keyName) {
			this.EnsureKeyFileIsPresent(accountUuid, keyName, 1);
			this.EnsureKeyPassphrase(accountUuid, keyName, 1);
		}

		public void EnsureKeyFileIsPresent(Guid accountUuid, byte ordinal, int attempt) {

			this.EnsureWalletLoaded();

			if(accountUuid != Guid.Empty) {
				string keyName = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo.Single(k => k.Value.OrdinalId == ordinal).Key;

				this.EnsureKeyFileIsPresent(accountUuid, keyName, attempt);
			}
		}

		public void EnsureKeyFileIsPresent(Guid accountUuid, KeyInfo keyInfo, int attempt) {
			this.EnsureKeyFileIsPresent(accountUuid, keyInfo.Name, attempt);
		}

		public void EnsureKeyFileIsPresent(Guid accountUuid, string keyName, int attempt) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				// first, ensure the key is physically present
				if(!walletKeyFileInfo.FileExists) {

					throw new KeyFileMissingException(accountUuid, keyName, attempt);
				}
			}
		}

		public void EnsureKeyPassphrase(Guid accountUuid, byte ordinal, int attempt) {
			if(accountUuid != Guid.Empty) {
				this.EnsureWalletLoaded();
				string keyName = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo.Single(k => k.Value.OrdinalId == ordinal).Key;

				this.EnsureKeyPassphrase(accountUuid, keyName, attempt);
			}
		}

		public void EnsureKeyPassphrase(Guid accountUuid, KeyInfo keyInfo, int attempt) {
			this.EnsureKeyPassphrase(accountUuid, keyInfo.Name, attempt);

		}

		public void EnsureKeyPassphrase(Guid accountUuid, string keyName, int attempt) {
			if(accountUuid != Guid.Empty) {
				this.EnsureWalletIsLoaded();

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				// now the passphrase
				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeys && !accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountUuid, keyName)) {

					throw new KeyPassphraseMissingException(accountUuid, keyName, attempt);
				}
			}
		}

		public void CaptureKeyPassphrase(CorrelationContext correlationContext, Guid accountUuid, KeyInfo keyInfo, int attempt) {
			this.CaptureKeyPassphrase(correlationContext, accountUuid, keyInfo.Name, attempt);

		}

		public void CaptureKeyPassphrase(CorrelationContext correlationContext, Guid accountUuid, string keyName, int attempt) {
			if(accountUuid != Guid.Empty) {
				this.EnsureWalletIsLoaded();

				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				// now the passphrase
				if(accountFileInfo.AccountSecurityDetails.EncryptWalletKeys && !accountFileInfo.AccountSecurityDetails.KeyPassphraseValid(accountUuid, keyName)) {

					if(this.WalletKeyPassphraseRequest == null) {
						throw new ApplicationException("No key passphrase handling callback provided");
					}

					SecureString passphrase = this.WalletKeyPassphraseRequest(correlationContext, accountUuid, keyName, attempt);

					if(passphrase == null) {
						throw new InvalidOperationException("null passphrase provided. Invalid");
					}

					this.SetKeysPassphrase(accountUuid, keyName, passphrase);
				}
			}
		}

		/// <summary>
		///     Apply the same passphrase to all keys
		/// </summary>
		/// <param name="taskStasher"></param>
		/// <param name="accountUuid"></param>
		/// <exception cref="ApplicationException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public void CaptureAllKeysPassphrase(CorrelationContext correlationContext, Guid accountUuid, int attempt) {
			if(accountUuid != Guid.Empty) {
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

				SecureString passphrase = this.WalletKeyPassphraseRequest(correlationContext, accountUuid, "All Keys", attempt);

				if(this.WalletKeyPassphraseRequest == null) {
					throw new ApplicationException("No key passphrase handling callback provided");
				}

				if(passphrase == null) {
					throw new InvalidOperationException("null passphrase provided. Invalid");
				}

				foreach(var walletKeyFileInfo in accountFileInfo.WalletKeysFileInfo) {

					this.SetKeysPassphrase(accountUuid, walletKeyFileInfo.Key, passphrase);
				}
			}
		}

		public void SetKeysPassphrase(Guid accountUuid, string keyname, string passphrase) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountUuid, keyname, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void SetKeysPassphrase(Guid accountUuid, string keyname, SecureString passphrase) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				accountFileInfo.AccountSecurityDetails.SetKeysPassphrase(accountUuid, keyname, passphrase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DefaultKeyPassphraseTimeout);
			}
		}

		public void ClearWalletKeyPassphrase(Guid accountUuid, string keyName) {
			if(accountUuid != Guid.Empty) {
				IAccountFileInfo accountFileInfo = this.WalletFileInfo.Accounts[accountUuid];
				WalletKeyFileInfo walletKeyFileInfo = accountFileInfo.WalletKeysFileInfo[keyName];

				accountFileInfo.AccountSecurityDetails.ClearKeysPassphrase();
			}
		}

	#endregion

	#region Chain State

		public void SetChainStateHeight(Guid accountUuid, long blockId) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = this.WalletBase.GetAccount(accountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = walletChainStateInfo.ChainState;

			if(blockId < chainState.LastBlockSynced) {
				throw new ApplicationException("The new chain state height can not be lower than the existing value");
			}

			if(!GlobalSettings.ApplicationSettings.MobileMode && (blockId != (chainState.LastBlockSynced + 1))) {
				Log.Warning($"The new chain state height ({blockId}) is higher than the next block id for current chain state height ({chainState.LastBlockSynced}).");
			}

			chainState.LastBlockSynced = blockId;

			walletChainStateInfo.Save();

		}

		public long GetChainStateHeight(Guid accountUuid) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = this.WalletBase.GetAccount(accountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = walletChainStateInfo.ChainState;

			return chainState.LastBlockSynced;
		}

		public KeyUseIndexSet GetChainStateLastSyncedKeyHeight(IWalletKey key) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = this.WalletBase.GetAccount(key.AccountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = walletChainStateInfo.ChainState;

			IWalletAccountChainStateKey keyChainState = chainState.Keys[key.KeyAddress.OrdinalId];

			return keyChainState.LatestBlockSyncKeyUse;

		}

		public void UpdateLocalChainStateKeyHeight(IWalletKey key) {
			this.EnsureWalletIsLoaded();
			IWalletAccount account = this.WalletBase.GetAccount(key.AccountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = walletChainStateInfo.ChainState;

			IWalletAccountChainStateKey keyChainState = chainState.Keys[key.KeyAddress.OrdinalId];

			if(key.KeySequenceId < keyChainState.LocalKeyUse?.KeyUseSequenceId.Value) {
				throw new ApplicationException("The key sequence is lower than the one we have in the chain state");
			}

			if(key.KeySequenceId < keyChainState.LatestBlockSyncKeyUse?.KeyUseSequenceId.Value) {
				throw new ApplicationException("The key sequence is lower than the lasy synced block value");
			}

			if(key is IXmssWalletKey xmssWalletKey) {

				if(keyChainState.LocalKeyUse.IsSet && (new KeyUseIndexSet(key.KeySequenceId, xmssWalletKey.KeyUseIndex, key.KeyAddress.OrdinalId) < keyChainState.LocalKeyUse)) {
					throw new ApplicationException("The key sequence is lower than the one we have in the chain state");
				}

				if(keyChainState.LatestBlockSyncKeyUse.IsSet && (new KeyUseIndexSet(key.KeySequenceId, xmssWalletKey.KeyUseIndex, key.KeyAddress.OrdinalId) < keyChainState.LatestBlockSyncKeyUse)) {
					throw new ApplicationException("The key sequence is lower than the lasy synced block value");
				}

				keyChainState.LocalKeyUse.KeyUseIndex = xmssWalletKey.KeyUseIndex;
			}

			keyChainState.LocalKeyUse.KeyUseSequenceId = key.KeySequenceId;

			walletChainStateInfo.Save();

		}

		/// <summary>
		///     update the key chain state with the highest key use we have found in the block.
		/// </summary>
		/// <param name="accountUuid"></param>
		/// <param name="highestKeyUse"></param>
		/// <exception cref="ApplicationException"></exception>
		protected void UpdateLocalChainStateTransactionKeyLatestSyncHeight(Guid accountUuid, KeyUseIndexSet highestKeyUse, bool saveWallet) {

			if(!this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.UseKeyLog) {
				return;
			}

			this.EnsureWalletIsLoaded();
			IWalletAccount account = this.WalletBase.GetAccount(accountUuid);

			WalletChainStateFileInfo walletChainStateInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletChainStatesInfo;

			IWalletAccountChainState chainState = walletChainStateInfo.ChainState;

			IWalletAccountChainStateKey keyChainState = chainState.Keys[highestKeyUse.Ordinal];

			if(keyChainState.LatestBlockSyncKeyUse.IsSet && (highestKeyUse < keyChainState.LatestBlockSyncKeyUse)) {
				throw new ApplicationException("The last synced block transaction key sequence is lower than the value in our wallet. We may have a corrupt wallet and can not use it safely.");
			}

			if(keyChainState.LocalKeyUse.IsSet && (highestKeyUse > keyChainState.LocalKeyUse)) {
				throw new ApplicationException("The last synced block transaction key sequence is higher than the value in our wallet. We may have an out of date wallet and can not use it safely.");
			}

			keyChainState.LatestBlockSyncKeyUse = highestKeyUse;

			if(saveWallet) {
				walletChainStateInfo.Save();
			}

		}

	#endregion

	#region Elections History

		public virtual IWalletElectionsHistory InsertElectionsHistoryEntry(SynthesizedBlock.SynthesizedElectionResult electionResult, AccountId electedAccountId) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != null) && a.PublicAccountId.Equals(electedAccountId));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.AccountUuidHash != null) && a.AccountUuidHash.Equals(electedAccountId));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletElectionsHistory walletElectionsHistory = this.CreateNewWalletElectionsHistoryEntry();

			this.FillWalletElectionsHistoryEntry(walletElectionsHistory, electionResult, electedAccountId);

			IWalletElectionsHistoryFileInfo electionsHistoryInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionsHistoryInfo;

			electionsHistoryInfo.InsertElectionsHistoryEntry(walletElectionsHistory);

			return walletElectionsHistory;

		}

		protected virtual void FillWalletElectionsHistoryEntry(IWalletElectionsHistory walletElectionsHistory, SynthesizedBlock.SynthesizedElectionResult electionResult, AccountId electedAccountId) {

			walletElectionsHistory.BlockId = electionResult.BlockId;
			walletElectionsHistory.Timestamp = electionResult.Timestamp;
			walletElectionsHistory.DelegateAccount = electionResult.ElectedAccounts[electedAccountId].delegateAccountId;
			walletElectionsHistory.PeerShareType = electionResult.ElectedAccounts[electedAccountId].peerShareType;
			walletElectionsHistory.SelectedTransactions = electionResult.ElectedAccounts[electedAccountId].selectedTransactions;
		}

	#endregion

	#region Transaction History

		public virtual IWalletTransactionHistory InsertTransactionHistoryEntry(ITransaction transaction, AccountId targetAccountId, string note) {
			this.EnsureWalletIsLoaded();
			AccountId accountId = targetAccountId;

			if(accountId == null) {
				accountId = transaction.TransactionId.Account;
			}

			IWalletAccount account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != null) && a.PublicAccountId.Equals(accountId));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.AccountUuidHash != null) && a.AccountUuidHash.Equals(accountId));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionHistory walletAccountTransactionHistory = this.CreateNewWalletAccountTransactionHistoryEntry();

			walletAccountTransactionHistory.Local = targetAccountId == transaction.TransactionId.Account;

			this.FillWalletTransactionHistoryEntry(walletAccountTransactionHistory, transaction, accountId, note);

			IWalletTransactionHistoryFileInfo transactionHistoryFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionHistoryInfo;

			transactionHistoryFileInfo.InsertTransactionHistoryEntry(walletAccountTransactionHistory);

			return walletAccountTransactionHistory;

		}

		protected virtual void FillWalletTransactionHistoryEntry(IWalletTransactionHistory walletAccountTransactionHistory, ITransaction transaction, AccountId targetAccountId, string note) {
			walletAccountTransactionHistory.TransactionId = transaction.TransactionId.ToString();
			walletAccountTransactionHistory.Version = transaction.Version.ToString();
			walletAccountTransactionHistory.Contents = JsonUtils.SerializeJsonSerializable(transaction);
			walletAccountTransactionHistory.Recipient = transaction.TransactionId.Account.ToString();

			walletAccountTransactionHistory.Note = note;
			walletAccountTransactionHistory.Timestamp = this.serviceSet.BlockchainTimeService.GetTransactionDateTime(transaction.TransactionId.SimpleTransactionId, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);

			bool ours = transaction.TransactionId.Account == targetAccountId;

			if(ours) {
				// this is ours
				walletAccountTransactionHistory.Status = (byte) WalletTransactionHistory.TransactionStatuses.New;
			} else {
				// incoming transactions are always confirmed
				walletAccountTransactionHistory.Status = (byte) WalletTransactionHistory.TransactionStatuses.Confirmed;
			}
		}

		public virtual IWalletTransactionHistoryFileInfo UpdateLocalTransactionHistoryEntry(TransactionId transactionId, WalletTransactionHistory.TransactionStatuses status) {
			this.EnsureWalletIsLoaded();
			IWalletAccount account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != null) && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.AccountUuidHash != null) && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionHistoryFileInfo transactionHistoryFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionHistoryInfo;

			transactionHistoryFileInfo.UpdateTransactionStatus(transactionId, status);

			return transactionHistoryFileInfo;

		}

	#endregion

	#region Transaction Cache

		public virtual void InsertLocalTransactionCacheEntry(ITransactionEnvelope transactionEnvelope) {
			this.EnsureWalletIsLoaded();
			TransactionId transactionId = transactionEnvelope.Contents.RehydratedTransaction.TransactionId.SimpleTransactionId;

			IWalletAccount account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != null) && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.AccountUuidHash != null) && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionCache walletAccountTransactionCache = this.CreateNewWalletAccountTransactionCacheEntry();

			this.FillWalletTransactionCacheEntry(walletAccountTransactionCache, transactionEnvelope, transactionId.Account);

			IWalletTransactionCacheFileInfo transactionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionCacheInfo;

			transactionCacheFileInfo.InsertTransactionCacheEntry(walletAccountTransactionCache);

		}

		protected virtual void FillWalletTransactionCacheEntry(IWalletTransactionCache walletAccountTransactionCache, ITransactionEnvelope transactionEnvelope, AccountId targetAccountId) {
			walletAccountTransactionCache.TransactionId = transactionEnvelope.Contents.RehydratedTransaction.TransactionId.ToString();
			walletAccountTransactionCache.Version = transactionEnvelope.Contents.RehydratedTransaction.Version.ToString();
			walletAccountTransactionCache.Timestamp = this.serviceSet.BlockchainTimeService.GetTransactionDateTime(transactionEnvelope.Contents.RehydratedTransaction.TransactionId.SimpleTransactionId, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
			walletAccountTransactionCache.Transaction = transactionEnvelope.DehydrateEnvelope();

			walletAccountTransactionCache.Expiration = transactionEnvelope.GetExpirationTime(this.serviceSet.TimeService, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);

			bool ours = transactionEnvelope.Contents.Uuid.Account == targetAccountId;

			if(ours) {
				// this is ours
				walletAccountTransactionCache.Status = (byte) WalletTransactionCache.TransactionStatuses.New;
			} else {
				// incoming transactions are always confirmed
				walletAccountTransactionCache.Status = (byte) WalletTransactionCache.TransactionStatuses.Confirmed;
			}
		}

		public virtual IWalletTransactionCache GetLocalTransactionCacheEntry(TransactionId transactionId) {
			this.EnsureWalletIsLoaded();
			IWalletAccount account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != null) && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.AccountUuidHash != null) && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			return this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionCacheInfo.GetTransactionBase(transactionId);

		}

		public virtual void UpdateLocalTransactionCacheEntry(TransactionId transactionId, WalletTransactionCache.TransactionStatuses status, long gossipMessageHash) {
			this.EnsureWalletIsLoaded();
			IWalletAccount account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != null) && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.AccountUuidHash != null) && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionCacheFileInfo transactionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionCacheInfo;

			transactionCacheFileInfo.UpdateTransaction(transactionId, status, gossipMessageHash);

		}

		public virtual void RemoveLocalTransactionCacheEntry(TransactionId transactionId) {
			this.EnsureWalletIsLoaded();
			IWalletAccount account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.PublicAccountId != null) && a.PublicAccountId.Equals(transactionId.Account));

			if(account == null) {
				// try the hash, if its a presentation transaction
				account = this.WalletFileInfo.WalletBase.Accounts.Values.SingleOrDefault(a => (a.AccountUuidHash != null) && a.AccountUuidHash.Equals(transactionId.Account));

				if(account == null) {
					throw new ApplicationException("No account found for transaction");
				}
			}

			IWalletTransactionCacheFileInfo transactionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletTransactionCacheInfo;

			transactionCacheFileInfo.RemoveTransaction(transactionId);

		}

	#endregion

	#region Election Cache

		public void CreateElectionCacheWalletFile(IWalletAccount account) {
			this.EnsureWalletIsLoaded();
			this.DeleteElectionCacheWalletFile(account);

			IAccountFileInfo walletFileInfoAccount = this.WalletFileInfo.Accounts[account.AccountUuid];

			walletFileInfoAccount.WalletElectionCacheInfo = this.SerialisationFal.CreateWalletElectionCacheFileInfo(account, this.WalletFileInfo.WalletSecurityDetails);

			walletFileInfoAccount.WalletElectionCacheInfo.CreateEmptyFile();

		}

		public void DeleteElectionCacheWalletFile(IWalletAccount account) {
			this.EnsureWalletIsLoaded();
			IAccountFileInfo walletFileInfoAccount = this.WalletFileInfo.Accounts[account.AccountUuid];

			walletFileInfoAccount.WalletElectionCacheInfo?.DeleteFile();

			walletFileInfoAccount.WalletElectionCacheInfo = null;

		}

		public List<TransactionId> GetElectionCacheTransactions(IWalletAccount account) {
			this.EnsureWalletIsLoaded();
			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionCacheInfo;

			return electionCacheFileInfo.GetAllTransactions();

		}

		public void InsertElectionCacheTransactions(List<TransactionId> transactionIds, long blockId, IWalletAccount account) {
			this.EnsureWalletIsLoaded();
			var entries = new List<WalletElectionCache>();

			foreach(TransactionId transactionId in transactionIds) {

				WalletElectionCache entry = this.CreateNewWalletAccountElectionCacheEntry();
				entry.TransactionId = transactionId;
				entry.BlockId = blockId;

				entries.Add(entry);
			}

			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionCacheInfo;

			electionCacheFileInfo.InsertElectionCacheEntries(entries);

		}

		public void RemoveBlockElection(long blockId, IWalletAccount account) {
			this.EnsureWalletIsLoaded();
			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionCacheInfo;

			electionCacheFileInfo.RemoveBlockElection(blockId);

		}

		public void RemoveBlockElectionTransactions(long blockId, List<TransactionId> transactionIds, IWalletAccount account) {
			this.EnsureWalletIsLoaded();
			WalletElectionCacheFileInfo electionCacheFileInfo = this.WalletFileInfo.Accounts[account.AccountUuid].WalletElectionCacheInfo;

			electionCacheFileInfo.RemoveBlockElectionTransactions(blockId, transactionIds);

		}

	#endregion

	#region Keys

		/// <summary>
		///     here we add a new key to the account
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public void AddAccountKey<KEY>(Guid accountUuid, KEY key, Dictionary<int, string> passphrases)
			where KEY : IWalletKey {
			this.EnsureWalletIsLoaded();

			key.AccountUuid = accountUuid;

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = this.GetKeyInfo(accountUuid, key.Name);

			if(keyMeta.keyInfo != null) {
				throw new ApplicationException("Key already exists in account");
			}

			// its a brand new key
			KeyInfo keyInfo = new KeyInfo();

			byte ordinal = 1;

			// find the highest ordinal, and add 1
			if(keyMeta.account.Keys.Count != 0) {
				ordinal = (byte) (ordinal + keyMeta.account.Keys.Max(k => k.Ordinal));
			}

			keyInfo.Name = key.Name;
			key.KeyAddress.OrdinalId = ordinal;
			keyInfo.Ordinal = ordinal;
			key.KeySequenceId = 0;

			// we add this new key
			keyMeta.account.Keys.Add(keyInfo);

			IAccountFileInfo walletAccountFileInfo = this.WalletFileInfo.Accounts[accountUuid];

			if(walletAccountFileInfo.AccountSecurityDetails.EncryptWalletKeys) {
				keyInfo.EncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(GlobalSettings.ApplicationSettings);

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
					this.SetKeysPassphrase(accountUuid, keyInfo.Name, passphrase);
				}

				this.EnsureKeyPassphrase(accountUuid, keyInfo.Name, 1);
			}

			// ensure we create the key file
			walletAccountFileInfo.WalletKeysFileInfo[key.Name].CreateEmptyFile(key);

			// add the key chainstate
			IWalletAccountChainStateKey chainStateKey = this.CreateNewWalletAccountChainStateKeyEntry();
			chainStateKey.Ordinal = key.KeyAddress.OrdinalId;

			KeyUseIndexSet keyUseIndex = new KeyUseIndexSet(key.KeySequenceId, -1, chainStateKey.Ordinal);

			if(key is XmssWalletKey xmssWalletKey) {
				keyUseIndex.KeyUseIndex = xmssWalletKey.KeyUseIndex;
			}

			chainStateKey.LocalKeyUse = keyUseIndex;
			chainStateKey.LatestBlockSyncKeyUse = new KeyUseIndexSet(0, 0, chainStateKey.Ordinal);

			walletAccountFileInfo.WalletChainStatesInfo.ChainState.Keys.Add(chainStateKey.Ordinal, chainStateKey);

			this.SaveWallet();

		}

		/// <summary>
		///     this method can be called to create and set the next XMSS key. This can be useful to pre create large keys as the
		///     next key, save some time at key change time.
		/// </summary>
		/// <param name="accountUuid"></param>
		/// <param name="keyName"></param>
		public void CreateNextXmssKey(Guid accountUuid, string keyName) {
			this.EnsureWalletIsLoaded();
			this.EnsureWalletKeyIsReady(accountUuid, keyName);

			bool nextKeySet = this.IsNextKeySet(accountUuid, keyName);

			using(IXmssWalletKey nextKey = this.CreateXmssKey(keyName)) {

				if(nextKeySet) {
					this.UpdateNextKey(accountUuid, nextKey);
				} else {
					this.SetNextKey(accountUuid, nextKey);
				}

				this.SaveWallet();
			}
		}

		public void CreateNextXmssKey(Guid accountUuid, byte ordinal) {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = this.GetKeyInfo(accountUuid, ordinal);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo);

			this.CreateNextXmssKey(accountUuid, keyMeta.keyInfo.Name);

		}

		/// <summary>
		///     determine if the next key has already been created and set
		/// </summary>
		/// <param name="taskStasher"></param>
		/// <param name="accountUuid"></param>
		/// <param name="ordinal"></param>
		/// <returns></returns>
		public bool IsNextKeySet(Guid accountUuid, string keyName) {

			this.EnsureWalletIsLoaded();
			this.EnsureWalletKeyIsReady(accountUuid, keyName);

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = this.GetKeyInfo(accountUuid, keyName);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			Repeater.Repeat(index => {
				// ensure the key files are present
				this.EnsureKeyFileIsPresent(accountUuid, keyMeta.keyInfo, index);
			});

			bool isNextKeySet = false;

			this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[keyName];

			isNextKeySet = walletKeyInfo.IsNextKeySet;

			return isNextKeySet;

		}

		public void SetNextKey(Guid accountUuid, IWalletKey nextKey) {

			this.EnsureWalletIsLoaded();

			nextKey.AccountUuid = accountUuid;

			nextKey.Status = Enums.KeyStatus.New;

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = this.GetKeyInfo(accountUuid, nextKey.Name);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo);
			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[nextKey.Name];

			walletKeyInfo.SetNextKey(keyMeta.keyInfo, nextKey);
		}

		public void UpdateNextKey(Guid accountUuid, IWalletKey nextKey) {
			nextKey.AccountUuid = accountUuid;

			nextKey.Status = Enums.KeyStatus.New;

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = this.GetKeyInfo(accountUuid, nextKey.Name);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[nextKey.Name];

			walletKeyInfo.UpdateNextKey(keyMeta.keyInfo, nextKey);
		}

		public IWalletKey LoadKey(string keyName) {
			return this.LoadKey<IWalletKey>(this.GetAccountUuid(), keyName);
		}

		public IWalletKey LoadKey(byte ordinal) {
			return this.LoadKey<IWalletKey>(this.GetAccountUuid(), ordinal);
		}

		public IWalletKey LoadKey(Guid AccountUuid, string keyName) {
			return this.LoadKey<IWalletKey>(AccountUuid, keyName);
		}

		public IWalletKey LoadKey(Guid AccountUuid, byte ordinal) {
			return this.LoadKey<IWalletKey>(AccountUuid, ordinal);
		}

		public T LoadKey<T>(string keyName)
			where T : class, IWalletKey {

			return this.LoadKey<T>(this.GetAccountUuid(), keyName);
		}

		public T LoadKey<T>(byte ordinal)
			where T : class, IWalletKey {

			return this.LoadKey<T>(this.GetAccountUuid(), ordinal);
		}

		public T LoadKey<T>(Guid accountUuid, string keyName)
			where T : class, IWalletKey {
			T Selector(T key) {
				return key;
			}

			return this.LoadKey<T>(Selector, accountUuid, keyName);

		}

		public T LoadKey<T>(Guid accountUuid, byte ordinal)
			where T : class, IWalletKey {
			T Selector(T key) {
				return key;
			}

			return this.LoadKey<T>(Selector, accountUuid, ordinal);

		}

		public T LoadKey<T>(Func<T, T> selector, Guid accountUuid, string name)
			where T : class, IWalletKey {
			return this.LoadKey<T, T>(selector, accountUuid, name);
		}

		public T LoadKey<T>(Func<T, T> selector, Guid accountUuid, byte ordinal)
			where T : class, IWalletKey {

			return this.LoadKey<T, T>(selector, accountUuid, ordinal);
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
		public T LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, string name)
			where T : class
			where K : class, IWalletKey {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = this.GetKeyInfo(accountUuid, name);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			// ensure the key files are present
			this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[keyMeta.keyInfo.Name];

			return walletKeyInfo.LoadKey(selector, accountUuid, keyMeta.keyInfo.Name);
		}

		public T LoadKey<K, T>(Func<K, T> selector, Guid accountUuid, byte ordinal)
			where T : class
			where K : class, IWalletKey {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = this.GetKeyInfo(accountUuid, ordinal);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			this.EnsureWalletKeyIsReady(accountUuid, keyMeta.keyInfo);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[accountUuid].WalletKeysFileInfo[keyMeta.keyInfo.Name];

			return walletKeyInfo.LoadKey(selector, accountUuid, keyMeta.keyInfo.Name);
		}

		public void UpdateKey(IWalletKey key) {

			this.EnsureWalletIsLoaded();

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = this.GetKeyInfo(key.AccountUuid, key.Name);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			this.EnsureWalletKeyIsReady(key.AccountUuid, keyMeta.keyInfo);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[key.AccountUuid].WalletKeysFileInfo[key.Name];

			walletKeyInfo.UpdateKey(key);
		}

		/// <summary>
		///     Swap the next key with the current key. the old key is placed in the key history for archiving
		/// </summary>
		/// <param name="key"></param>
		/// <exception cref="ApplicationException"></exception>
		public void SwapNextKey(IWalletKey key, bool storeHistory = true) {

			this.EnsureWalletIsLoaded();

			if(key.NextKey == null) {
				throw new ApplicationException("Cannot swap keys if next key was not created");
			}

			IWalletKey nextKey = key.NextKey;

			(KeyInfo keyInfo, IWalletAccount account) keyMeta = this.GetKeyInfo(key.AccountUuid, key.Name);

			if(keyMeta.keyInfo == null) {
				throw new ApplicationException("Key did not exist. nothing to swap");
			}

			this.EnsureWalletKeyIsReady(key.AccountUuid, keyMeta.keyInfo);

			WalletKeyFileInfo walletKeyInfo = this.WalletFileInfo.Accounts[key.AccountUuid].WalletKeysFileInfo[key.Name];

			if(storeHistory) {
				WalletKeyHistoryFileInfo walletKeyHistoryInfo = this.WalletFileInfo.Accounts[key.AccountUuid].WalletKeyHistoryInfo;

				walletKeyHistoryInfo.InsertKeyHistoryEntry(key, this.CreateNewWalletKeyHistoryEntry());
			}

			walletKeyInfo.SwapNextKey(key);

			// we swapped our key, we must update the chain state
			this.UpdateLocalChainStateKeyHeight(nextKey);

		}

	#endregion

	#region external requests

		// a special method that will block and request the outside world to load the wallet or create a new one
		public void EnsureWalletLoaded() {
			//TODO: this is mostly for dev. remove in prod
			if(!this.IsWalletLoaded) {
				if(this.WalletFileExists) {
					if(this.LoadWallet(default) && this.IsWalletLoaded) {
						return;
					}
				}

				throw new WalletNotLoadedException();
			}

		}

		/// <summary>
		///     here we will raise events when we need the passphrases, and external providers can provide us with what we need.
		/// </summary>
		public void SetExternalPassphraseHandlers(Delegates.RequestPassphraseDelegate requestPassphraseDelegate, Delegates.RequestKeyPassphraseDelegate requestKeyPassphraseDelegate, Delegates.RequestCopyKeyFileDelegate requestKeyCopyFileDelegate, Delegates.RequestCopyWalletFileDelegate copyWalletDelegate) {
			this.WalletPassphraseRequest += requestPassphraseDelegate;

			this.WalletKeyPassphraseRequest += requestKeyPassphraseDelegate;

			this.WalletCopyKeyFileRequest += requestKeyCopyFileDelegate;

			this.CopyWalletRequest += copyWalletDelegate;
		}

	#endregion

	#region console

		/// <summary>
		///     Set the default passphrase request handling to the console
		/// </summary>
		public void SetConsolePassphraseHandlers() {
			this.WalletPassphraseRequest += (correlationContext, attempt) => this.RequestWalletPassphraseByConsole();

			this.WalletKeyPassphraseRequest += (correlationContext, accountUUid, keyName, attempt) => this.RequestKeysPassphraseByConsole(accountUUid, keyName);

			this.WalletCopyKeyFileRequest += (correlationContext, accountUUid, keyName, attempt) => this.RequestKeysCopyFileByConsole(accountUUid, keyName);

			this.CopyWalletRequest += (correlationContext, attempt) => this.RequestCopyWalletByConsole();
		}

		public SecureString RequestWalletPassphraseByConsole(int maxTryCount = 10) {
			return this.RequestPassphraseByConsole("wallet", maxTryCount);
		}

		public SecureString RequestKeysPassphraseByConsole(Guid accountUUid, string keyName, int maxTryCount = 10) {
			return this.RequestPassphraseByConsole($"wallet key (account: {accountUUid}, key name: {keyName})", maxTryCount);
		}

		public void RequestKeysCopyFileByConsole(Guid accountUUid, string keyName, int maxTryCount = 10) {
			Log.Warning($"Wallet key file (account: {accountUUid}, key name: {keyName}) is not present. Please copy it.", maxTryCount);

			Console.ReadKey();
		}

		public void RequestCopyWalletByConsole() {
			Log.Information("Please ensure the wallet file is in the wallets baseFolder");
			Console.ReadKey();
		}

		/// <summary>
		///     a utility method to request for the passphrase via the console. This only works in certain situations, not for RPC
		///     calls for sure.
		/// </summary>
		/// <param name="passphraseType"></param>
		/// <returns>the secure string or null if error occured</returns>
		public SecureString RequestPassphraseByConsole(string passphraseType = "wallet", int maxTryCount = 10) {
			bool valid = false;
			SecureString pass = null;

			int counter = 0;

			do {
				// we must request the passwords by console
				Log.Verbose("");
				Log.Verbose($"Enter your {passphraseType} passphrase (ESC to skip):");
				SecureString temp = this.RequestConsolePassphrase();

				if(temp == null) {
					Log.Verbose("Entry has been skipped.");

					return null;
				}

				Log.Verbose($"Enter your {passphraseType} passphrase again:");
				SecureString pass2 = this.RequestConsolePassphrase();

				valid = temp.SecureStringEqual(pass2);

				if(!valid) {
					Log.Verbose("Passphrases are different.");
				} else {
					// its valid!
					pass = temp;
				}

				counter++;
			} while((valid == false) && (counter < maxTryCount));

			return pass;

		}

		/// <summary>
		///     a simple method to capture a console fairly securely from the console
		/// </summary>
		/// <returns></returns>
		private SecureString RequestConsolePassphrase() {
			SecureString securePwd = new SecureString();
			ConsoleKeyInfo key;

			do {
				key = Console.ReadKey(true);

				if(key.Key == ConsoleKey.Escape) {
					return null;
				}

				// Ignore any key out of range.
				if(((int) key.Key >= 65) && ((int) key.Key <= 90)) {
					// Append the character to the password.
					securePwd.AppendChar(key.KeyChar);
					Console.Write("*");
				}

				// Exit if Enter key is pressed.
			} while((key.Key != ConsoleKey.Enter) || (securePwd.Length == 0));

			Log.Verbose("");

			return securePwd;

		}

	#endregion

	#region entry creation

		protected virtual IUserWallet CreateNewWalletEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewUserWallet();
		}

		protected virtual IWalletAccount CreateNewWalletAccountEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccount();
		}

		protected virtual WalletAccountKeyLog CreateNewWalletAccountKeyLogEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountKeyLog();
		}

		protected virtual IWalletTransactionCache CreateNewWalletAccountTransactionCacheEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountTransactionCache();
		}

		protected virtual IWalletElectionsHistory CreateNewWalletElectionsHistoryEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletElectionsHistoryEntry();
		}

		protected virtual IWalletTransactionHistory CreateNewWalletAccountTransactionHistoryEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountTransactionHistory();
		}

		protected virtual WalletElectionCache CreateNewWalletAccountElectionCacheEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountElectionCache();
		}

		protected virtual WalletAccountChainState CreateNewWalletAccountChainStateEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountChainState();
		}

		protected virtual IWalletAccountChainStateKey CreateNewWalletAccountChainStateKeyEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountChainStateKey();
		}

		protected virtual WalletKeyHistory CreateNewWalletKeyHistoryEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletKeyHistory();
		}

		public virtual IWalletStandardAccountSnapshot CreateNewWalletStandardAccountSnapshotEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountSnapshot();
		}

		public virtual IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshotEntry() {
			return this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletJointAccountSnapshot();
		}

	#endregion

	#region XMSS

		public IXmssWalletKey CreateXmssKey(string name) {
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			int treeHeight = WalletProvider.MINIMAL_XMSS_KEY_HEIGHT;
			float xmssKeyWarningLevel = 0.7F;
			float xmssKeyChangeLevel = 0.9F;
			int keyHashBits = 0;
			WalletProvider.HashTypes hashType = WalletProvider.HashTypes.Sha2;

			if(name == GlobalsService.TRANSACTION_KEY_NAME) {
				treeHeight = Math.Max((int) chainConfiguration.TransactionXmssKeyTreeHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.TransactionXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.TransactionXmssKeyChangeLevel;
				hashType = chainConfiguration.TransactionXmssKeyHashType == ChainConfigurations.HashTypes.Sha2 ? WalletProvider.HashTypes.Sha2 : WalletProvider.HashTypes.Sha3;
				keyHashBits = WalletProvider.TRANSACTION_KEY_HASH_BITS;
			}

			if(name == GlobalsService.MESSAGE_KEY_NAME) {
				treeHeight = Math.Max((int) chainConfiguration.MessageXmssKeyTreeHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.MessageXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.MessageXmssKeyChangeLevel;
				hashType = chainConfiguration.MessageXmssKeyHashType == ChainConfigurations.HashTypes.Sha2 ? WalletProvider.HashTypes.Sha2 : WalletProvider.HashTypes.Sha3;
				keyHashBits = WalletProvider.MESSAGE_KEY_HASH_BITS;
			}

			if(name == GlobalsService.CHANGE_KEY_NAME) {
				treeHeight = Math.Max((int) chainConfiguration.ChangeXmssKeyTreeHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.ChangeXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.ChangeXmssKeyChangeLevel;
				hashType = chainConfiguration.ChangeXmssKeyHashType == ChainConfigurations.HashTypes.Sha2 ? WalletProvider.HashTypes.Sha2 : WalletProvider.HashTypes.Sha3;
				keyHashBits = WalletProvider.CHANGE_KEY_HASH_BITS;
			}

			return this.CreateXmssKey(name, treeHeight, keyHashBits, hashType, xmssKeyWarningLevel, xmssKeyChangeLevel);
		}

		public IXmssWalletKey CreateXmssKey(string name, float warningLevel, float changeLevel) {
			return this.CreateXmssKey(name, XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT, WalletProvider.DEFAULT_KEY_HASH_BITS, WalletProvider.HashTypes.Sha2, warningLevel, changeLevel);
		}

		public IXmssWalletKey CreateXmssKey(string name, int treeHeight, int hashBits, WalletProvider.HashTypes HashType, float warningLevel, float changeLevel) {
			IXmssWalletKey key = this.CreateBasicKey<IXmssWalletKey>(name, Enums.KeyTypes.XMSS);

			Enums.KeyHashBits fullHashbits = Enums.KeyHashBits.SHA2_256;

			if((HashType == WalletProvider.HashTypes.Sha2) && (hashBits == 256)) {
				fullHashbits = Enums.KeyHashBits.SHA2_256;
			} else if((HashType == WalletProvider.HashTypes.Sha2) && (hashBits == 512)) {
				fullHashbits = Enums.KeyHashBits.SHA2_512;
			} else if((HashType == WalletProvider.HashTypes.Sha3) && (hashBits == 256)) {
				fullHashbits = Enums.KeyHashBits.SHA3_256;
			} else if((HashType == WalletProvider.HashTypes.Sha3) && (hashBits == 512)) {
				fullHashbits = Enums.KeyHashBits.SHA3_512;
			}

			using(XMSSProvider provider = new XMSSProvider(fullHashbits, treeHeight)) {

				provider.Initialize();

				Log.Information($"Creating a new XMSS key named '{name}' with tree height {treeHeight} and hashBits {provider.HashBits}");

				(IByteArray privateKey, IByteArray publicKey) keys = provider.GenerateKeys();

				key.HashBits = provider.HashBitsEnum;
				key.TreeHeight = provider.TreeHeight;
				key.KeyType = Enums.KeyTypes.XMSS;
				key.WarningHeight = provider.GetKeyUseThreshold(warningLevel);
				key.ChangeHeight = provider.GetKeyUseThreshold(changeLevel);
				key.MaximumHeight = provider.MaximumHeight;
				key.KeyUseIndex = 0;

				key.PrivateKey = keys.privateKey.ToExactByteArrayCopy();
				key.PublicKey = keys.publicKey.ToExactByteArrayCopy();

				// store a copy just in case
				key.InitialPrivateKey = key.PrivateKey.ToArray();

				keys.privateKey.Return();
				keys.publicKey.Return();
			}

			this.HashKey(key);

			Log.Information($"XMSS Key '{name}' created");

			return key;
		}

		public IXmssMTWalletKey CreateXmssmtKey(string name, float warningLevel, float changeLevel) {
			return this.CreateXmssmtKey(name, XMSSMTProvider.DEFAULT_XMSSMT_TREE_HEIGHT, XMSSMTProvider.DEFAULT_XMSSMT_TREE_LAYERS, XMSSProvider.DEFAULT_HASH_BITS, warningLevel, changeLevel);
		}

		public IXmssMTWalletKey CreateXmssmtKey(string name, int treeHeight, int treeLayers, Enums.KeyHashBits hashBits, float warningLevel, float changeLevel) {
			IXmssMTWalletKey key = this.CreateBasicKey<IXmssMTWalletKey>(name, Enums.KeyTypes.XMSSMT);

			using(XMSSMTProvider provider = new XMSSMTProvider(hashBits, treeHeight, treeLayers)) {
				provider.Initialize();

				Log.Information($"Creating a new XMSS^MT key named '{name}' with tree height {treeHeight}, tree layers {treeLayers} and hashBits {provider.HashBits}");

				(IByteArray privateKey, IByteArray publicKey) keys = provider.GenerateKeys();

				key.HashBits = provider.HashBitsEnum;
				key.TreeHeight = provider.TreeHeight;
				key.TreeLayers = provider.TreeLayers;
				key.KeyType = Enums.KeyTypes.XMSSMT;
				key.WarningHeight = provider.GetKeyUseThreshold(warningLevel);
				key.ChangeHeight = provider.GetKeyUseThreshold(changeLevel);
				key.MaximumHeight = provider.MaximumHeight;
				key.KeyUseIndex = 0;

				key.PrivateKey = keys.privateKey.ToExactByteArrayCopy();
				key.PublicKey = keys.publicKey.ToExactByteArrayCopy();

				// store a copy just in case
				key.InitialPrivateKey = key.PrivateKey.ToArray();

				keys.privateKey.Return();
				keys.publicKey.Return();
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

				(IByteArray privateKey, IByteArray publicKey) keys = provider.GenerateKeys();
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
		public virtual IByteArray PerformCryptographicSignature(Guid accountUuid, string keyName, IByteArray message) {

			this.EnsureWalletIsLoaded();
			this.EnsureWalletKeyIsReady(accountUuid, keyName);

			IWalletKey key = this.LoadKey<IWalletKey>(k => {
				return k;
			}, accountUuid, keyName);

			if(key == null) {
				throw new ApplicationException($"The key named '{keyName}' could not be loaded. Make sure it is available before progressing.");
			}

			return this.PerformCryptographicSignature(key, message);
		}

		public virtual IByteArray PerformCryptographicSignature(IWalletKey key, IByteArray message) {

			this.EnsureWalletIsLoaded();
			this.EnsureWalletKeyIsReady(key.AccountUuid, key.Name);

			IByteArray signature = null;

			if(key is IXmssWalletKey xmssWalletKey) {

				// check if we reached the maximum use of our key
				bool keyStillUsable = xmssWalletKey.KeyUseIndex < xmssWalletKey.MaximumHeight;

				if(keyStillUsable) {

					XMSSProviderBase provider = null;

					if(key is IXmssMTWalletKey xmssMTWalletKey && (key.KeyType == Enums.KeyTypes.XMSSMT)) {
						provider = new XMSSMTProvider(xmssMTWalletKey.HashBits, Enums.ThreadMode.Half, xmssMTWalletKey.TreeHeight, xmssMTWalletKey.TreeLayers);
					} else {
						provider = new XMSSProvider(xmssWalletKey.HashBits, xmssWalletKey.TreeHeight);
					}

					(IByteArray signature, IByteArray nextPrivateKey) result;

					using(provider) {

						provider.Initialize();

						if(provider is XMSSMTProvider xmssmtProvider) {
							result = xmssmtProvider.Sign(message, (ByteArray) key.PrivateKey);
						} else if(provider is XMSSProvider xmssProvider) {
							result = xmssProvider.Sign(message, (ByteArray) key.PrivateKey);
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
					this.UpdateKey(key);
				}

				if((xmssWalletKey.KeyUseIndex >= xmssWalletKey.ChangeHeight) && (key.Status != Enums.KeyStatus.Changing)) {
					Log.Warning($"Key named {key.Name} has reached end of life. An automatic key change is being performed. You can keep using it until the change is fully confirmed.");

					// Here we trigger the key change workflow, we must change the key, its time adn we wont trust the user to do it in time at this point. they were warned already

					this.KeyUseMaximumLevelReached(key.KeyAddress.OrdinalId, xmssWalletKey.KeyUseIndex, xmssWalletKey.WarningHeight, xmssWalletKey.ChangeHeight, new CorrelationContext());
				} else if(xmssWalletKey.KeyUseIndex >= xmssWalletKey.WarningHeight) {
					Log.Warning($"Key named {key.Name} is nearing its end of life. A manual key change is recommended. You can use it {xmssWalletKey.MaximumHeight - xmssWalletKey.KeyUseIndex} more times before an automatic key change.");
					this.KeyUseWarningLevelReached(key.KeyAddress.OrdinalId, xmssWalletKey.KeyUseIndex, xmssWalletKey.WarningHeight, xmssWalletKey.ChangeHeight);
				}

				if(!keyStillUsable) {
					// we have reached the maximum use amount for this key. we can't sign anything else until a key change happens
					throw new ApplicationException("Your xmss key has reached it's full use. A key change must now be performed?");
				}

			} else if(key is ISecretDoubleWalletKey qsecretDoubleWalletKey) {
				IByteArray signature1 = null;
				IByteArray signature2 = null;

				using(QTeslaProvider provider = new QTeslaProvider(qsecretDoubleWalletKey.SecurityCategory)) {
					provider.Initialize();

					// thats it, perform the signature and increment our private key
					signature1 = provider.Sign(message, (ByteArray) qsecretDoubleWalletKey.PrivateKey);
				}

				using(QTeslaProvider provider = new QTeslaProvider(qsecretDoubleWalletKey.SecondKey.SecurityCategory)) {
					provider.Initialize();

					// thats it, perform the signature and increment our private key
					signature2 = provider.Sign(message, (ByteArray) qsecretDoubleWalletKey.SecondKey.PrivateKey);
				}

				IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

				dehydrator.Write(signature1);
				dehydrator.Write(signature2);

				signature = dehydrator.ToArray();

			} else if(key is IQTeslaWalletKey qTeslaWalletKey) {
				using(QTeslaProvider provider = new QTeslaProvider(qTeslaWalletKey.SecurityCategory)) {
					provider.Initialize();

					// thats it, perform the signature and increment our private key
					signature = provider.Sign(message, (ByteArray) qTeslaWalletKey.PrivateKey);
				}
			} else {
				throw new ApplicationException("Invalid key type provided");
			}

			return signature;
		}

		protected virtual void KeyUseWarningLevelReached(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight) {
			// do nothing
		}

		protected virtual void KeyUseMaximumLevelReached(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			this.LaunchChangeKeyWorkflow(changeKeyOrdinal, keyUseIndex, warningHeight, maximumHeight, correlationContext);
		}

		protected virtual void LaunchChangeKeyWorkflow(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			var changeKeyTransactionWorkflow = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateChangeKeyTransactionWorkflow(changeKeyOrdinal, "automatically triggered keychange", correlationContext);

			this.centralCoordinator.PostWorkflow(changeKeyTransactionWorkflow);
		}

	#endregion

	#region external API

		/// <summary>
		///     Query the entire wallet transaction history
		/// </summary>
		/// <param name="taskStasher"></param>
		/// <returns></returns>
		public virtual List<WalletTransactionHistoryHeaderAPI> APIQueryWalletTransactionHistory(Guid accountUuid) {
			this.EnsureWalletIsLoaded();

			var results = this.WalletFileInfo.Accounts[accountUuid].WalletTransactionHistoryInfo.RunQuery<WalletTransactionHistoryHeaderAPI, WalletTransactionHistory>(caches => caches.Select(t => {

				TransactionId transactionId = new TransactionId(t.TransactionId);
				var version = new ComponentVersion<TransactionType>(t.Version);

				return new WalletTransactionHistoryHeaderAPI {
					TransactionId = t.TransactionId, Sender = transactionId.Account.ToString(), Timestamp = t.Timestamp, Status = t.Status,
					Version = new {transactionType = version.Type.Value, major = version.Major.Value, minor = version.Minor.Value}, Local = t.Local, Note = t.Note, Recipient = t.Recipient
				};
			}).OrderByDescending(t => t.Timestamp).ToList());

			return results.ToList();

		}

		public virtual WalletTransactionHistoryDetailsAPI APIQueryWalletTransationHistoryDetails(Guid accountUuid, string transactionId) {
			this.EnsureWalletIsLoaded();

			if(accountUuid == Guid.Empty) {
				accountUuid = this.GetAccountUuid();
			}

			var results = this.WalletFileInfo.Accounts[accountUuid].WalletTransactionHistoryInfo.RunQuery<WalletTransactionHistoryDetailsAPI, WalletTransactionHistory>(caches => caches.Where(t => t.TransactionId == transactionId).Select(t => {

				var version = new ComponentVersion<TransactionType>(t.Version);

				return new WalletTransactionHistoryDetailsAPI {
					TransactionId = t.TransactionId, Sender = new TransactionId(t.TransactionId).Account.ToString(), Timestamp = t.Timestamp, Status = t.Status,
					Version = new {transactionType = version.Type.Value, major = version.Major.Value, minor = version.Minor.Value}, Recipient = t.Recipient, Contents = t.Contents, Local = t.Local,
					Note = t.Note
				};
			}).ToList());

			return results.SingleOrDefault();

		}

		/// <summary>
		///     Get the list of all accounts in the wallet
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual List<WalletAccountAPI> APIQueryWalletAccounts() {
			this.EnsureWalletIsLoaded();

			Guid activeAccountUuid = this.GetActiveAccount().AccountUuid;

			var apiAccounts = new List<WalletAccountAPI>();

			foreach(IWalletAccount account in this.GetAccounts()) {

				apiAccounts.Add(new WalletAccountAPI {
					AccountUuid = account.AccountUuid, AccountId = account.GetAccountId().ToString(), FriendlyName = account.FriendlyName, Status = account.Status,
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
		public virtual WalletAccountDetailsAPI APIQueryWalletAccountDetails(Guid accountUuid) {
			this.EnsureWalletIsLoaded();

			Guid activeAccountUuid = this.GetActiveAccount().AccountUuid;
			IWalletAccount account = this.GetWalletAccount(accountUuid);

			var apiAccounts = new List<WalletAccountAPI>();

			return new WalletAccountDetailsAPI {
				AccountUuid = account.AccountUuid, AccountId = account.PublicAccountId?.ToString(), AccountHash = account.AccountUuidHash?.ToString(), FriendlyName = account.FriendlyName,
				Status = account.Status, IsActive = account.AccountUuid == activeAccountUuid, AccountType = (int) account.WalletAccountType, TrustLevel = account.TrustLevel,
				DeclarationBlockid = account.ConfirmationBlockId, KeysEncrypted = account.KeyLogFileEncryptionParameters != null
			};

		}

		public TransactionId APIQueryWalletAccountPresentationTransactionId(Guid accountUuid) {
			this.EnsureWalletIsLoaded();

			IWalletAccount account = this.GetWalletAccount(accountUuid);

			if(account == null) {
				throw new ApplicationException($"Failed to load account with Uuid {accountUuid}");
			}

			return account.PresentationTransactionId.Clone;

		}

	#endregion

	#region walletservice methods

		public IByteArray SignTransaction(IByteArray transactionHash, string keyName) {
			this.EnsureWalletIsLoaded();

			//TODO: make sure we confirm our signature height in the wallet with the recorded one on chain. To prevent mistaken wallet copies.
			IWalletAccount activeAccount = this.GetActiveAccount();

			using(IWalletKey key = this.LoadKey<IWalletKey>(k => k, activeAccount.AccountUuid, keyName)) {
				if(key == null) {
					throw new ApplicationException($"The key named '{keyName}' could not be loaded. Make sure it is available before progressing.");
				}

				// thats it, lets perform the signature
				if(key is IXmssWalletKey xmssWalletKey) {
					return this.SignTransactionXmss(transactionHash, xmssWalletKey);
				}

				return this.SignTransaction(transactionHash, key);
			}
		}

		/// <summary>
		///     This version will ensure track key usage heights
		/// </summary>
		/// <param name="taskStasher"></param>
		/// <param name="transactionHash"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public IByteArray SignTransactionXmss(IByteArray transactionHash, IXmssWalletKey key) {
			this.EnsureWalletIsLoaded();

			//TODO: we would want to do it for (potentially) sphincs and xmssmt too
			ChainConfigurations configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration();

			if(configuration.UseKeyLog && configuration.KeySecurityConfigurations.EnableKeyHeightChecks) {
				KeyUseIndexSet lastSyncedKeyUse = this.GetChainStateLastSyncedKeyHeight(key);

				if(lastSyncedKeyUse.IsSet && (new KeyUseIndexSet(key.KeySequenceId, key.KeyUseIndex, key.KeyAddress.OrdinalId) < lastSyncedKeyUse)) {
					throw new ApplicationException("Your key height is lower than the chain key use height. This is very serious. Are you using an older copy of your regular wallet?");
				}
			}

			// thats it, lets perform the signature
			return this.SignTransaction(transactionHash, key);
		}

		public IByteArray SignTransaction(IByteArray transactionHash, IWalletKey key) {

			// thats it, lets perform the signature
			return this.PerformCryptographicSignature(key, transactionHash);
		}

		public IByteArray SignMessageXmss(IByteArray messageHash, IXmssWalletKey key) {

			// thats it, lets perform the signature
			IByteArray results = this.SignTransactionXmss(messageHash, key);

			// for messages, we never get confirmation, so we update the key height right away
			this.UpdateLocalChainStateKeyHeight(key);

			// and confirmation too
			this.UpdateLocalChainStateTransactionKeyLatestSyncHeight(key.AccountUuid, new KeyUseIndexSet(key.KeySequenceId, key.KeyUseIndex, key.KeyAddress.OrdinalId), true);

			return results;
		}

		public IByteArray SignMessageXmss(Guid accountUuid, IByteArray message) {
			using(IXmssWalletKey key = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(accountUuid, GlobalsService.MESSAGE_KEY_NAME)) {

				// and sign the whole thing with our key
				return this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SignMessageXmss(message, key);
			}
		}

		public IByteArray SignMessage(IByteArray messageHash, IWalletKey key) {

			// thats it, lets perform the signature
			return this.SignTransaction(messageHash, key);
		}

		public virtual SynthesizedBlock ConvertApiSynthesizedBlock(SynthesizedBlockAPI synthesizedBlockApi) {
			SynthesizedBlock synthesizedBlock = this.CreateSynthesizedBlockFromApi(synthesizedBlockApi);

			synthesizedBlock.BlockId = synthesizedBlockApi.BlockId;

			GzipCompression compressor = new GzipCompression();

			foreach(var apiTransaction in synthesizedBlockApi.ConfirmedGeneralTransactions) {
				IDehydratedTransaction dehydratedTransaction = new DehydratedTransaction();

				IByteArray bytes = compressor.Decompress((ByteArray) apiTransaction.Value);
				dehydratedTransaction.Rehydrate(bytes);
				bytes.Return();

				ITransaction transaction = dehydratedTransaction.RehydrateTransaction(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

				synthesizedBlock.ConfirmedGeneralTransactions.Add(transaction.TransactionId.SimpleTransactionId, transaction);
			}

			synthesizedBlock.RejectedTransactions.AddRange(synthesizedBlockApi.RejectedTransactions.Select(t => new RejectedTransaction {TransactionId = new TransactionIdExtended(t.Key), Reason = (RejectionCode) t.Value}));

			AccountId accountId = null;

			bool hasPublicAccount = !string.IsNullOrWhiteSpace(synthesizedBlockApi.AccountId);
			bool hasAcountHash = !string.IsNullOrWhiteSpace(synthesizedBlockApi.AccountHash);

			if(hasPublicAccount || hasAcountHash) {
				var accounts = this.GetAccounts();

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

					IByteArray bytes = compressor.Decompress((ByteArray) apiTransaction.Value);
					dehydratedTransaction.Rehydrate(bytes);
					bytes.Return();

					ITransaction transaction = dehydratedTransaction.RehydrateTransaction(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

					if(transaction.TransactionId.Account == accountId) {
						synthesizedBlockAccountSet.ConfirmedLocalTransactions.Add(transaction.TransactionId, transaction);
					} else {
						synthesizedBlockAccountSet.ConfirmedExternalsTransactions.Add(transaction.TransactionId.SimpleTransactionId, transaction);
					}

					synthesizedBlock.ConfirmedTransactions.Add(transaction.TransactionId.SimpleTransactionId, transaction);
				}

				synthesizedBlock.AccountScopped.Add(accountId, synthesizedBlockAccountSet);
				synthesizedBlock.Accounts.Add(accountId);

			}

			return synthesizedBlock;
		}

		protected abstract SynthesizedBlock CreateSynthesizedBlockFromApi(SynthesizedBlockAPI synthesizedBlockApi);

		public abstract SynthesizedBlockAPI DeserializeSynthesizedBlockAPI(string synthesizedBlock);

	#endregion

	}

}