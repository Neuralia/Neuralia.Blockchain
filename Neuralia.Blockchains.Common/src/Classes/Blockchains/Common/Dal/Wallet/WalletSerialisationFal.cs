using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.IO;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface IWalletSerialisationFal {

		WalletSerializationTransactionalLayer TransactionalFileSystem { get; }
		string GetWalletFilePath();
		string GetChainStorageFilesPath();
		string GetWalletFolderPath();
		string GetWalletCryptoFilePath();
		string GetWalletAccountsFolderPath(Guid AccountUuid);
		string GetWalletKeysFilePath(Guid AccountUuid, string name);
		string GetWalletKeyLogPath(Guid accountUuid);
		string GetWalletChainStatePath(Guid accountUuid);

		/// <summary>
		///     check the wallet file to know if it is encrypted
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		bool IsFileWalleteEncrypted();

		/// <summary>
		///     Add the encrypted marker to the encrypted bytes
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		SafeArrayHandle WrapEncryptedBytes(SafeArrayHandle buffer);

		SafeArrayHandle RunDbOperation(Action<LiteDBDAL> operation, SafeArrayHandle databaseBytes);
		(SafeArrayHandle newBytes, T result) RunDbOperation<T>(Func<LiteDBDAL, T> operation, SafeArrayHandle databaseBytes);

		T RunQueryDbOperation<T>(Func<LiteDBDAL, T> operation, SafeArrayHandle databaseBytes);
		void SaveFile(string filename, SafeArrayHandle databaseBytes, EncryptionInfo encryptionInfo, bool wrapEncryptedBytes);
		SafeArrayHandle LoadFile(string filename, EncryptionInfo encryptionInfo, bool wrappedEncryptedBytes);
		IUserWalletFileInfo CreateWalletFileInfo();

		WalletKeyFileInfo CreateWalletKeysFileInfo<KEY_TYPE>(IWalletAccount account, string keyName, byte ordinalId, WalletPassphraseDetails walletPassphraseDetails, AccountPassphraseDetails accountPassphraseDetails)
			where KEY_TYPE : IWalletKey;

		WalletKeyLogFileInfo CreateWalletKeyLogFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		WalletChainStateFileInfo CreateWalletChainStateFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		IWalletTransactionCacheFileInfo CreateWalletTransactionCacheFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		IWalletTransactionHistoryFileInfo CreateWalletTransactionHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		IWalletElectionsHistoryFileInfo CreateWalletElectionsHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		WalletElectionCacheFileInfo CreateWalletElectionCacheFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		WalletKeyHistoryFileInfo CreateWalletKeyHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);

		IWalletAccountSnapshotFileInfo CreateWalletSnapshotFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		bool WalletKeyFileExists(Guid AccountUuid, string name);

		WalletSerializationTransaction BeginTransaction();

		void CommitTransaction();

		void RollbackTransaction();

		(string path, string salt, int iterations) BackupWallet(string passphrase);
	}

	public abstract class WalletSerialisationFal<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWalletSerialisationFal
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public const string WALLET_FOLDER_PATH = "wallet";

		public const string BACKUP_FOLDER_PATH = "backups";

		public const string WALLET_FILE_NAME = "wallet.neuralia";

		public const string STORAGE_FOLDER_NAME = "system";

		public const string WALLET_CRYPTO_FILE_NAME = "wallet.crypto.neuralia";

		public const string WALLET_KEYS_FILE_NAME = "{0}.key";

		public const string ACCOUNTS_FOLDER_PATH = "accounts";

		public const string ACCOUNTS_KEYS_FOLDER_PATH = "keys";

		public const string WALLET_KEY_HISTORY_FILE_NAME = "KeyHistory.neuralia";
		public const string WALLET_KEYLOG_FILE_NAME = "KeyLog.neuralia";
		public const string WALLET_CHAINSTATE_FILE_NAME = "ChainState.neuralia";
		public const string WALLET_TRANSACTION_CACHE_FILE_NAME = "TransactionCache.neuralia";
		public const string WALLET_TRANSACTION_HISTORY_FILE_NAME = "TransactionHistory.neuralia";
		public const string WALLET_ELECTIONS_HISTORY_FILE_NAME = "ElectionsHistory.neuralia";
		public const string WALLET_ELECTION_CACHE_FILE_NAME = "ElectionCache.neuralia";
		public const string WALLET_ACCOUNT_SNAPSHOT_FILE_NAME = "AccountSnapshot.neuralia";

		private const long ENCRYPTED_WALLET_TAG = 0x5555555555555555L;

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		protected readonly string ChainFilesDirectoryPath;

		protected readonly bool compressFiles;

		protected readonly ICompression walletCompressor;
		
		public WalletSerialisationFal(CENTRAL_COORDINATOR centralCoordinator, string chainFilesDirectoryPath, IFileSystem fileSystem) {
			this.ChainFilesDirectoryPath = chainFilesDirectoryPath;

			var exclusions = new List<(string name, WalletSerializationTransactionalLayer.FilesystemTypes type)>(new[] {("events", WalletSerializationTransactionalLayer.FilesystemTypes.Folder)});
			this.TransactionalFileSystem = new WalletSerializationTransactionalLayer(this.GetWalletFolderPath(), exclusions, fileSystem);

			this.centralCoordinator = centralCoordinator;
			this.compressFiles = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.CompressWallet;

			if(this.compressFiles) {
				this.walletCompressor = new BrotliCompression();
			} else {
				this.walletCompressor = new NullCompression();
			}
		}

		public WalletSerializationTransactionalLayer TransactionalFileSystem { get; }

		public WalletSerializationTransaction BeginTransaction() {
			return this.TransactionalFileSystem.BeginTransaction();
		}

		public void CommitTransaction() {
			this.TransactionalFileSystem.CommitTransaction();
		}

		public void RollbackTransaction() {
			this.TransactionalFileSystem.RollbackTransaction();
		}

		public string GetChainStorageFilesPath() {
			return Path.Combine(this.ChainFilesDirectoryPath, STORAGE_FOLDER_NAME);
		}

		public virtual string GetWalletFilePath() {

			return Path.Combine(this.GetWalletFolderPath(), WALLET_FILE_NAME);
		}

		public virtual string GetWalletCryptoFilePath() {
			return Path.Combine(this.GetWalletFolderPath(), WALLET_CRYPTO_FILE_NAME);
		}

		public virtual string GetWalletAccountsFolderPath(Guid AccountUuid) {
			return Path.Combine(Path.Combine(this.GetWalletFolderPath(), ACCOUNTS_FOLDER_PATH), AccountUuid.ToString());
		}

		public virtual string GetWalletKeysFilePath(Guid AccountUuid, string name) {
			return Path.Combine(this.GetWalletAccountsKeysFolderPath(AccountUuid), string.Format(WALLET_KEYS_FILE_NAME, name));
		}

		public virtual string GetWalletKeyLogPath(Guid accountUuid) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountUuid), WALLET_KEYLOG_FILE_NAME);
		}

		public virtual string GetWalletChainStatePath(Guid accountUuid) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountUuid), WALLET_CHAINSTATE_FILE_NAME);
		}

		/// <summary>
		///     check the wallet file to know if it is encrypted
		/// </summary>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public bool IsFileWalleteEncrypted() {
			string walletFile = this.GetWalletFilePath();
			string walletCryptoFile = null;

			if(!this.TransactionalFileSystem.DirectoryExists(this.GetWalletFolderPath())) {
				Log.Information($"Creating new wallet baseFolder in path: {this.GetWalletFolderPath()}");
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
		public SafeArrayHandle WrapEncryptedBytes(SafeArrayHandle buffer) {
			//TODO: might be faster to avoid the copy and simply write the bytes directly
			SafeArrayHandle completeEncryptedFile = ByteArray.Create(buffer.Length + sizeof(long));

			byte[] tagBytes = new byte[sizeof(long)];
			TypeSerializer.Serialize(ENCRYPTED_WALLET_TAG, tagBytes);
			
			Buffer.BlockCopy(tagBytes, 0, completeEncryptedFile.Bytes, completeEncryptedFile.Offset, sizeof(long));
			Buffer.BlockCopy(buffer.Bytes, buffer.Offset, completeEncryptedFile.Bytes, sizeof(long), buffer.Length);

			return completeEncryptedFile;
		}

		public SafeArrayHandle RunDbOperation(Action<LiteDBDAL> operation, SafeArrayHandle databaseBytes) {
			using(RecyclableMemoryStream walletStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("dbstream")) {
				if(databaseBytes.HasData) {
					walletStream.Write(databaseBytes.Bytes, databaseBytes.Offset, databaseBytes.Length);
					walletStream.Position = 0;
				}

				LiteDBDAL litedbDal = LiteDBDAL.GetLiteDBDAL(walletStream);

				operation?.Invoke(litedbDal);

				SafeArrayHandle result = ByteArray.Create(walletStream);

				walletStream.ClearStream();

				return result;
			}
		}

		public (SafeArrayHandle newBytes, T result) RunDbOperation<T>(Func<LiteDBDAL, T> operation, SafeArrayHandle databaseBytes) {

			T result = default;

			SafeArrayHandle newbytes = this.RunDbOperation(LiteDBDAL => {

				result = operation(LiteDBDAL);
			}, databaseBytes);

			return (newbytes, result);
		}

		public T RunQueryDbOperation<T>(Func<LiteDBDAL, T> operation, SafeArrayHandle databaseBytes) {
			using(RecyclableMemoryStream walletStream = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("dbstream")) {
				if(databaseBytes.IsEmpty) {
					throw new ApplicationException("database bytes can not be null");
				}

				walletStream.Write(databaseBytes.Bytes, databaseBytes.Offset, databaseBytes.Length);
				walletStream.Position = 0;

				LiteDBDAL litedbDal = LiteDBDAL.GetLiteDBDAL(walletStream);

				T result = operation(litedbDal);

				walletStream.ClearStream();

				return result;
			}
		}

		public void SaveFile(string filename, SafeArrayHandle databaseBytes, EncryptionInfo encryptionInfo, bool wrapEncryptedBytes) {
			try {

				if(databaseBytes.IsEmpty) {
					throw new ApplicationException("Cannot save null database data");
				}

				string directory = this.TransactionalFileSystem.GetDirectoryName(filename);

				if(!this.TransactionalFileSystem.DirectoryExists(directory)) {
					Log.Information($"Creating new baseFolder in path: {directory}");
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

						encryptedBytes = FileEncryptor.Encrypt(activeBytes, encryptionInfo.Secret(), encryptionInfo.EncryptionParameters);

						if(wrapEncryptedBytes) {
							// wrap the encrypted byes with the flag marker
							SafeArrayHandle wrappedBytes = this.WrapEncryptedBytes(encryptedBytes);
							encryptedBytes.Entry.Clear();
							encryptedBytes.Return();
							encryptedBytes = wrappedBytes;
						}

						// create or overwrite the file
						this.TransactionalFileSystem.OpenWrite(filename, encryptedBytes);

					} else {
						if(this.compressFiles) {
							compressedBytes = this.walletCompressor.Compress(databaseBytes);
							activeBytes = compressedBytes;
						} else {
							activeBytes = databaseBytes;
						}

						this.TransactionalFileSystem.OpenWrite(filename, activeBytes);
					}
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
				Log.Verbose("error occured", e);

				throw;
			}
		}

		public SafeArrayHandle LoadFile(string filename, EncryptionInfo encryptionInfo, bool wrappedEncryptedBytes) {
			try {

				if(!this.TransactionalFileSystem.FileExists(filename)) {
					throw new FileNotFoundException("file does not exist");
				}

				string directory = Path.GetDirectoryName(filename);

				if(!this.TransactionalFileSystem.DirectoryExists(directory)) {
					Log.Information($"Creating new baseFolder in path: {directory}");
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
						encryptedWalletFileBytes = ByteArray.WrapAndOwn(this.TransactionalFileSystem.ReadAllBytes(filename));
					}

					using(SafeArrayHandle decryptedWalletBytes = FileEncryptor.Decrypt(encryptedWalletFileBytes, encryptionInfo.Secret(), encryptionInfo.EncryptionParameters)) {

						if(!this.compressFiles) {
							return decryptedWalletBytes.Branch();
						}
						return this.walletCompressor.Decompress(decryptedWalletBytes);
					}
				}

				using(SafeArrayHandle walletSimpleBytes = ByteArray.WrapAndOwn(this.TransactionalFileSystem.ReadAllBytes(filename))) {

					if(!this.compressFiles) {
						return walletSimpleBytes.Branch();
					}
					return this.walletCompressor.Decompress(walletSimpleBytes);
				}

			} catch(Exception e) {
				Log.Verbose("error occured", e);

				throw;
			}
		}

		/// <summary>
		///     tells us if a wallet key file exists
		/// </summary>
		/// <param name="AccountUuid"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public bool WalletKeyFileExists(Guid AccountUuid, string name) {
			string walletKeysFile = this.GetWalletKeysFilePath(AccountUuid, name);

			return this.TransactionalFileSystem.FileExists(walletKeysFile);
		}

		public virtual string GetWalletFolderPath() {

			return Path.Combine(this.ChainFilesDirectoryPath, WALLET_FOLDER_PATH);
		}

		/// <summary>
		///     here we make a backup of all our wallet files, encrypt it and return it's path
		/// </summary>
		/// <param name="passphrase"></param>
		/// <returns></returns>
		public (string path, string salt, int iterations) BackupWallet(string passphrase) {

			// first, let's generate a passphrase
			string walletPath = this.GetWalletFolderPath();

			string backupsPath = this.GetWalletFolderPath();

			FileExtensions.EnsureDirectoryStructure(backupsPath, this.centralCoordinator.FileSystem);

			string zipFile = Path.Combine(backupsPath, "temp.zip");
			string resultsFile = Path.Combine(backupsPath, $"backup.{DateTime.UtcNow:yyyy-dd-M--HH-mm-ss}.neuralia");

			if(this.centralCoordinator.FileSystem.File.Exists(zipFile)) {
				this.centralCoordinator.FileSystem.File.Delete(zipFile);
			}

			if(this.centralCoordinator.FileSystem.File.Exists(resultsFile)) {
				this.centralCoordinator.FileSystem.File.Delete(resultsFile);
			}

			ZipFile.CreateFromDirectory(walletPath, zipFile);

			XChachaEncryptorParameters encryptionParameters = XChaChaFileEncryptor.GenerateEncryptionParameters(13);

			XChaChaFileEncryptor encryptor = new XChaChaFileEncryptor(encryptionParameters);

			SafeArrayHandle passwordBytes = ByteArray.WrapAndOwn(Encoding.UTF8.GetBytes(passphrase.ToUpper()));
			SafeArrayHandle fileBytes = FileExtensions.ReadAllBytes(zipFile, this.centralCoordinator.FileSystem);
			SafeArrayHandle encrypted = encryptor.Encrypt(fileBytes, passwordBytes);

			FileExtensions.WriteAllBytes(resultsFile, encrypted, this.centralCoordinator.FileSystem);

			fileBytes.Return();
			passwordBytes.Return();
			encrypted.Return();

			if(this.centralCoordinator.FileSystem.File.Exists(zipFile)) {
				this.centralCoordinator.FileSystem.File.Delete(zipFile);
			}

			return (resultsFile, encryptionParameters.Salt.Entry.ToBase58(), encryptionParameters.Iterations);
		}

		public virtual string GetWalletAccountsContentsFolderPath(Guid AccountUuid) {
			return this.GetWalletAccountsFolderPath(AccountUuid);
		}

		public virtual string GetWalletAccountsKeysFolderPath(Guid AccountUuid) {
			return Path.Combine(this.GetWalletAccountsFolderPath(AccountUuid), ACCOUNTS_KEYS_FOLDER_PATH);
		}

		public virtual string GetWalletKeyHistoryPath(Guid accountUuid) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountUuid), WALLET_KEY_HISTORY_FILE_NAME);
		}

		public virtual string GetBackupsFolderPath() {

			return Path.Combine(this.ChainFilesDirectoryPath, BACKUP_FOLDER_PATH);
		}

		public virtual string GetWalletTransactionCachePath(Guid accountUuid) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountUuid), WALLET_TRANSACTION_CACHE_FILE_NAME);
		}

		public virtual string GetWalletTransactionHistoryPath(Guid accountUuid) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountUuid), WALLET_TRANSACTION_HISTORY_FILE_NAME);
		}

		public virtual string GetWalletElectionsHistoryPath(Guid accountUuid) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountUuid), WALLET_ELECTIONS_HISTORY_FILE_NAME);
		}

		public virtual string GetWalletElectionCachePath(Guid accountUuid) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountUuid), WALLET_ELECTION_CACHE_FILE_NAME);
		}

		public virtual string GetWalletAccountSnapshotPath(Guid accountUuid) {
			return Path.Combine(this.GetWalletAccountsContentsFolderPath(accountUuid), WALLET_ACCOUNT_SNAPSHOT_FILE_NAME);
		}

		/// <summary>
		///     this method will read the first 8 bytes of the wallet file and determine if it is encrypted.
		/// </summary>
		/// <param name="walletFile"></param>
		/// <returns></returns>
		private bool CheckWalletEncrypted(string walletFile) {
			using(Stream stream = this.TransactionalFileSystem.OpenRead(walletFile)) {
				if(stream.Length == 0) {
					throw new ApplicationException("Wallet size cannot be 0.");
				}

				using(ByteArray buffer = ByteArray.Create(sizeof(long))) {
					stream.Read(buffer.Bytes, buffer.Offset, buffer.Length);

					TypeSerializer.Deserialize(buffer.Span, out long tag);
					return tag == ENCRYPTED_WALLET_TAG;
				}
			}
		}

		/// <summary>
		///     Get the valuable bytes of an encrtypted wallet file (skip the initial marker tag)
		/// </summary>
		/// <param name="walletFile"></param>
		/// <returns></returns>
		private SafeArrayHandle GetEncryptedWalletFileBytes(string walletFile) {

			using(Stream fs = this.TransactionalFileSystem.OpenRead(walletFile)) {
				fs.Position = sizeof(long); // skip the marker

				using(ByteArray buffer = ByteArray.Create((int) fs.Length - sizeof(long))) {

					fs.Read(buffer.Bytes, buffer.Offset, buffer.Length);

					return buffer;
				}
			}
		}

	#region File Info Creation Methods

		public abstract IUserWalletFileInfo CreateWalletFileInfo();

		public WalletKeyFileInfo CreateWalletKeysFileInfo<KEY_TYPE>(IWalletAccount account, string keyName, byte ordinalId, WalletPassphraseDetails walletPassphraseDetails, AccountPassphraseDetails accountPassphraseDetails)
			where KEY_TYPE : IWalletKey {

			return new WalletKeyFileInfo(account, keyName, ordinalId, typeof(KEY_TYPE), this.GetWalletKeysFilePath(account.AccountUuid, keyName), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, accountPassphraseDetails, walletPassphraseDetails);
		}

		public WalletKeyLogFileInfo CreateWalletKeyLogFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails) {

			return new WalletKeyLogFileInfo(account, this.GetWalletKeyLogPath(account.AccountUuid), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, walletPassphraseDetails);
		}

		public WalletKeyHistoryFileInfo CreateWalletKeyHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails) {
			return new WalletKeyHistoryFileInfo(account, this.GetWalletKeyHistoryPath(account.AccountUuid), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, walletPassphraseDetails);
		}

		public WalletChainStateFileInfo CreateWalletChainStateFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails) {

			return new WalletChainStateFileInfo(account, this.GetWalletChainStatePath(account.AccountUuid), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, walletPassphraseDetails);
		}

		public abstract IWalletTransactionCacheFileInfo CreateWalletTransactionCacheFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		public abstract IWalletTransactionHistoryFileInfo CreateWalletTransactionHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);
		public abstract IWalletElectionsHistoryFileInfo CreateWalletElectionsHistoryFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);

		public virtual WalletElectionCacheFileInfo CreateWalletElectionCacheFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails) {

			return new WalletElectionCacheFileInfo(account, this.GetWalletElectionCachePath(account.AccountUuid), this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet, this, walletPassphraseDetails);

		}

		public abstract IWalletAccountSnapshotFileInfo CreateWalletSnapshotFileInfo(IWalletAccount account, WalletPassphraseDetails walletPassphraseDetails);

	#endregion

	}
}