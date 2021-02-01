using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Extensions;
using Nito.AsyncEx.Synchronous;
using Serilog;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Transactions {
	/// <summary>
	///     this class allows us to build an abstraction over the wallet file system and build transactions. this way, we can
	///     accumulate changes in memory and commit them only when ready.
	///     when we do commit, the physical filesystem is replaced by the in memory one to match perfectly. hence, commiting
	///     changes
	/// </summary>
	/// <remarks>This class is very sensitive to file access and should be run inside file and thread locks.</remarks>
	public class WalletSerializationTransactionalLayer : IDisposableExtended {

		public enum FileOps {
			Created,
			Modified,
			Deleted
		}

		public enum FileStatuses {
			Untouched,
			Modified
		}

		public enum FilesystemTypes {
			File,
			Folder
		}

		private const string BACKUP_PATH_NAME = "backup";
		private readonly object cleanLocker = new object();

		private const uint HRFileLocked = 0x80070020;
		private const uint HRPortionOfFileLocked = 0x80070021;

		private readonly WrapperConcurrentQueue<Action> cleaningOperations = new WrapperConcurrentQueue<Action>();
		private Task cleaningTask;

		private readonly List<(string name, FilesystemTypes type)> exclusions;

		private readonly Dictionary<string, FileStatuses> fileStatuses = new Dictionary<string, FileStatuses>();
		private readonly HashSet<string> initialFileStructure = new HashSet<string>();

		protected readonly FileSystemWrapper physicalFileSystem;

		private readonly string walletsPath;

		//TODO: replace MemoryFileSystem by our own implementation. using it for now as it works for basic purposes
		protected FileSystemWrapper activeFileSystem;

		private WalletSerializationTransaction walletSerializationTransaction;

		protected readonly ICentralCoordinator centralCoordinator;
		public ICentralCoordinator CentralCoordinator => this.centralCoordinator;

		public string GetBackupPath() {
			return Path.Combine(this.walletsPath, BACKUP_PATH_NAME);
		}
		
		public WalletSerializationTransactionalLayer(ICentralCoordinator centralCoordinator, string walletsPath, List<(string name, FilesystemTypes type)> exclusions, FileSystemWrapper fileSystem) {
			// start with a physical filesystem
			this.physicalFileSystem = fileSystem ?? FileSystemWrapper.CreatePhysical();
			this.activeFileSystem = this.physicalFileSystem;
			this.centralCoordinator = centralCoordinator;
			this.walletsPath = walletsPath;
			this.exclusions = exclusions; 
		}

		public FileSystemWrapper FileSystem => this.activeFileSystem;

		private void Repeat(Action action) {
			Repeater.Repeat(action, 5, () => {
				Thread.Sleep(150);
			});
		}
		
		private Task RepeatAsync(Func<Task> action) {
			return Repeater.RepeatAsync(action, 5, () => {
				Thread.Sleep(150);
				
				return Task.CompletedTask;
			});
		}

		private void RepeatFileOperation(Action action) {

			int attempt = 1;

			Repeater.Repeat(() => {

				try {
					action();
				} catch(IOException e) {
					if(this.IsFileLocked(e)) {
						// file is locked, lets sleep a good amount of time then we try again
						int delay = Math.Min(200 * attempt, 1000);
						Thread.Sleep(delay);
						attempt++;
					}

					throw;
				}
			}, 5);
		}
		
		private Task RepeatFileOperationAsync(Func<Task> action) {

			int attempt = 1;

			return Repeater.RepeatAsync(async () => {

				try {
					await action().ConfigureAwait(false);
				} catch(IOException e) {
					if(this.IsFileLocked(e)) {
						// file is locked, lets sleep a good amount of time then we try again
						int delay = Math.Min(200 * attempt, 1000);
						Thread.Sleep(delay);
						attempt++;
					}

					throw;
				}
			}, 5);
		}

		private bool IsFileLocked(IOException ioex) {
			uint errorCode = (uint) Marshal.GetHRForException(ioex);

			return (errorCode == HRFileLocked) || (errorCode == HRPortionOfFileLocked);
		}

		private void AddCleaningTask(Action operation) {
			lock(this.cleanLocker) {
				this.cleaningOperations.Enqueue(operation);

				if(this.cleaningTask != null && this.cleaningTask.IsCompleted) {
					this.cleaningTask = null;
				}

				if(this.cleaningTask == null) {
					this.cleaningTask = Task.Run(() => {

						while(this.cleaningOperations.TryDequeue(out Action op)) {
							try {
								op();
							} catch (Exception ex){
								this.CentralCoordinator.Log.Debug(ex, "Error while running wallet cleaning action");
							}
						}
					});
				}
			}
		}

		private void ClearCleaningTasks() {

			try {
				lock(this.cleanLocker) {
					this.cleaningOperations.Clear();
					this.cleaningTask = null;
				}
			} catch(Exception ex) {
				// nothing to do
			}
		}

		private async Task WaitCleaningTasks() {
			this.ClearCleaningTasks();

			try {
				bool cleaning = false;

				lock(this.cleanLocker) {
					cleaning = this.cleaningTask != null && !this.cleaningTask.IsCompleted;
				}

				if(cleaning) {
					try {
						this.cleaningTask?.Wait(TimeSpan.FromSeconds(20));
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Error(ex, "Failed to process cleaning tasks.");
					} finally {
						this.cleaningTask = null;
					}
				}
			} finally {
				this.ClearCleaningTasks();
			}
		}

		public bool IsInTransaction => this.walletSerializationTransaction != null;
		
		public async Task<WalletSerializationTransaction> BeginTransaction() {
			await this.WaitCleaningTasks().ConfigureAwait(false);
			
			this.fileStatuses.Clear();
			this.initialFileStructure.Clear();

			if(this.IsInTransaction) {
				throw new ApplicationException("Transaction already in progress");
			}

			try {
				this.walletSerializationTransaction = new WalletSerializationTransaction(this);
				this.activeFileSystem = FileSystemWrapper.CreateMemory();
				
				await ImportFilesystems().ConfigureAwait(false);
				
				return this.walletSerializationTransaction;
			} catch {
				await this.RollbackTransaction().ConfigureAwait(false);

				throw;
			}
		}

		private Task DeleteFolderStructure(DirectoryEntry directory, FileSystemWrapper memoryFileSystem) {
			if(GlobalSettings.ApplicationSettings.WalletTransactionDeletionMode == AppSettingsBase.WalletTransactionDeletionModes.Safe) {
				return SafeDeleteDirectoryStructure(directory, memoryFileSystem);
			} else {
				return this.FastDeleteFolderStructure(directory, memoryFileSystem);
			}
		}

		public async Task CommitTransaction() {

			// wait for any remaining tasks if required
			await this.WaitCleaningTasks().ConfigureAwait(false);
			
			try {
				// this is important. let's try 3 times before we declare it a fail
				if(!await Repeater.RepeatAsync(this.CommitDirectoryChanges, 2, this.WaitCleaningTasks).ConfigureAwait(false)) {
					await this.RollbackTransaction().ConfigureAwait(false);
				}

			} catch(Exception ex) {
				await this.RollbackTransaction().ConfigureAwait(false);

				throw ex;
			}

			await this.walletSerializationTransaction.CommitTransaction().ConfigureAwait(false);
			this.walletSerializationTransaction = null;

			FileSystemWrapper memoryFileSystem = this.activeFileSystem;

			this.AddCleaningTask(() => this.DeleteFolderStructure(memoryFileSystem.GetDirectoryEntryUnconditional(this.walletsPath), memoryFileSystem));
			
			this.activeFileSystem = this.physicalFileSystem;
			this.fileStatuses.Clear();
			this.initialFileStructure.Clear();
		}

		public async Task RollbackTransaction() {
			this.ClearCleaningTasks();

			try {
				if(this.IsInTransaction) {
					await this.walletSerializationTransaction.RollbackTransaction().ConfigureAwait(false);
				}
			} finally {
				this.walletSerializationTransaction = null;

				FileSystemWrapper memoryFileSystem = this.activeFileSystem;

				// let all changes go, we release it all
				this.AddCleaningTask(() => this.DeleteFolderStructure(memoryFileSystem.GetDirectoryEntryUnconditional(this.walletsPath), memoryFileSystem));

				this.activeFileSystem = this.physicalFileSystem;
				this.fileStatuses.Clear();
				this.initialFileStructure.Clear();
			}
		}
		
		private async Task CommitDirectoryChanges() {
			//TODO: make this bullet proof
			if(!this.IsInTransaction) {
				return;
			}

			// copy directory structure
			DirectoryEntry activeDirectoryInfo = this.activeFileSystem.GetDirectoryEntryUnconditional(this.walletsPath);
			DirectoryEntry physicalDirectoryInfo = this.physicalFileSystem.GetDirectoryEntryUnconditional(this.walletsPath);
			string backupPath = this.GetBackupPath();

			List<FileOperationInfo> fileDeltas = new List<FileOperationInfo>();

			try {
				await this.BuildDelta(activeDirectoryInfo, physicalDirectoryInfo, fileDeltas).ConfigureAwait(false);

				if(fileDeltas.Any()) {
					await this.CreateSafetyBackup(backupPath, fileDeltas).ConfigureAwait(false);

					await this.ApplyFileDeltas(fileDeltas).ConfigureAwait(false);

					await this.CompleteFileChanges(fileDeltas).ConfigureAwait(false);

					await this.RepeatAsync(async () => {

						foreach(string file in Narballer.GetPackageFilesList(backupPath)) {
							FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file);

							if(fileInfo?.Exists ?? false) {
								await this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
							}
						}
					}).ConfigureAwait(false);
				}

			} catch(Exception ex) {
				// delete our temp work
				this.CentralCoordinator.Log.Error(ex, "Failed to write transaction data. Will attempt to undo");

				if(await this.UndoFileChanges(backupPath, fileDeltas).ConfigureAwait(false)) {
					// ok, at least we undid everything
					this.CentralCoordinator.Log.Error(ex, "Undo transaction was successful");
				} else {
					this.CentralCoordinator.Log.Error(ex, "Failed to undo transaction");

					throw new ApplicationException("Failed to undo transaction", ex);
				}

				throw ex;
			}
		}

		protected async Task CreateSafetyBackup(string backupPath, List<FileOperationInfo> fileDeltas) {

			Narballer nar = new Narballer(this.walletsPath, this.physicalFileSystem);

			await this.RepeatAsync(async () => {
				foreach(string file in Narballer.GetPackageFilesList(backupPath)) {
					FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file);

					if(fileInfo?.Exists ?? false) {
						await this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
					}
				}
			}).ConfigureAwait(false);

			foreach(FileOperationInfo file in fileDeltas) {
				if(file.FileOp == FileOps.Deleted) {

					this.Repeat(() => {

						string adjustedPath = file.originalName.Replace(this.walletsPath, "");
						string separator1 = Path.DirectorySeparatorChar.ToString();
						string separator2 = Path.AltDirectorySeparatorChar.ToString();

						if(adjustedPath.StartsWith(separator1)) {
							adjustedPath = adjustedPath.Substring(separator1.Length, adjustedPath.Length - separator1.Length);
						}

						if(adjustedPath.StartsWith(separator2)) {
							adjustedPath = adjustedPath.Substring(separator2.Length, adjustedPath.Length - separator2.Length);
						}

						if(this.physicalFileSystem.FileExists(file.originalName)) {
							nar.AddFile(file.originalName);
						}
					});
				}

				if(file.FileOp == FileOps.Modified) {

					this.Repeat(() => {

						string adjustedPath = file.originalName.Replace(this.walletsPath, "");
						string separator1 = Path.DirectorySeparatorChar.ToString();
						string separator2 = Path.AltDirectorySeparatorChar.ToString();

						if(adjustedPath.StartsWith(separator1)) {
							adjustedPath = adjustedPath.Substring(separator1.Length, adjustedPath.Length - separator1.Length);
						}

						if(adjustedPath.StartsWith(separator2)) {
							adjustedPath = adjustedPath.Substring(separator2.Length, adjustedPath.Length - separator2.Length);
						}

						if(this.physicalFileSystem.FileExists(file.originalName)) {
							nar.AddFile(file.originalName);
						}
					});
				}
			}

			if(!nar.Package(backupPath)) {
				this.CentralCoordinator.Log.Verbose("No files were found to backup. no backup created.");
			}
		}

		protected async Task ApplyFileDeltas(List<FileOperationInfo> fileDeltas) {

			foreach(FileOperationInfo file in fileDeltas) {

				if(file.FileOp == FileOps.Deleted) {

					await this.RepeatFileOperationAsync(async () => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.temporaryName), this.physicalFileSystem);

						FileSystemEntry fileInfoDest = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

						if(fileInfoDest?.Exists ?? false) {
							await this.FullyDeleteFile(fileInfoDest.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
						}

						this.physicalFileSystem.MoveFile(file.originalName, file.temporaryName);
					}).ConfigureAwait(false);
				}

				if(file.FileOp == FileOps.Created) {

					await this.RepeatFileOperationAsync(async () => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.temporaryName), this.physicalFileSystem);

						FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

						if(fileInfoSource?.Exists ?? false) {
							await this.FullyDeleteFile(fileInfoSource.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
						}

                        await this.physicalFileSystem.WriteAllBytesAsync(file.temporaryName, this.activeFileSystem.ReadAllBytes(file.originalName)).ConfigureAwait(false);
					}).ConfigureAwait(false);
				}

				if(file.FileOp == FileOps.Modified) {

					await this.RepeatFileOperationAsync(async () => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.temporaryName), this.physicalFileSystem);

						FileSystemEntry fileInfoDest = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

						if(fileInfoDest?.Exists ?? false) {
							await this.FullyDeleteFile(fileInfoDest.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
						}

						this.physicalFileSystem.MoveFile(file.originalName, file.temporaryName);
					}).ConfigureAwait(false);

					await this.RepeatFileOperationAsync(async () => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.originalName), this.physicalFileSystem);

						FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

						if(fileInfoSource?.Exists ?? false) {
							await this.FullyDeleteFile(fileInfoSource.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
						}

                        await this.physicalFileSystem.WriteAllBytesAsync(file.originalName, this.activeFileSystem.ReadAllBytes(file.originalName)).ConfigureAwait(false);
					}).ConfigureAwait(false);
				}
			}
		}

		protected async Task<bool> UndoFileChanges(string backupPath, List<FileOperationInfo> fileDeltas) {

			try {
				await this.RepeatFileOperationAsync(async () => {

					foreach(FileOperationInfo file in fileDeltas) {
						if(file.FileOp == FileOps.Deleted) {

							FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

							await this.RepeatFileOperationAsync(async () => {

								if(fileInfoSource?.Exists ?? false) {
									await this.FullyDeleteFile(fileInfoSource.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
								}
							}).ConfigureAwait(false);

							FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

							this.RepeatFileOperation(() => {

								if(fileInfo?.Exists ?? false) {
									this.physicalFileSystem.MoveFile(file.temporaryName, file.originalName);
								}
							});
						}

						if(file.FileOp == FileOps.Created) {

							FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

							this.RepeatFileOperation(async () => {
								if(fileInfo?.Exists ?? false) {

									await this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
								}
							});
						}

						if(file.FileOp == FileOps.Modified) {

							FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

							this.RepeatFileOperation(async () => {
								if(fileInfo?.Exists ?? false) {

									await this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
								}
							});

							fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

							this.RepeatFileOperation(() => {
								if(fileInfo?.Exists ?? false) {
									this.physicalFileSystem.MoveFile(file.temporaryName, file.originalName);
								}
							});
						}
					}

				}).ConfigureAwait(false);

				// now confirm each original file is there and matches hash
				List<FileOperationInfo> missingFileDeltas = new List<FileOperationInfo>();

				foreach(FileOperationInfo file in fileDeltas) {
					if(file.FileOp == FileOps.Deleted) {

						FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

						if(!fileInfoSource?.Exists ?? (false || (file.originalHash != await this.HashFile(file.originalName).ConfigureAwait(false)))) {
							missingFileDeltas.Add(file);
						}
					}

					if(file.FileOp == FileOps.Modified) {

						FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

						if(!fileInfoSource?.Exists ?? (false || (file.originalHash != await this.HashFile(file.originalName).ConfigureAwait(false)))) {
							missingFileDeltas.Add(file);
						}
					}
				}
				
				if(missingFileDeltas.Any()) {
					this.RestoreFromBackup(backupPath, missingFileDeltas);
				}

				await this.RepeatAsync(async () => {
					foreach(string file in Narballer.GetPackageFilesList(backupPath)) {
						FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file);

						if(fileInfo?.Exists ?? false) {
							await this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
						}
					}
				}).ConfigureAwait(false);

				// clear remaining zombie directories
				await this.DeleteInnexistentDirectories(this.physicalFileSystem.GetDirectoryEntryUnconditional(this.walletsPath)).ConfigureAwait(false);

				return true;
			} catch(Exception ex) {

				if(Narballer.PackageFilesValid(backupPath, this.physicalFileSystem)) {
					this.CentralCoordinator.Log.Error(ex, "An exception occured in the transaction. attempting to restore from backup package");

					// ok, lets restore from the zip
					try {
						this.RestoreFromBackup(backupPath, fileDeltas);

						try {
							await this.RepeatAsync(async () => {
								foreach(string file in Narballer.GetPackageFilesList(backupPath)) {
									FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file);

									if(fileInfo?.Exists ?? false) {
										await this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
									}
								}
							}).ConfigureAwait(false);

						} catch {
							// nothing to do, its ok
						}

						this.CentralCoordinator.Log.Error(ex, "restore from zip completed successfully");

						return true;
					} catch {
						this.CentralCoordinator.Log.Fatal(ex, "Failed to restore from zip. Exiting application to saveguard the wallet. please restore your wallet manually by extracting from zip file.");

						Process.GetCurrentProcess().Kill();

					}
				} else {
					this.CentralCoordinator.Log.Fatal(ex, "An exception occured in the transaction. wallet could be in an incomplete state and no backup could be found. possible corruption");

					Process.GetCurrentProcess().Kill();
				}
			}

			return false;
		}

		/// <summary>
		/// this allows us to rescue a wallet from an existing narball structure
		/// </summary>
		/// <param name="backupPath"></param>
		/// <returns></returns>
		public async Task<bool> RescueFromNarballStructure() {

			string backupPath = this.GetBackupPath();

			if(Narballer.PackageFilesValid(backupPath, this.physicalFileSystem)) {

				// ok, lets restore from the zip

				try {
					await this.RepeatAsync(async () => {
						
						Narballer nar = new Narballer(this.walletsPath, this.physicalFileSystem);

						nar.Restore(this.walletsPath, backupPath, null, false);
					}).ConfigureAwait(false);
					
					return true;
				} catch {
					// nothing to do, its ok
					
				}
			}

			return false;
		}



		/// <summary>
		///     delete directories that may be remaining in case of a rollback
		/// </summary>
		/// <param name="fileDeltas"></param>
		private async Task DeleteInnexistentDirectories(DirectoryEntry physicalDirectoryInfo) {

			string[] directories = physicalDirectoryInfo.EnumerateDirectories().Select(d => d.ToOsPath(this.physicalFileSystem)).ToArray();

			foreach(string directory in directories) {
				if(!this.initialFileStructure.Any(s => s.StartsWith(directory, StringComparison.InvariantCultureIgnoreCase))) {
					DirectoryEntry directoryInfo = this.physicalFileSystem.GetDirectoryEntryUnconditional(directory);

					await this.RepeatFileOperationAsync(async () => {
						if(directoryInfo.Exists) {
							await SafeDeleteDirectoryStructure(directoryInfo, this.physicalFileSystem).ConfigureAwait(false);
						}
					}).ConfigureAwait(false);
				}
			}

			foreach(DirectoryEntry subdirectory in physicalDirectoryInfo.EnumerateDirectories()) {

				await this.DeleteInnexistentDirectories(subdirectory).ConfigureAwait(false);
			}
		}

		protected void RestoreFromBackup(string backupPath, List<FileOperationInfo> fileDeltas) {
			try {
				this.Repeat(() => {
					if(!Narballer.PackageFilesValid(backupPath, this.physicalFileSystem)) {
						return;
					}

					Narballer nar = new Narballer(this.walletsPath, this.physicalFileSystem);

					nar.Restore(this.walletsPath, backupPath, fileDeltas.Select(d => d.originalName).ToList(), false);
				});
			} catch(Exception ex) {
				throw new ApplicationException($"Failed to restore wallet from backup. This is serious. Original backup files remain available and can be recovered manually from '{backupPath}'.", ex);
			}
		}

		protected async Task CompleteFileChanges(List<FileOperationInfo> fileDeltas) {

			foreach(FileOperationInfo file in fileDeltas) {
				if(file.FileOp == FileOps.Deleted) {

					FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

					await this.RepeatFileOperationAsync(async () => {
						if(fileInfo?.Exists ?? false) {
							await this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
						}
					}).ConfigureAwait(false);
				}

				if(file.FileOp == FileOps.Created) {

					FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

					await this.RepeatFileOperationAsync(async () => {
						if(fileInfoSource?.Exists ?? false) {
							await this.FullyDeleteFile(fileInfoSource.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
						}
					}).ConfigureAwait(false);

					FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

					this.RepeatFileOperation(() => {
						if(fileInfo?.Exists ?? false) {
							this.physicalFileSystem.MoveFile(file.temporaryName, file.originalName);
						}
					});
				}

				if(file.FileOp == FileOps.Modified) {

					FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

					if(fileInfo?.Exists ?? false) {
						await this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem).ConfigureAwait(false);
					}
				}
			}
		}

		/// <summary>
		///     compare both directories and build the modified files delta
		/// </summary>
		/// <param name="activeDirectoryInfo"></param>
		/// <param name="physicalDirectoryInfo"></param>
		/// <exception cref="ApplicationException"></exception>
		private async Task BuildDelta(DirectoryEntry activeDirectoryInfo, DirectoryEntry physicalDirectoryInfo, List<FileOperationInfo> fileDeltas) {
			// skip any exclusions
			//TODO: make this more thserful with regexes
			if(this.exclusions?.Any(e => string.Equals(e.name, activeDirectoryInfo.Name, StringComparison.CurrentCultureIgnoreCase)) ?? false) {
				return;
			}

			FileSystemEntry[] activeFiles = activeDirectoryInfo.Exists ? activeDirectoryInfo.EnumerateFiles().ToArray() : new FileSystemEntry[0];
			FileSystemEntry[] physicalFiles;

			if((physicalDirectoryInfo == null) || !physicalDirectoryInfo.Exists) {
				physicalFiles = new FileSystemEntry[0];
			} else {
				physicalFiles = physicalDirectoryInfo.EnumerateFiles().ToArray();
			}

			//TODO: this is due to a bug where the temp files remain. fix the bug instead of this hack
			physicalFiles = physicalFiles.Where(f => !f.ToOsPath(this.physicalFileSystem).EndsWith("transaction-delete") && !f.ToOsPath(this.physicalFileSystem).EndsWith("transaction-new") && !f.ToOsPath(this.physicalFileSystem).EndsWith("transaction-modified")).ToArray();

			List<string> activeFileNames = activeFiles.Select(f => f.ToOsPath(this.physicalFileSystem)).ToList();
			List<string> physicalFileNames = physicalFiles.Select(f => f.ToOsPath(this.physicalFileSystem)).ToList();

			//  prepare the delta
			List<FileSystemEntry> deleteFiles = physicalFiles.Where(f => !activeFileNames.Contains(f.ToOsPath(this.physicalFileSystem))).ToList();
			List<FileSystemEntry> newFiles = activeFiles.Where(f => !physicalFileNames.Contains(f.ToOsPath(this.physicalFileSystem))).ToList();

			// TODO: this can be made faster by filtering fileStatuses, not query the whole set
			List<FileSystemEntry> modifiedFiles = activeFiles.Where(f => physicalFileNames.Contains(f.ToOsPath(this.physicalFileSystem)) && this.fileStatuses.ContainsKey(f.ToOsPath(this.physicalFileSystem)) && (this.fileStatuses[f.ToOsPath(this.physicalFileSystem)] == FileStatuses.Modified)).ToList();

			foreach(FileSystemEntry file in deleteFiles) {

				string clearableFileName = file.ToOsPath(this.physicalFileSystem) + "-transaction-delete";
				fileDeltas.Add(new FileOperationInfo {temporaryName = clearableFileName, originalName = file.ToOsPath(this.physicalFileSystem), FileOp = FileOps.Deleted, originalHash = await this.HashFile(file.ToOsPath(this.physicalFileSystem)).ConfigureAwait(false) });
			}

			foreach(FileSystemEntry file in newFiles) {
				string clearableFileName = file.ToOsPath(this.physicalFileSystem) + "-transaction-new";
				fileDeltas.Add(new FileOperationInfo {temporaryName = clearableFileName, originalName = file.ToOsPath(this.physicalFileSystem), FileOp = FileOps.Created});
			}

			foreach(FileSystemEntry file in modifiedFiles) {
				string clearableFileName = file.ToOsPath(this.physicalFileSystem) + "-transaction-modified";
				fileDeltas.Add(new FileOperationInfo {temporaryName = clearableFileName, originalName = file.ToOsPath(this.physicalFileSystem), FileOp = FileOps.Modified, originalHash = await this.HashFile(file.ToOsPath(this.physicalFileSystem)).ConfigureAwait(false) });
			}

			// and recurse into its sub directories
			foreach(DirectoryEntry subdirectory in activeDirectoryInfo.EnumerateDirectories()) {

				DirectoryEntry directoryEntry = null;
				string directoryPath = Path.Combine(physicalDirectoryInfo.ToOsPath(this.physicalFileSystem), subdirectory.Name);

				if(this.physicalFileSystem.DirectoryExists(directoryPath)) {
					directoryEntry = this.physicalFileSystem.GetDirectoryEntryUnconditional(directoryPath);
				}

				await this.BuildDelta(subdirectory, directoryEntry, fileDeltas).ConfigureAwait(false);
			}
		}

		private Task<long> HashFile(string filename) {
			FileEntry fileEntry = this.physicalFileSystem.GetFileEntryUnconditional(filename);

			return this.HashFile(fileEntry);
		}

		private Task<long> HashFile(FileSystemEntry file) {
			if((file?.Exists ?? false) && (file.GetFileLength() != 0)) {
				return HashingUtils.XxHashFile(file.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
			}

			return Task.FromResult(0L);
		}

		private Task FullyDeleteFile(string fileName, FileSystemWrapper fileSystem) {
			return this.RepeatFileOperationAsync(() => {
				if(GlobalSettings.ApplicationSettings.WalletTransactionDeletionMode == AppSettingsBase.WalletTransactionDeletionModes.Safe) {
					return SecureWipe.WipeFile(fileName, fileSystem);
				} else {
					fileSystem.DeleteFile(fileName);
				}
				
				return Task.CompletedTask;
			});

		}

		/// <summary>
		///     Reasonably safely clear files from the physical disk
		/// </summary>
		/// <param name="directoryInfo"></param>
		private static Task SafeDeleteDirectoryStructure(DirectoryEntry directoryInfo, FileSystemWrapper fileSystem) {

			return SecureWipe.WipeDirectory(directoryInfo, fileSystem);
		}

		/// <summary>
		///     an unsafe bust fase regular file deletion
		/// </summary>
		/// <param name="directoryInfo"></param>
		/// <param name="fileSystem"></param>
		private async Task FastDeleteFolderStructure(DirectoryEntry directoryInfo, FileSystemWrapper fileSystem) {
			if(!directoryInfo.Exists) {
				return;
			}

			foreach(FileEntry file in directoryInfo.EnumerateFiles().ToArray()) {
				this.RepeatFileOperation(() => {
					fileSystem.DeleteFile(file.ToOsPath(this.physicalFileSystem));
				});
			}

			foreach(DirectoryEntry subdirectory in directoryInfo.EnumerateDirectories()) {

				await this.DeleteFolderStructure(subdirectory, fileSystem).ConfigureAwait(false);
			}

			this.RepeatFileOperation(() => {
				fileSystem.GetDirectoryEntryUnconditional(directoryInfo.ToOsPath(this.physicalFileSystem)).Delete(true);
			});
		}

		private async Task ImportFilesystems() {
			if(!this.IsInTransaction) {
				return;
			}

			// copy directory structure
			DirectoryEntry directoryInfo = this.physicalFileSystem.GetDirectoryEntryUnconditional(this.walletsPath);

			await RepeatAsync(async () => {

                fileStatuses.Clear();

                await CloneDirectory(directoryInfo).ConfigureAwait(false);

				if(!activeFileSystem.IsMemory) {
					return;
				}

				if(!activeFileSystem.AllFiles().Any()) {
					throw new ApplicationException("Failed to read wallet files from disk");
				}
			}).ConfigureAwait(false);

		}

		private async Task CloneDirectory(DirectoryEntry directory) {

			// skip any exclusions
			//TODO: make this stronger with regexes
			if(this.exclusions?.Any(e => string.Equals(e.name, directory.Name, StringComparison.CurrentCultureIgnoreCase)) ?? false) {
				return;
			}

			this.CreateDirectory(directory.ToOsPath(this.physicalFileSystem));

			foreach(FileEntry file in directory.EnumerateFiles().ToArray()) {
				string path = file.ToOsPath(this.physicalFileSystem);
				this.Create(path);

				if(!this.fileStatuses.ContainsKey(path)) {
					this.fileStatuses.Add(path, FileStatuses.Untouched);
				} else {
					this.fileStatuses[path] = FileStatuses.Untouched;
				}
				if(!this.initialFileStructure.Contains(path)) {
					this.initialFileStructure.Add(path);
				}
			}

			// and its sub directories
			foreach(DirectoryEntry subdirectory in directory.EnumerateDirectories()) {

				await CloneDirectory(subdirectory).ConfigureAwait(false);
			}
		}

		public void EnsureDirectoryStructure(string directory) {

			FileExtensions.EnsureDirectoryStructure(directory, this.activeFileSystem);
		}

		public void EnsureFileExists(string filename) {
			FileExtensions.EnsureFileExists(filename, this.activeFileSystem);
		}

		public bool DirectoryExists(string directory) {
			// complete the path if it is relative
			string path = this.CompletePath(directory);

			return this.activeFileSystem.DirectoryExists(path);
		}

		public void CreateDirectory(string directory) {
			// complete the path if it is relative
			string path = this.CompletePath(directory);

			this.activeFileSystem.CreateDirectory(path);
		}

		public void DeleteDirectory(string directory, bool recursive) {

			// complete the path if it is relative
			string path = this.CompletePath(directory);

			this.activeFileSystem.DeleteDirectory(path, recursive);
		}

		public bool FileExists(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);

			return this.activeFileSystem.FileExists(path);
		}

		/// <summary>
		///     We check If something has changed on the physical file system that may not reflect in the memory one
		/// </summary>
		/// <param name="file"></param>
		public void RefreshFile(string file) {
			string path = this.CompletePath(file);

			//TODO: right now it only adds missing files. should we handle deletes and udpates too?

			bool physicalExists = this.physicalFileSystem.FileExists(path);
			bool activeExists = this.activeFileSystem.FileExists(path);

			if(physicalExists && !activeExists) {
				// it was added on the physical
				this.fileStatuses.Add(file, FileStatuses.Untouched);
				this.initialFileStructure.Add(file);
				this.Create(file);
			}

			if(!physicalExists && activeExists) {
				// it was deleted on the physical
			}
		}

		private string CompletePath(string file) {
			if(!file.StartsWith(this.walletsPath)) {
				file = Path.Combine(this.walletsPath, file);
			}

			return file;
		}

		public void FileDelete(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);

			this.activeFileSystem.DeleteFile(path);

			// mark it as modified
			if(this.fileStatuses.ContainsKey(path)) {
				this.fileStatuses[path] = FileStatuses.Modified;
			}
		}

		public void FileMove(string src, string dest) {

			// complete the path if it is relative
			string srcpath = this.CompletePath(src);
			string destPath = this.CompletePath(dest);
			this.CompleteFile(srcpath);

			this.activeFileSystem.MoveFile(srcpath, destPath);

			// mark it as modified
			if(this.fileStatuses.ContainsKey(srcpath)) {
				this.fileStatuses[srcpath] = FileStatuses.Modified;
			}

			// mark it as modified
			if(this.fileStatuses.ContainsKey(destPath)) {
				this.fileStatuses[destPath] = FileStatuses.Modified;
			} else {
				this.fileStatuses.Add(destPath, FileStatuses.Modified);
			}
		}

		public string GetDirectoryName(string path) {
			// complete the path if it is relative
			string fullpath = this.CompletePath(path);

			return Path.GetDirectoryName(fullpath);
		}

		/// <summary>
		///     Ensure we compelte the file's data to use it. this is lazy loading
		/// </summary>
		/// <param name="path"></param>
		private void CompleteFile(string path) {
			if(this.fileStatuses.ContainsKey(path) && (this.fileStatuses[path] == FileStatuses.Untouched)) {
				this.activeFileSystem.WriteAllBytes(path, this.physicalFileSystem.ReadAllBytes(path));

			}
		}

		public void OpenWrite(string filename, SafeArrayHandle bytes) {
			// complete the path if it is relative
			string path = this.CompletePath(filename);

			this.CompleteFile(path);

			FileExtensions.OpenWrite(path, bytes, this.activeFileSystem);

			// mark it as modified
			if(this.fileStatuses.ContainsKey(path)) {
				this.fileStatuses[path] = FileStatuses.Modified;
			} else {
				this.fileStatuses.Add(path, FileStatuses.Modified);
			}
		}

		public Task OpenWriteAsync(string filename, SafeArrayHandle bytes) {
			return OpenWriteAsync(filename, new []{bytes});
		}

		public async Task OpenWriteAsync(string filename, SafeArrayHandle[] bytes) {
			// complete the path if it is relative
			string path = this.CompletePath(filename);

			this.CompleteFile(path);

			await FileExtensions.OpenWriteAsync(path, bytes, this.activeFileSystem).ConfigureAwait(false);

			// mark it as modified
			if(this.fileStatuses.ContainsKey(path)) {
				this.fileStatuses[path] = FileStatuses.Modified;
			} else {
				this.fileStatuses.Add(path, FileStatuses.Modified);
			}
		}

		public void OpenWrite(string filename, string text) {
			// complete the path if it is relative
			string path = this.CompletePath(filename);

			this.CompleteFile(path);

			FileExtensions.OpenWrite(path, text, this.activeFileSystem);

			// mark it as modified
			if(this.fileStatuses.ContainsKey(path)) {
				this.fileStatuses[path] = FileStatuses.Modified;
			} else {
				this.fileStatuses.Add(path, FileStatuses.Modified);
			}
		}

		public async Task OpenWriteAsync(string filename, string text) {
			// complete the path if it is relative
			string path = this.CompletePath(filename);

			this.CompleteFile(path);

			await FileExtensions.OpenWriteAsync(path, text, this.activeFileSystem).ConfigureAwait(false);

			// mark it as modified
			if(this.fileStatuses.ContainsKey(path)) {
				this.fileStatuses[path] = FileStatuses.Modified;
			} else {
				this.fileStatuses.Add(path, FileStatuses.Modified);
			}
		}

		public byte[] ReadAllBytes(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);
			this.CompleteFile(path);

			return this.activeFileSystem.ReadAllBytes(path);
		}

		public Task<byte[]> ReadAllBytesAsync(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);
			this.CompleteFile(path);

			return this.activeFileSystem.ReadAllBytesAsync(path);
		}

		public Stream OpenRead(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);
			this.CompleteFile(path);

			return this.activeFileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		}

		public void Create(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);

			FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file), this.activeFileSystem);

			this.activeFileSystem.CreateEmptyFile(path);
		}


		protected class FileOperationInfo {
			public FileSystemEntry FileInfo;
			public FileOps FileOp;
			public long originalHash;
			public string originalName;
			public string temporaryName;
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				if(this.walletSerializationTransaction != null) {
					this.RollbackTransaction().WaitAndUnwrapException();
				}

				// lets wait for all cleaning tasks to complete before we go any further
				this.ClearCleaningTasks();

				this.WaitCleaningTasks().WaitAndUnwrapException();

				this.ClearCleaningTasks();
			}

			this.IsDisposed = true;
		}

		~WalletSerializationTransactionalLayer() {
			this.Dispose(false);
		}

	#endregion

	}
}