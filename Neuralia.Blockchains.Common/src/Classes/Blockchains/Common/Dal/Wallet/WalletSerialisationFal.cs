using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IO;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface IWalletSerialisationFal {

		ICentralCoordinator CentralCoordinator { get; }
		WalletSerializationTransactionalLayer TransactionalFileSystem { get; }
		string GetWalletFilePath();
		string GetChainStorageFilesPath();
		string GetWalletFolderPath();
		string GetWalletCryptoFilePath();
		string GetWalletAccountsFolderPath(string AccountCode);
		string GetWalletKeysFilePath(string accountCode, string name);
		string GetWalletKeyLogPath(string accountCode);
		string GetWalletChainStatePath(string accountCode);
		string GetWalletCachePath();
		string GetWalletUnwrappedCachePath();
		string GetWalletKeysCachePath();
		
		Task<bool> RescueFromNarballStructure();
		
		/// <summary>
		///     check the wallet file to know if it is encrypted
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		bool IsFileWalletEncrypted();
		
		Task<SafeArrayHandle> RunDbOperation(string creatorKey, Func<IWalletDBDAL, LockContext, Task> operation, SafeArrayHandle databaseBytes, LockContext lockContext);
		Task<(SafeArrayHandle newBytes, T result)> RunDbOperation<T>(string creatorKey, Func<IWalletDBDAL, LockContext, Task<T>> operation, SafeArrayHandle databaseBytes, LockContext lockContext);
		Task<bool> WalletFullyCreated(LockContext lockContext);
		Task InstallWalletCreatingTag(LockContext lockContext);
		Task RemoveWalletCreatingTag(LockContext lockContext);
		Task ClearDamagedWallet(LockContext lockContext);
		Task<T> RunQueryDbOperation<T>(string creatorKey, Func<IWalletDBDAL, LockContext, Task<T>> operation, SafeArrayHandle databaseBytes, LockContext lockContext);
		Task SaveFile(string filename, SafeArrayHandle databaseBytes, EncryptionInfo encryptionInfo, bool wrapEncryptedBytes);
		Task<SafeArrayHandle> LoadFile(string filename, EncryptionInfo encryptionInfo, bool wrappedEncryptedBytes);
		IUserWalletFileInfo CreateWalletFileInfo();

		WalletKeyFileInfo CreateWalletKeysFileInfo<KEY_TYPE>(IWalletAccount account, string keyName, byte ordinalId, WalletPassphraseDetails walletPassphraseDetails, AccountPassphraseDetails accountPassphraseDetails)
			where KEY_TYPE : IWalletKey;

		WalletKeyLogFileInfo CreateWalletKeyLogFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		WalletChainStateFileInfo CreateWalletChainStateFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		IWalletGenerationCacheFileInfo CreateWalletGenerationCacheFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		IWalletTransactionHistoryFileInfo CreateWalletTransactionHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		IWalletElectionsHistoryFileInfo CreateWalletElectionsHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		IWalletElectionsStatisticsFileInfo CreateWalletElectionsStatisticsFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		
		WalletElectionCacheFileInfo CreateWalletElectionCacheFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		WalletKeyHistoryFileInfo CreateWalletKeyHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);

		IWalletAccountSnapshotFileInfo CreateWalletSnapshotFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		bool WalletKeyFileExists(string accountCode, string name);

		Task<WalletSerializationTransaction> BeginTransaction();

		Task CommitTransaction();

		Task RollbackTransaction();
	}

	public static class WalletSerialisationFal {

		private static readonly ConcurrentDictionary<string, Func<RecyclableMemoryStream, IWalletDBDAL>> creators = new ConcurrentDictionary<string, Func<RecyclableMemoryStream, IWalletDBDAL>>();

		public static void RegisterCreator(string key, Func<RecyclableMemoryStream, IWalletDBDAL> creator) {
			if(string.IsNullOrWhiteSpace(key)) {
				DefaultWalletDALCreator = creator;
			} else {
				if(!creators.ContainsKey(key)) {
					creators.TryAdd(key, creator);
				} else {
					creators[key] = creator;
				}
			}
		}
		
		public static Func<RecyclableMemoryStream, IWalletDBDAL> GetCreator(string key) {
			if(!string.IsNullOrWhiteSpace(key) && creators.ContainsKey(key)) {
				return creators[key];
			}

			return DefaultWalletDALCreator;
		}
		private static Func<RecyclableMemoryStream, IWalletDBDAL> DefaultWalletDALCreator { get; set; } = (ms) => LiteDBDAL.GetLiteDBDAL(ms);
	}

	public abstract class WalletSerialisationFal<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWalletSerialisationFal
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public const string WALLET_FOLDER_PATH = "wallet";

		public const string BACKUP_FOLDER_PATH = "backups";

		public const string WALLET_FILE_NAME = "wallet.neuralia";
		
		public const string WALLET_CACHE_FOLDER_NAME = "cache";
		
		
		public const string WALLET_KEYS_CACHE_FOLDER_NAME = "keys";

		public const string STORAGE_FOLDER_NAME = "system";

		public const string WALLET_CRYPTO_FILE_NAME = "wallet.crypto.neuralia";

		public const string WALLET_KEYS_FILE_NAME = "{0}.key";

		public const string ACCOUNTS_FOLDER_PATH = "accounts";

		public const string ACCOUNTS_KEYS_FOLDER_PATH = "keys";
		public const string WALLET_CREATING_TAG_PATH = "creating.tag";
		

		public const string WALLET_KEY_HISTORY_FILE_NAME = "KeyHistory.neuralia";
		public const string WALLET_KEYLOG_FILE_NAME = "KeyLog.neuralia";
		public const string WALLET_CHAINSTATE_FILE_NAME = "ChainState.neuralia";
		public const string WALLET_TRANSACTION_CACHE_FILE_NAME = "GenerationCache.neuralia";
		public const string WALLET_TRANSACTION_HISTORY_FILE_NAME = "TransactionHistory.neuralia";
		public const string WALLET_ELECTIONS_HISTORY_FILE_NAME = "ElectionsHistory.neuralia";
		public const string WALLET_ELECTIONS_STATISTICS_FILE_NAME = "ElectionsStatistics.neuralia";
		public const string WALLET_ELECTION_CACHE_FILE_NAME = "ElectionCache.neuralia";
		public const string WALLET_ACCOUNT_SNAPSHOT_FILE_NAME = "AccountSnapshot.neuralia";

		private const long ENCRYPTED_WALLET_TAG = 0x5555555555555555L;

		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		public ICentralCoordinator CentralCoordinator => this.centralCoordinator;
		
		protected readonly string ChainFilesDirectoryPath;

		protected readonly bool compressFiles;

		protected readonly ICompression walletCompressor;
		private SafeArrayHandle encryptionFlagBytes;
		
		public WalletSerialisationFal(CENTRAL_COORDINATOR centralCoordinator, string chainFilesDirectoryPath, FileSystemWrapper fileSystem) {
			this.ChainFilesDirectoryPath = chainFilesDirectoryPath;

			List<(string name, WalletSerializationTransactionalLayer.FilesystemTypes type)> exclusions = new List<(string name, WalletSerializationTransactionalLayer.FilesystemTypes type)>(new[] {("events", WalletSerializationTransactionalLayer.FilesystemTypes.Folder)});
			this.TransactionalFileSystem = new WalletSerializationTransactionalLayer(centralCoordinator, this.GetWalletFolderPath(), exclusions, fileSystem);

			this.centralCoordinator = centralCoordinator;
			this.compressFiles = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.CompressWallet;

			if(this.compressFiles) {
				this.walletCompressor = new BrotliCompression();
			} else {
				this.walletCompressor = new NullCompression();
			}
		}

		protected FileSystemWrapper FileSystem => this.CentralCoordinator.FileSystem;
		
		protected IWalletDBDAL GetDBDal(string key, RecyclableMemoryStream walletStream) {
			return WalletSerialisationFal.GetCreator(key)(walletStream);
		}

		public WalletSerializationTransactionalLayer TransactionalFileSystem { get; }

		public Task<WalletSerializationTransaction> BeginTransaction() {
			return this.TransactionalFileSystem.BeginTransaction();
		}

		public Task CommitTransaction() {
			return this.TransactionalFileSystem.CommitTransaction();
		}

		public Task RollbackTransaction() {
			return this.TransactionalFileSystem.RollbackTransaction();
		}

		public string GetChainStorageFilesPath() {
			return Path.Combine(this.ChainFilesDirectoryPath, STORAGE_FOLDER_NAME);
		}
		
		public virtual string GetWalletFilePath() {

			return Path.Combine(this.GetWalletFolderPath(), WALLET_FILE_NAME);
		}

		public string GetWalletCachePath() {
			return Path.Combine(this.GetWalletFolderPath(), WALLET_CACHE_FOLDER_NAME);
		}
		
		public string GetWalletUnwrappedCachePath() {
			return Path.Combine(this.GetWalletCachePath(), WalletSerializationTransactionalLayer.UNWRAPPED_FOLDER_NAME);
		}
		
		public string GetWalletKeysCachePath() {
			return Path.Combine(this.GetWalletUnwrappedCachePath(), WALLET_KEYS_CACHE_FOLDER_NAME);
		}
		
		public virtual string GetWalletCryptoFilePath() {
			return Path.Combine(this.GetWalletFolderPath(), WALLET_CRYPTO_FILE_NAME);
		}

		public virtual string GetWalletAccountsFolderPath(string AccountCode) {
			return Path.Combine(Path.Combine(this.GetWalletFolderPath(), ACCOUNTS_FOLDER_PATH), AccountCode.ToString());
		}

		public virtual string GetWalletKeysFilePath(string accountCode, string name) {
			return Path.Combine(this.GetWalletAccountsKeysFolderPath(accountCode), string.Format(WALLET_KEYS_FILE_NAME, name));
		}

		public virtual string GetWalletKeyLogPath(string accountCode) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountCode), WALLET_KEYLOG_FILE_NAME);
		}

		public virtual string GetWalletChainStatePath(string accountCode) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountCode), WALLET_CHAINSTATE_FILE_NAME);
		}

		public Task<bool> RescueFromNarballStructure() {
			return this.TransactionalFileSystem.RescueFromNarballStructure();
		}

		public Task<bool> WalletFullyCreated(LockContext lockContext) {

			return Task.FromResult(!this.TransactionalFileSystem.FileExists(this.GetWalletCreatingTagPath()));
		}

		public Task InstallWalletCreatingTag(LockContext lockContext) {

			FileExtensions.EnsureFileExists(this.GetWalletCreatingTagPath(), this.FileSystem);
			return Task.CompletedTask;
		}
		
		public Task RemoveWalletCreatingTag(LockContext lockContext) {

			string path = this.GetWalletCreatingTagPath();

			if(this.TransactionalFileSystem.FileExists(path)) {
				this.TransactionalFileSystem.FileDelete(path);
			}
			
			return Task.CompletedTask;
		}
		
		public Task ClearDamagedWallet(LockContext lockContext) {
			
			this.TransactionalFileSystem.DeleteDirectory(this.GetWalletFolderPath(), true);
			
			return Task.CompletedTask;
		}
		
		/// <summary>
		///     check the wallet file to know if it is encrypted
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public bool IsFileWalletEncrypted() {
			string walletFile = this.GetWalletFilePath();
			string walletCryptoFile = null;

			if(!this.TransactionalFileSystem.DirectoryExists(this.GetWalletFolderPath())) {
				this.CentralCoordinator.Log.Information($"Creating new wallet baseFolder in path: {this.GetWalletFolderPath()}");
				this.TransactionalFileSystem.CreateDirectory(this.GetWalletFolderPath());
			}

			bool walletFileExists = this.TransactionalFileSystem.FileExists(walletFile);

			if(!walletFileExists) {
				return false;
			}

			// check if it is encrypted
			bool encrypted = this.CheckWalletEncrypted(walletFile);

			if(encrypted) {
				walletCryptoFile = this.GetWalletCryptoFilePath();
				bool walletCryptoFileExists = this.TransactionalFileSystem.FileExists(walletCryptoFile);

				if(!walletCryptoFileExists) {
					throw new ApplicationException("An encrypted wallet file was found, but no corresponding cryptographic parameters file was found. Cannot decrypt.");
				}
			}

			return encrypted;
		}

		/// <summary>
		///     Add the encrypted marker to the encrypted bytes
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public void WrapEncryptedBytes(SafeArrayHandle buffer) {
			
			buffer.Entry.ResetOffsetIncrement();

			if(this.encryptionFlagBytes == null) {
				this.encryptionFlagBytes = SafeArrayHandle.Create(sizeof(long));

				TypeSerializer.Serialize(ENCRYPTED_WALLET_TAG, this.encryptionFlagBytes.Span);
			}

			this.encryptionFlagBytes.CopyTo(buffer);
		}

		public async Task<SafeArrayHandle> RunDbOperation(string creatorKey, Func<IWalletDBDAL, LockContext, Task> operation, SafeArrayHandle databaseBytes, LockContext lockContext) {
			await using RecyclableMemoryStream walletStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("dbstream");

			if(databaseBytes.HasData) {
				walletStream.Write(databaseBytes.Bytes, databaseBytes.Offset, databaseBytes.Length);
				walletStream.Position = 0;
			}
			
			if(operation != null) {
				IWalletDBDAL dal = this.GetDBDal(creatorKey, walletStream);
				
				await Repeater.RepeatAsync(() => operation(dal, lockContext)).ConfigureAwait(false);
			}

			SafeArrayHandle result = SafeArrayHandle.Create(walletStream);

			walletStream.ClearStream();

			return result;

		}

		public async Task<(SafeArrayHandle newBytes, T result)> RunDbOperation<T>(string creatorKey, Func<IWalletDBDAL, LockContext, Task<T>> operation, SafeArrayHandle databaseBytes, LockContext lockContext) {

			T result = default;

			SafeArrayHandle newbytes = await this.RunDbOperation(creatorKey, async (IWalletDBDAL, lc) => {

				result = await operation(IWalletDBDAL, lc).ConfigureAwait(false);

			}, databaseBytes, lockContext).ConfigureAwait(false);

			return (newbytes, result);
		}

		public async Task<T> RunQueryDbOperation<T>(string creatorKey, Func<IWalletDBDAL, LockContext, Task<T>> operation, SafeArrayHandle databaseBytes, LockContext lockContext) {
			await using RecyclableMemoryStream walletStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("dbstream");

			if(databaseBytes.IsEmpty) {
				throw new ApplicationException("database bytes can not be null");
			}

			walletStream.Write(databaseBytes.Bytes, databaseBytes.Offset, databaseBytes.Length);
			walletStream.Position = 0;

			IWalletDBDAL dal = this.GetDBDal(creatorKey, walletStream);

			T result = await Repeater.RepeatAsync(() => operation(dal, lockContext)).ConfigureAwait(false);

			walletStream.ClearStream();

			return result;

		}

		public async Task SaveFile(string filename, SafeArrayHandle databaseBytes, EncryptionInfo encryptionInfo, bool wrapEncryptedBytes) {
			try {

				if(databaseBytes.IsEmpty) {
					throw new ApplicationException("Cannot save null database data");
				}

				string directory = this.TransactionalFileSystem.GetDirectoryName(filename);

				if(!this.TransactionalFileSystem.DirectoryExists(directory)) {
					this.CentralCoordinator.Log.Information($"Creating new baseFolder in path: {directory}");
					this.TransactionalFileSystem.CreateDirectory(directory);
				}

				SafeArrayHandle compressedBytes = null;
				SafeArrayHandle encryptedBytes = null;

				try {
					//  a temporary holder, do not dispose.
					SafeArrayHandle activeBytes = null;

					if(encryptionInfo.Encrypt) {

						if(encryptionInfo.EncryptionParameters == null) {
							throw new ApplicationException("Encryption parameters were null. can not encrypt");
						}

						if(this.compressFiles) {
							compressedBytes = this.walletCompressor.Compress(databaseBytes);
							activeBytes = compressedBytes;
						} else {
							activeBytes = databaseBytes;
						}
						
						encryptedBytes = FileEncryptor.Encrypt(activeBytes, encryptionInfo.Secret(), encryptionInfo.EncryptionParameters, sizeof(long));
						
						if(wrapEncryptedBytes) {
							// wrap the encrypted byes with the flag marker
							 this.WrapEncryptedBytes(encryptedBytes);
						}
						
						activeBytes = encryptedBytes;
					} else {
						if(this.compressFiles) {
							compressedBytes = this.walletCompressor.Compress(databaseBytes);
							activeBytes = compressedBytes;
						} else {
							activeBytes = databaseBytes;
						}
					}
					
					if(activeBytes.Length == 0) {
						throw new ApplicationException("Write size can not be 0!");
					}
					
					await this.TransactionalFileSystem.OpenWriteAsync(filename, activeBytes).ConfigureAwait(false);
				} finally {
					if(compressedBytes != null) {
						compressedBytes.Entry.Clear();
						compressedBytes.Return();
					}

					if(encryptedBytes != null) {
						encryptedBytes.Entry.Clear();
						encryptedBytes.Return();
					}
				}

			} catch(Exception e) {
				this.CentralCoordinator.Log.Verbose("error occured", e);

				throw;
			}
		}

		public async Task<SafeArrayHandle> LoadFile(string filename, EncryptionInfo encryptionInfo, bool wrappedEncryptedBytes) {
			try {

				if(!this.TransactionalFileSystem.FileExists(filename)) {
					throw new FileNotFoundException("file does not exist");
				}

				string directory = Path.GetDirectoryName(filename);

				if(!this.TransactionalFileSystem.DirectoryExists(directory)) {
					this.CentralCoordinator.Log.Information($"Creating new baseFolder in path: {directory}");
					this.TransactionalFileSystem.CreateDirectory(directory);
				}

				if(encryptionInfo.Encrypt) {

					if(encryptionInfo.EncryptionParameters == null) {
						throw new ApplicationException("Encryption parameters were null. can not encrypt");
					}

					SafeArrayHandle encryptedWalletFileBytes = null;

					if(wrappedEncryptedBytes) {
						encryptedWalletFileBytes = this.GetEncryptedWalletFileBytes(filename);
					} else {
						encryptedWalletFileBytes = SafeArrayHandle.WrapAndOwn(await this.TransactionalFileSystem.ReadAllBytesAsync(filename).ConfigureAwait(false));
					}

					using SafeArrayHandle decryptedWalletBytes = FileEncryptor.Decrypt(encryptedWalletFileBytes, encryptionInfo.Secret(), encryptionInfo.EncryptionParameters);

					if(!this.compressFiles) {
						return decryptedWalletBytes.Branch();
					}

					return this.walletCompressor.Decompress(decryptedWalletBytes);

				}

				using SafeArrayHandle walletSimpleBytes = SafeArrayHandle.WrapAndOwn(await this.TransactionalFileSystem.ReadAllBytesAsync(filename).ConfigureAwait(false));

				if(!this.compressFiles) {
					return walletSimpleBytes.Branch();
				}

				return this.walletCompressor.Decompress(walletSimpleBytes);

			} catch(Exception e) {
				this.CentralCoordinator.Log.Verbose("error occured", e);

				throw;
			}
		}

		/// <summary>
		///     tells us if a wallet key file exists
		/// </summary>
		/// <param name="AccountCode"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public bool WalletKeyFileExists(string accountCode, string name) {
			string walletKeysFile = this.GetWalletKeysFilePath(accountCode, name);

			return this.TransactionalFileSystem.FileExists(walletKeysFile);
		}

		public virtual string GetWalletFolderPath() {

			return Path.Combine(this.ChainFilesDirectoryPath, WALLET_FOLDER_PATH);
		}
		
		public virtual string GetWalletCreatingTagPath() {

			return Path.Combine(this.GetWalletFolderPath(), WALLET_CREATING_TAG_PATH);
		}

		public virtual string GetWalletAccountsContentsFolderPath(string AccountCode) {
			return this.GetWalletAccountsFolderPath(AccountCode);
		}

		public virtual string GetWalletAccountsKeysFolderPath(string AccountCode) {
			return Path.Combine(this.GetWalletAccountsFolderPath(AccountCode), ACCOUNTS_KEYS_FOLDER_PATH);
		}

		public virtual string GetWalletKeyHistoryPath(string accountCode) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountCode), WALLET_KEY_HISTORY_FILE_NAME);
		}

		public virtual string GetBackupsFolderPath() {

			return Path.Combine(this.ChainFilesDirectoryPath, BACKUP_FOLDER_PATH);
		}

		public virtual string GetWalletGenerationCachePath(string accountCode) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountCode), WALLET_TRANSACTION_CACHE_FILE_NAME);
		}

		public virtual string GetWalletTransactionHistoryPath(string accountCode) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountCode), WALLET_TRANSACTION_HISTORY_FILE_NAME);
		}

		public virtual string GetWalletElectionsHistoryPath(string accountCode) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountCode), WALLET_ELECTIONS_HISTORY_FILE_NAME);
		}

		public virtual string GetWalletElectionsStatisticsPath(string accountCode) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountCode), WALLET_ELECTIONS_STATISTICS_FILE_NAME);
		}
		
		public virtual string GetWalletElectionCachePath(string accountCode) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountCode), WALLET_ELECTION_CACHE_FILE_NAME);
		}

		public virtual string GetWalletAccountSnapshotPath(string accountCode) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountCode), WALLET_ACCOUNT_SNAPSHOT_FILE_NAME);
		}

		/// <summary>
		///     this method will read the first 8 bytes of the wallet file and determine if it is encrypted.
		/// </summary>
		/// <param name="walletFile"></param>
		/// <returns></returns>
		private bool CheckWalletEncrypted(string walletFile) {
			using Stream stream = this.TransactionalFileSystem.OpenRead(walletFile);

			if(stream.Length == 0) {
				throw new ApplicationException("Wallet size cannot be 0.");
			}

			using ByteArray buffer = ByteArray.Create(sizeof(long));

			stream.Read(buffer.Bytes, buffer.Offset, buffer.Length);

			TypeSerializer.Deserialize(buffer.Span, out long tag);

			return tag == ENCRYPTED_WALLET_TAG;

		}

		/// <summary>
		///     Get the valuable bytes of an encrtypted wallet file (skip the initial marker tag)
		/// </summary>
		/// <param name="walletFile"></param>
		/// <returns></returns>
		private SafeArrayHandle GetEncryptedWalletFileBytes(string walletFile) {

			using Stream fs = this.TransactionalFileSystem.OpenRead(walletFile);

			fs.Position = sizeof(long); // skip the marker

			SafeArrayHandle buffer = SafeArrayHandle.Create((int) fs.Length - sizeof(long));

			fs.Read(buffer.Bytes, buffer.Offset, buffer.Length);

			return buffer;

		}

	#region File Info Creation Methods

		public abstract IUserWalletFileInfo CreateWalletFileInfo();

		public WalletKeyFileInfo CreateWalletKeysFileInfo<KEY_TYPE>(IWalletAccount account, string keyName, byte ordinalId, WalletPassphraseDetails walletPassphraseDetails, AccountPassphraseDetails accountPassphraseDetails)
			where KEY_TYPE : IWalletKey {

			return new WalletKeyFileInfo(account, keyName, ordinalId, typeof(KEY_TYPE), this.GetWalletKeysFilePath(account.AccountCode, keyName), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, accountPassphraseDetails, walletPassphraseDetails);
		}

		public WalletKeyLogFileInfo CreateWalletKeyLogFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails) {

			return new WalletKeyLogFileInfo(account, this.GetWalletKeyLogPath(account.AccountCode), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, walletPassphraseDetails);
		}

		public WalletKeyHistoryFileInfo CreateWalletKeyHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails) {
			return new WalletKeyHistoryFileInfo(account, this.GetWalletKeyHistoryPath(account.AccountCode), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, walletPassphraseDetails);
		}

		public WalletChainStateFileInfo CreateWalletChainStateFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails) {

			return new WalletChainStateFileInfo(account, this.GetWalletChainStatePath(account.AccountCode), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, walletPassphraseDetails);
		}
		
		public abstract IWalletGenerationCacheFileInfo CreateWalletGenerationCacheFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		public abstract IWalletTransactionHistoryFileInfo CreateWalletTransactionHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		public abstract IWalletElectionsHistoryFileInfo CreateWalletElectionsHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		public abstract IWalletElectionsStatisticsFileInfo CreateWalletElectionsStatisticsFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);

		public virtual WalletElectionCacheFileInfo CreateWalletElectionCacheFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails) {

			return new WalletElectionCacheFileInfo(account, this.GetWalletElectionCachePath(account.AccountCode), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, walletPassphraseDetails);

		}

		public abstract IWalletAccountSnapshotFileInfo CreateWalletSnapshotFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);

	#endregion

	}
}