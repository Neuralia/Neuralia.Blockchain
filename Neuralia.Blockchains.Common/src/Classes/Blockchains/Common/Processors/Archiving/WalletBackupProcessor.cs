using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.Archiving {
	public interface IWalletBackupProcessor {
		
		Task<WalletBackupProcessor.WalletBackupInfo> BackupWallet(WalletProvider.BackupTypes backupType, LockContext lockContext);
		Task<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, string nonce, int iterations, LockContext lockContext);
	}
	
	public class WalletBackupProcessor : IWalletBackupProcessor {

		private enum BackupTypes {
			Copy,
			Essence,
			SuperKey
		}
		
		private enum SubBackupTypes {
			Full,
			Partial
		}
		
		private readonly FileSystemWrapper fileSystem;
		protected readonly string walletFolderPath;
		protected FileSystemWrapper FileSystem => this.fileSystem;
		private readonly IWalletProviderInternal walletProvider;
		
		public WalletBackupProcessor(IWalletProviderInternal walletProvider, FileSystemWrapper fileSystem, string walletFolderPath) {
			this.fileSystem = fileSystem;
			this.walletFolderPath = walletFolderPath;
			this.walletProvider = walletProvider;
		}

		private string GetManifestFilePath() {
			return this.GetManifestFilePath(this.walletFolderPath);
		}
		
		private string GetManifestFilePath(string folderPath) {
			return Path.Combine(folderPath, "backup.manifest");
		}

	#region Backup
		/// <summary>
		///     here we make a backup of all our wallet files, encrypt it and return it's path
		/// </summary>
		/// <param name="passphrase"></param>
		/// <returns></returns>
		public async Task<WalletBackupInfo> BackupWallet(WalletProvider.BackupTypes backupType, LockContext lockContext) {
			// first, let's get the wallet and backup paths
			string backupsPath = Path.Combine(GlobalSettings.ApplicationSettings.SystemFilesPath, "backup");
			string tempPath = Path.Combine(GlobalSettings.ApplicationSettings.SystemFilesPath, "temp");
			string workingFolderPath = Path.Combine(tempPath, "backupWorkingFolder");
			
			try {
				FileExtensions.EnsureDirectoryStructure(backupsPath, this.FileSystem);

				string zipFile = Path.Combine(backupsPath, "tempBackup.zip");
				string resultsFile = Path.Combine(backupsPath, $"backup.{DateTimeEx.CurrentTime:yyyy-dd-M--HH-mm-ss}.neuralia");

				if(this.FileSystem.FileExists(zipFile)) {
					await SecureWipe.WipeFile(zipFile, this.fileSystem).ConfigureAwait(false);
				}

				if(this.FileSystem.FileExists(resultsFile)) {
					await SecureWipe.WipeFile(resultsFile, this.fileSystem).ConfigureAwait(false);
				}
				
				// add the manifest
				BackupTypes selectedBackupType = BackupTypes.Copy;
				SubBackupTypes backupSubtype = SubBackupTypes.Full;
				int major = 1;
				int minor = 1;
				if(backupType == WalletProvider.BackupTypes.CopyFull) {
					selectedBackupType = BackupTypes.Copy;
					backupSubtype = SubBackupTypes.Full;
				}
				else if(backupType == WalletProvider.BackupTypes.SuperKey) {
					selectedBackupType = BackupTypes.SuperKey;
				}
				var manifest = new WalletBackupManifest(){Major = major, Minor = minor, Type = selectedBackupType, SubType = backupSubtype};

				string selectedWorkingDirectory = workingFolderPath;
				
				FileExtensions.EnsureDirectoryStructure(workingFolderPath, this.fileSystem);

				Func<Task> finalAction = null;
				bool transformed = false;
				if(manifest.Type == BackupTypes.Copy) {
					if(manifest.SubType == SubBackupTypes.Full) {
						if(manifest.Major == 1) {
							(selectedWorkingDirectory, finalAction) = await this.PerformCopyFullBackupV1(workingFolderPath, manifest.Minor).ConfigureAwait(false);
							transformed = true;
						}
					}
					else if(manifest.SubType == SubBackupTypes.Partial) {
						//TODO: implement this
					}
				}
				else if(manifest.Type == BackupTypes.Essence) {
					//TODO: implement this
				}
				else if(manifest.Type == BackupTypes.SuperKey) {
					(selectedWorkingDirectory, finalAction) = await this.PerformSuperKeyBackupV1(workingFolderPath, manifest.Minor, lockContext).ConfigureAwait(false);
					transformed = true;
				}

				if(!transformed) {
					throw new NotSupportedException();
				}

				string backupManifestPath = this.GetManifestFilePath(selectedWorkingDirectory);
				FileExtensions.WriteAllText(backupManifestPath, System.Text.Json.JsonSerializer.Serialize(manifest), this.FileSystem);

				ZipFile.CreateFromDirectory(selectedWorkingDirectory, zipFile);
				
				this.FileSystem.DeleteFile(backupManifestPath);

				using ChaCha20Poly1305EncryptorParameters encryptionParameters = ChaCha20Poly1305FileEncryptor.GenerateEncryptionParameters(EncryptorParameters.SymetricCiphers.XCHACHA_20_POLY_1305, null, 40);
				
				using ChaCha20Poly1305FileEncryptor encryptor = new ChaCha20Poly1305FileEncryptor(encryptionParameters);

				string passphrase = WordsGenerator.GenerateRandomPhrase(4);
				using (SafeArrayHandle passwordBytes = SafeArrayHandle.WrapAndOwn(Encoding.UTF8.GetBytes(passphrase.ToUpper(CultureInfo.InvariantCulture))))
				{
					using SafeArrayHandle fileBytes = FileExtensions.ReadAllBytes(zipFile, this.FileSystem);
					using SafeArrayHandle encrypted = encryptor.Encrypt(fileBytes, passwordBytes);
					FileExtensions.WriteAllBytes(resultsFile, encrypted, this.FileSystem);
				}

				if(this.FileSystem.FileExists(zipFile)) {
					await SecureWipe.WipeFile(zipFile, this.fileSystem).ConfigureAwait(false);
				}

				var walletBackupInfo = new WalletBackupInfo();
				walletBackupInfo.Path = resultsFile;
				walletBackupInfo.Passphrase = passphrase;
				walletBackupInfo.Salt = encryptionParameters.Salt.ToBase32();
				walletBackupInfo.Nonce = encryptionParameters.Nonce.ToBase32();
				walletBackupInfo.Iterations = encryptionParameters.Iterations;

				if(finalAction != null) {
					await finalAction().ConfigureAwait(false);
				}
				return walletBackupInfo;
			} finally {
				//Let's cleanup old backups to not take hard-disk spaces for nothing. We'll keep the 5 youngests.
				IEnumerable<FileEntry> backupEntries = this.FileSystem.EnumerateFileEntries(backupsPath).Where(p => p.ExtensionWithDot.Equals(".neuralia", StringComparison.OrdinalIgnoreCase));

				try {
					if(backupEntries.Count() > 5) {
						IEnumerable<FileEntry> expiredEntries = backupEntries.OrderByDescending(p => p.CreationTime).Skip(5);
					
						foreach(FileEntry entry in expiredEntries) {
							await SecureWipe.WipeFile(entry, this.fileSystem, 20).ConfigureAwait(false);
						}
					}
				} catch {
					//TODO: what to do here?
				}
				
				try {
					await SecureWipe.WipeDirectory(workingFolderPath, this.fileSystem, 20).ConfigureAwait(false);
				} catch {
					//TODO: what to do here?
				}
			}
		}

		/// <summary>
		/// perform a full copy of the entire folder as is.
		/// </summary>
		/// <param name="workingFolder"></param>
		/// <param name="minor"></param>
		/// <returns></returns>
		private Task<(string usingWorkingPath, Func<Task> finalize)> PerformCopyFullBackupV1(string workingFolder, int minor) {
			
			// in this case we dont do anything since we work directly from the original folder
			return Task.FromResult((this.walletFolderPath, (Func<Task>)null));
		}
		
		/// <summary>
		/// here we backup the super in 2 formats and then we delete the originals
		/// </summary>
		/// <param name="workingFolder"></param>
		/// <param name="minor"></param>
		/// <param name="lockContext"></param>
		/// <returns></returns>
		private async Task<(string usingWorkingPath, Func<Task> finalize)> PerformSuperKeyBackupV1(string workingFolder, int minor, LockContext lockContext) {

			var accounts = await this.walletProvider.GetAccounts(lockContext).ConfigureAwait(false);
			foreach(var account in accounts) {
				await this.walletProvider.EnsureWalletKeyIsReady(account.AccountCode, GlobalsService.SUPER_KEY_NAME, lockContext).ConfigureAwait(false);
				using var superKey = await this.walletProvider.LoadKey<IXmssWalletKey>(account.AccountCode, GlobalsService.SUPER_KEY_NAME, lockContext).ConfigureAwait(false);

				string keyText = superKey.ExportKey();
				FileExtensions.WriteAllText(Path.Combine(workingFolder, $"{account.AccountCode}.json"), keyText, this.fileSystem);

				WalletKeyFileInfo walletKeyInfo = this.walletProvider.WalletFileInfo.Accounts[account.AccountCode].WalletKeysFileInfo[GlobalsService.SUPER_KEY_NAME];

				var keyInfo = account.Keys.Single(k => k.Name == GlobalsService.SUPER_KEY_NAME);

				if(keyInfo.KeyEncrypted) {
					using var dehydrator = DataSerializationFactory.CreateDehydrator();

					keyInfo.EncryptionParameters.Dehydrate(dehydrator);
					using var encryptionParameterBytes = dehydrator.ToArray();
					FileExtensions.WriteAllBytes(Path.Combine(workingFolder, $"{account.AccountCode}.epm"), encryptionParameterBytes, this.fileSystem);
				}
				string destination = Path.Combine(workingFolder, $"{account.AccountCode}.key");
				this.fileSystem.CopyFile(walletKeyInfo.Filename, destination, true);
			}
			
			return (workingFolder, async () => {
					       // now delete the keys
					       foreach(var account in accounts) {
						       WalletKeyFileInfo walletKeyInfo = this.walletProvider.WalletFileInfo.Accounts[account.AccountCode].WalletKeysFileInfo[GlobalsService.SUPER_KEY_NAME];

						       await SecureWipe.WipeFile(walletKeyInfo.Filename, this.fileSystem).ConfigureAwait(false);
					       }
				       });
		}
		
	#endregion
	#region Restore
		/// <summary>
		///     Here we restore our wallet from a backup made with <see cref="BackupWallet" />.>
		/// </summary>
		/// <param name="backupPath"></param>
		/// <param name="passphrase"></param>
		/// <param name="salt"></param>
		/// <param name="iterations"></param>
		/// <returns></returns>
		public async Task<bool> RestoreWalletFromBackup(string backupsPath, string passphrase, string salt, string nonce, int iterations,LockContext lockContext) {

			// first, let's get the required paths

			string tempPath = Path.Combine(GlobalSettings.ApplicationSettings.SystemFilesPath, "temp");
			string zipFile = Path.Combine(tempPath, "tempRestore.zip");
			string tempRestoredWalletFolder = Path.Combine(tempPath, "restoredWallet");

			try {
				FileExtensions.EnsureDirectoryStructure(tempPath, this.FileSystem);

				if(this.FileSystem.FileExists(zipFile)) {
					await SecureWipe.WipeFile(zipFile, this.fileSystem).ConfigureAwait(false);
				}

				using var saltBytes = SafeArrayHandle.FromBase32(salt);
				using var nonceBytes = SafeArrayHandle.FromBase32(nonce);
				using ChaCha20Poly1305EncryptorParameters decryptionParameters = ChaCha20Poly1305FileEncryptor.GenerateEncryptionParameters( EncryptorParameters.SymetricCiphers.XCHACHA_20_POLY_1305, saltBytes, nonceBytes, iterations);

				using ChaCha20Poly1305FileEncryptor decryptor = new ChaCha20Poly1305FileEncryptor(decryptionParameters);
				
				using (SafeArrayHandle passwordBytes = SafeArrayHandle.WrapAndOwn(Encoding.UTF8.GetBytes(passphrase.ToUpper(CultureInfo.InvariantCulture))))
				{
					using SafeArrayHandle fileBytes = FileExtensions.ReadAllBytes(backupsPath, this.FileSystem);
					using SafeArrayHandle decrypted = decryptor.Decrypt(fileBytes, passwordBytes);

					FileExtensions.WriteAllBytes(zipFile, decrypted, this.FileSystem);

					if (this.FileSystem.DirectoryExists(tempRestoredWalletFolder))
					{
						await SecureWipe.WipeDirectory(tempRestoredWalletFolder, this.fileSystem).ConfigureAwait(false);
					}

					FileExtensions.EnsureDirectoryStructure(tempRestoredWalletFolder, this.FileSystem);
					ZipFile.ExtractToDirectory(zipFile, tempRestoredWalletFolder);
				}

				string backupManifestPath = this.GetManifestFilePath(tempRestoredWalletFolder);

				if(!this.FileSystem.FileExists(backupManifestPath)) {
					throw new ApplicationException("Backup manifest did not exist in file!");
				}

				string json = FileExtensions.ReadAllText(backupManifestPath, this.FileSystem);
				WalletBackupManifest manifest = System.Text.Json.JsonSerializer.Deserialize<WalletBackupManifest>(json);
				this.FileSystem.DeleteFile(backupManifestPath);

				string oldWallet = Path.ChangeExtension(this.walletFolderPath, ".old");

				try {
					if(this.FileSystem.DirectoryExists(this.walletFolderPath)) {
						this.FileSystem.MoveDirectory(this.walletFolderPath, oldWallet);
					}

					bool restored = false;
					if(manifest.Type == BackupTypes.Copy) {
                    	if(manifest.SubType == SubBackupTypes.Full) {
	                        if(manifest.Major == 1) {
		                        await this.PerformCopyFullRestoreV1(tempRestoredWalletFolder, manifest.Minor).ConfigureAwait(false);
		                        restored = true;
	                        }
                        }
                        else if(manifest.SubType == SubBackupTypes.Partial) {
	                        //TODO: implement this
                        }
                    }
					else if(manifest.Type == BackupTypes.Essence) {
						//TODO: implement this
					}
					else if(manifest.Type == BackupTypes.SuperKey) {
						//TODO: implement this
					}

					if(!restored) {
						throw new NotSupportedException();
					}

				} catch(Exception ex) {
					//Restore the wallet if an exception occured during the moving.
					if(this.FileSystem.DirectoryExists(oldWallet)) {
						if(this.FileSystem.DirectoryExists(this.walletFolderPath)) {
							await SecureWipe.WipeDirectory(this.walletFolderPath, this.fileSystem).ConfigureAwait(false);
						}

						this.FileSystem.MoveDirectory(oldWallet, this.walletFolderPath);
					}

					throw;
				} finally {
					if(this.FileSystem.DirectoryExists(oldWallet)) {
						await SecureWipe.WipeFile(oldWallet, this.fileSystem).ConfigureAwait(false);
					}
				}

				return true;
			} finally {
				if(this.FileSystem.FileExists(zipFile)) {
					await SecureWipe.WipeFile(zipFile, this.fileSystem).ConfigureAwait(false);
				}

				if(this.FileSystem.DirectoryExists(tempRestoredWalletFolder)) {
					await SecureWipe.WipeDirectory(tempRestoredWalletFolder, this.fileSystem).ConfigureAwait(false);
				}
			}
		}

		private async Task PerformCopyFullRestoreV1(string tempRestoredWalletFolder, int minor) {
			if(this.FileSystem.DirectoryExists(this.walletFolderPath)) {
				await SecureWipe.WipeDirectory(this.walletFolderPath, this.fileSystem).ConfigureAwait(false);
			}

			string parentFolder = Path.GetDirectoryName(this.walletFolderPath);
			FileExtensions.EnsureDirectoryStructure(parentFolder);
			this.FileSystem.MoveDirectory(tempRestoredWalletFolder, this.walletFolderPath);
		}
	#endregion

		public class WalletBackupInfo {
			public string Path{ get; set; }
			public string Passphrase{ get; set; }
			public string Salt{ get; set; }
			public string Nonce{ get; set; }
			public int Iterations{ get; set; }
		}
		
		private class WalletBackupManifest {
			public int Major { get; set; }
			public int Minor { get; set; }
			public BackupTypes Type { get; set; }
			public SubBackupTypes SubType { get; set; }
			public DateTime Timestamp { get; set; } = DateTimeEx.CurrentTime;
		}
	}
}