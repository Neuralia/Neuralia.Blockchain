using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Nito.AsyncEx.Synchronous;
using Serilog;
using Zio;
using Zio.FileSystems;
using SafeArrayHandle = Neuralia.Blockchains.Tools.Data.SafeArrayHandle;

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

		private const uint HRFileLocked          = 0x80070020;
		private const uint HRPortionOfFileLocked = 0x80070021;

		private readonly List<Task> cleaningTasks = new List<Task>();

		private readonly List<(string name, FilesystemTypes type)> exclusions;

		private readonly Dictionary<string, FileStatuses> fileStatuses         = new Dictionary<string, FileStatuses>();
		private readonly HashSet<string>                  initialFileStructure = new HashSet<string>();

		protected readonly FileSystemWrapper physicalFileSystem;

		private readonly string walletsPath;

		//TODO: replace MemoryFileSystem by our own implementation. using it for now as it works for basic purposes
		protected FileSystemWrapper activeFileSystem;

		private WalletSerializationTransaction walletSerializationTransaction;

		public WalletSerializationTransactionalLayer(string walletsPath, List<(string name, FilesystemTypes type)> exclusions, FileSystemWrapper fileSystem) {
			// start with a physical filesystem
			this.physicalFileSystem = fileSystem ?? FileSystemWrapper.CreatePhysical();
			this.activeFileSystem   = this.physicalFileSystem;
			this.walletsPath        = walletsPath;
			this.exclusions         = exclusions;
		}

		public FileSystemWrapper FileSystem => this.activeFileSystem;

		private void Repeat(Action action) {
			Repeater.Repeat(action, 5, () => {
				Thread.Sleep(150);
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

		private bool IsFileLocked(IOException ioex) {
			uint errorCode = (uint) Marshal.GetHRForException(ioex);

			return errorCode == HRFileLocked || errorCode == HRPortionOfFileLocked;
		}

		private void ClearCleaningTasks() {

			foreach(Task task in this.cleaningTasks.Where(t => t.IsCompleted).ToArray()) {
				this.cleaningTasks.Remove(task);
			}
		}

		public WalletSerializationTransaction BeginTransaction() {
			this.WaitCleaningTasks();

			this.ClearCleaningTasks();
			this.fileStatuses.Clear();
			this.initialFileStructure.Clear();

			if(this.walletSerializationTransaction != null) {
				throw new ApplicationException("Transaction already in progress");
			}

			this.walletSerializationTransaction = new WalletSerializationTransaction(this);
			this.activeFileSystem               = FileSystemWrapper.CreateMemory();

			this.ImportFilesystems();

			return this.walletSerializationTransaction;
		}

		private void DeleteFolderStructure(DirectoryEntry directory, FileSystemWrapper memoryFileSystem) {
			if(GlobalSettings.ApplicationSettings.WalletTransactionDeletionMode == AppSettingsBase.WalletTransactionDeletionModes.Safe) {
				this.SafeDeleteFolderStructure(directory, memoryFileSystem);
			} else {
				this.FastDeleteFolderStructure(directory, memoryFileSystem);
			}
		}

		public async Task CommitTransaction() {

			// wait for any remaining tasks if required
			this.WaitCleaningTasks();

			this.ClearCleaningTasks();

			try {
				// this is important. let's try 3 times before we declare it a fail
				if(!await Repeater.RepeatAsync(CommitDirectoryChanges, 2, WaitCleaningTasks).ConfigureAwait(false)) {
					await RollbackTransaction().ConfigureAwait(false);
				}

			} catch(Exception ex) {
				await RollbackTransaction().ConfigureAwait(false);

				throw ex;
			}

			this.walletSerializationTransaction.CommitTransaction();
			this.walletSerializationTransaction = null;

			FileSystemWrapper memoryFileSystem = this.activeFileSystem;

			this.cleaningTasks.Add(Task.Run(() => this.DeleteFolderStructure(memoryFileSystem.GetDirectoryEntryUnconditional(this.walletsPath), memoryFileSystem)));

			this.activeFileSystem = this.physicalFileSystem;
			this.fileStatuses.Clear();
			this.initialFileStructure.Clear();
		}

		public async Task RollbackTransaction() {
			this.ClearCleaningTasks();

			await walletSerializationTransaction.RollbackTransaction().ConfigureAwait(false);
			this.walletSerializationTransaction = null;

			FileSystemWrapper memoryFileSystem = this.activeFileSystem;

			// let all changes go, we release it all
			this.cleaningTasks.Add(Task.Run(() => this.DeleteFolderStructure(memoryFileSystem.GetDirectoryEntryUnconditional(this.walletsPath), memoryFileSystem)));

			this.activeFileSystem = this.physicalFileSystem;
			this.fileStatuses.Clear();
			this.initialFileStructure.Clear();
		}

		private async Task CommitDirectoryChanges() {
			//TODO: make this bullet proof
			if(this.walletSerializationTransaction == null) {
				return;
			}

			// copy directory structure
			DirectoryEntry activeDirectoryInfo   = this.activeFileSystem.GetDirectoryEntryUnconditional(this.walletsPath);
			DirectoryEntry physicalDirectoryInfo = this.physicalFileSystem.GetDirectoryEntryUnconditional(this.walletsPath);
			string         backupPath            = Path.Combine(this.walletsPath, BACKUP_PATH_NAME);

			var fileDeltas = new List<FileOperationInfo>();

			try {
				this.BuildDelta(activeDirectoryInfo, physicalDirectoryInfo, fileDeltas);

				this.CreateSafetyBackup(backupPath, fileDeltas);

				this.ApplyFileDeltas(fileDeltas);

				this.CompleteFileChanges(fileDeltas);

				this.Repeat(() => {

					foreach(var file in Narballer.GetPackageFilesList(backupPath)) {
						FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file);

						if(fileInfo?.Exists ?? false) {
							this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
						}
					}
				});

			} catch(Exception ex) {
				// delete our temp work
				Log.Error(ex, "Failed to write transaction data. Will attempt to undo");

				if(this.UndoFileChanges(backupPath, fileDeltas)) {
					// ok, at least we undid everything
					Log.Error(ex, "Undo transaction was successful");
				} else {
					Log.Error(ex, "Failed to undo transaction");

					throw new ApplicationException("Failed to undo transaction", ex);
				}

				throw ex;
			}
		}

		protected void CreateSafetyBackup(string backupPath, List<FileOperationInfo> fileDeltas) {

			Narballer nar = new Narballer(this.walletsPath, this.physicalFileSystem);

			this.Repeat(() => {
				foreach(var file in Narballer.GetPackageFilesList(backupPath)) {
					FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file);

					if(fileInfo?.Exists ?? false) {
						this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
					}
				}
			});

			foreach(FileOperationInfo file in fileDeltas) {
				if(file.FileOp == FileOps.Deleted) {

					this.Repeat(() => {

						string adjustedPath = file.originalName.Replace(this.walletsPath, "");
						string separator1   = Path.DirectorySeparatorChar.ToString();
						string separator2   = Path.AltDirectorySeparatorChar.ToString();

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
						string separator1   = Path.DirectorySeparatorChar.ToString();
						string separator2   = Path.AltDirectorySeparatorChar.ToString();

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
				Log.Verbose("No files were found to backup. no backup created.");
			}
		}

		protected void ApplyFileDeltas(List<FileOperationInfo> fileDeltas) {

			foreach(FileOperationInfo file in fileDeltas) {

				if(file.FileOp == FileOps.Deleted) {

					this.RepeatFileOperation(() => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.temporaryName), this.physicalFileSystem);

						FileSystemEntry fileInfoDest = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

						if(fileInfoDest?.Exists ?? false) {
							this.FullyDeleteFile(fileInfoDest.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
						}

						this.physicalFileSystem.MoveFile(file.originalName, file.temporaryName);
					});
				}

				if(file.FileOp == FileOps.Created) {

					this.RepeatFileOperation(() => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.temporaryName), this.physicalFileSystem);

						FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

						if(fileInfoSource?.Exists ?? false) {
							this.FullyDeleteFile(fileInfoSource.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
						}

						this.physicalFileSystem.WriteAllBytes(file.temporaryName, this.activeFileSystem.ReadAllBytes(file.originalName));
					});
				}

				if(file.FileOp == FileOps.Modified) {

					this.RepeatFileOperation(() => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.temporaryName), this.physicalFileSystem);

						FileSystemEntry fileInfoDest = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

						if(fileInfoDest?.Exists ?? false) {
							this.FullyDeleteFile(fileInfoDest.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
						}

						this.physicalFileSystem.MoveFile(file.originalName, file.temporaryName);
					});

					this.RepeatFileOperation(() => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.originalName), this.physicalFileSystem);

						FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

						if(fileInfoSource?.Exists ?? false) {
							this.FullyDeleteFile(fileInfoSource.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
						}

						this.physicalFileSystem.WriteAllBytes(file.originalName, this.activeFileSystem.ReadAllBytes(file.originalName));
					});
				}
			}
		}

		protected bool UndoFileChanges(string backupPath, List<FileOperationInfo> fileDeltas) {

			try {
				this.RepeatFileOperation(() => {

					foreach(FileOperationInfo file in fileDeltas) {
						if(file.FileOp == FileOps.Deleted) {

							FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

							this.RepeatFileOperation(() => {

								if(fileInfoSource?.Exists ?? false) {
									this.FullyDeleteFile(fileInfoSource.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
								}
							});

							FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

							this.RepeatFileOperation(() => {

								if(fileInfo?.Exists ?? false) {
									this.physicalFileSystem.MoveFile(file.temporaryName, file.originalName);
								}
							});
						}

						if(file.FileOp == FileOps.Created) {

							FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

							this.RepeatFileOperation(() => {
								if(fileInfo?.Exists ?? false) {

									this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
								}
							});
						}

						if(file.FileOp == FileOps.Modified) {

							FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

							this.RepeatFileOperation(() => {
								if(fileInfo?.Exists ?? false) {

									this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
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

				});

				// now confirm each original file is there and matches hash
				var missingFileDeltas = new List<FileOperationInfo>();

				foreach(FileOperationInfo file in fileDeltas) {
					if(file.FileOp == FileOps.Deleted) {

						FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

						if(!fileInfoSource?.Exists ?? false || file.originalHash != this.HashFile(file.originalName)) {
							missingFileDeltas.Add(file);
						}
					}

					if(file.FileOp == FileOps.Modified) {

						FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

						if(!fileInfoSource?.Exists ?? false || file.originalHash != this.HashFile(file.originalName)) {
							missingFileDeltas.Add(file);
						}
					}
				}

				if(missingFileDeltas.Any()) {
					this.RestoreFromBackup(backupPath, missingFileDeltas);
				}

				this.Repeat(() => {
					foreach(var file in Narballer.GetPackageFilesList(backupPath)) {
						FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file);

						if(fileInfo?.Exists ?? false) {
							this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
						}
					}
				});

				// clear remaining zomkbie directories
				this.DeleteInnexistentDirectories(this.physicalFileSystem.GetDirectoryEntryUnconditional(this.walletsPath));

				return true;
			} catch(Exception ex) {

				if(Narballer.PackageFilesValid(backupPath, this.physicalFileSystem)) {
					Log.Error(ex, "An exception occured in the transaction. attempting to restore from backup package");

					// ok, lets restore from the zip
					try {
						this.RestoreFromBackup(backupPath, fileDeltas);

						try {
							this.Repeat(() => {
								foreach(var file in Narballer.GetPackageFilesList(backupPath)) {
									FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file);

									if(fileInfo?.Exists ?? false) {
										this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
									}
								}
							});

						} catch {
							// nothing to do, its ok
						}

						Log.Error(ex, "restore from zip completed successfully");

						return true;
					} catch {
						Log.Fatal(ex, "Failed to restore from zip. Exiting application to saveguard the wallet. please restore your wallet manually by extracting from zip file.");

						Process.GetCurrentProcess().Kill();

					}
				} else {
					Log.Fatal(ex, "An exception occured in the transaction. wallet could be in an incomplete state and no backup could be found. possible corruption");

					Process.GetCurrentProcess().Kill();
				}
			}

			return false;
		}

		/// <summary>
		///     delete directories that may be remaining in case of a rollback
		/// </summary>
		/// <param name="fileDeltas"></param>
		private void DeleteInnexistentDirectories(DirectoryEntry physicalDirectoryInfo) {

			var directories = physicalDirectoryInfo.EnumerateDirectories().Select(d => d.ToOsPath(this.physicalFileSystem)).ToArray();

			foreach(string directory in directories) {
				if(!this.initialFileStructure.Any(s => s.StartsWith(directory, StringComparison.InvariantCultureIgnoreCase))) {
					DirectoryEntry directoryInfo = this.physicalFileSystem.GetDirectoryEntryUnconditional(directory);

					this.RepeatFileOperation(() => {
						if(directoryInfo.Exists) {
							this.SafeDeleteFolderStructure(directoryInfo, this.physicalFileSystem);
						}
					});
				}
			}

			foreach(DirectoryEntry subdirectory in physicalDirectoryInfo.EnumerateDirectories()) {

				this.DeleteInnexistentDirectories(subdirectory);
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

		protected void CompleteFileChanges(List<FileOperationInfo> fileDeltas) {

			foreach(FileOperationInfo file in fileDeltas) {
				if(file.FileOp == FileOps.Deleted) {

					FileEntry fileInfo = this.physicalFileSystem.GetFileEntryUnconditional(file.temporaryName);

					this.RepeatFileOperation(() => {
						if(fileInfo?.Exists ?? false) {
							this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
						}
					});
				}

				if(file.FileOp == FileOps.Created) {

					FileSystemEntry fileInfoSource = this.physicalFileSystem.GetFileEntryUnconditional(file.originalName);

					this.RepeatFileOperation(() => {
						if(fileInfoSource?.Exists ?? false) {
							this.FullyDeleteFile(fileInfoSource.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
						}
					});

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
						this.FullyDeleteFile(fileInfo.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
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
		private void BuildDelta(DirectoryEntry activeDirectoryInfo, DirectoryEntry physicalDirectoryInfo, List<FileOperationInfo> fileDeltas) {
			// skip any exclusions
			//TODO: make this more powerful with regexes
			if(this.exclusions?.Any(e => string.Equals(e.name, activeDirectoryInfo.Name, StringComparison.CurrentCultureIgnoreCase)) ?? false) {
				return;
			}

			var               activeFiles = activeDirectoryInfo.Exists ? activeDirectoryInfo.EnumerateFiles().ToArray() : new FileSystemEntry[0];
			FileSystemEntry[] physicalFiles;

			if(physicalDirectoryInfo == null || !physicalDirectoryInfo.Exists) {
				physicalFiles = new FileSystemEntry[0];
			} else {
				physicalFiles = physicalDirectoryInfo.EnumerateFiles().ToArray();
			}

			//TODO: this is due to a bug where the temp files remain. fix the bug instead of this hack
			physicalFiles = physicalFiles.Where(f => !f.ToOsPath(this.physicalFileSystem).EndsWith("transaction-delete") && !f.ToOsPath(this.physicalFileSystem).EndsWith("transaction-new") && !f.ToOsPath(this.physicalFileSystem).EndsWith("transaction-modified")).ToArray();

			var activeFileNames   = activeFiles.Select(f => f.ToOsPath(this.physicalFileSystem)).ToList();
			var physicalFileNames = physicalFiles.Select(f => f.ToOsPath(this.physicalFileSystem)).ToList();

			//  prepare the delta
			var deleteFiles = physicalFiles.Where(f => !activeFileNames.Contains(f.ToOsPath(this.physicalFileSystem))).ToList();
			var newFiles    = activeFiles.Where(f => !physicalFileNames.Contains(f.ToOsPath(this.physicalFileSystem))).ToList();

			// TODO: this can be made faster by filtering fileStatuses, not query the whole set
			var modifiedFiles = activeFiles.Where(f => physicalFileNames.Contains(f.ToOsPath(this.physicalFileSystem)) && this.fileStatuses.ContainsKey(f.ToOsPath(this.physicalFileSystem)) && this.fileStatuses[f.ToOsPath(this.physicalFileSystem)] == FileStatuses.Modified).ToList();

			foreach(FileSystemEntry file in deleteFiles) {

				string clearableFileName = file.ToOsPath(this.physicalFileSystem) + "-transaction-delete";
				fileDeltas.Add(new FileOperationInfo {temporaryName = clearableFileName, originalName = file.ToOsPath(this.physicalFileSystem), FileOp = FileOps.Deleted, originalHash = this.HashFile(file.ToOsPath(this.physicalFileSystem))});
			}

			foreach(FileSystemEntry file in newFiles) {
				string clearableFileName = file.ToOsPath(this.physicalFileSystem) + "-transaction-new";
				fileDeltas.Add(new FileOperationInfo {temporaryName = clearableFileName, originalName = file.ToOsPath(this.physicalFileSystem), FileOp = FileOps.Created});
			}

			foreach(FileSystemEntry file in modifiedFiles) {
				string clearableFileName = file.ToOsPath(this.physicalFileSystem) + "-transaction-modified";
				fileDeltas.Add(new FileOperationInfo {temporaryName = clearableFileName, originalName = file.ToOsPath(this.physicalFileSystem), FileOp = FileOps.Modified, originalHash = this.HashFile(file.ToOsPath(this.physicalFileSystem))});
			}

			// and recurse into its sub directories
			foreach(DirectoryEntry subdirectory in activeDirectoryInfo.EnumerateDirectories()) {

				DirectoryEntry directoryEntry = null;
				var            directoryPath  = Path.Combine(physicalDirectoryInfo.ToOsPath(this.physicalFileSystem), subdirectory.Name);

				if(this.physicalFileSystem.DirectoryExists(directoryPath)) {
					directoryEntry = this.physicalFileSystem.GetDirectoryEntryUnconditional(directoryPath);
				}

				this.BuildDelta(subdirectory, directoryEntry, fileDeltas);
			}
		}

		private long HashFile(string filename) {
			var fileEntry = this.physicalFileSystem.GetFileEntryUnconditional(filename);

			return this.HashFile(fileEntry);
		}

		private long HashFile(FileSystemEntry file) {
			if((file?.Exists ?? false) && file.GetFileLength() != 0) {
				return HashingUtils.XxHashFile(file.ToOsPath(this.physicalFileSystem), this.physicalFileSystem);
			}

			return 0;
		}

		private void FullyDeleteFile(string fileName, FileSystemWrapper fileSystem) {
			this.RepeatFileOperation(() => {
				if(GlobalSettings.ApplicationSettings.WalletTransactionDeletionMode == AppSettingsBase.WalletTransactionDeletionModes.Safe) {
					SecureWipe.WipeFile(fileName, 5, fileSystem);
				} else {
					fileSystem.DeleteFile(fileName);
				}
			});

		}

		/// <summary>
		///     Reasonably safely clear files from the physical disk
		/// </summary>
		/// <param name="directoryInfo"></param>
		private void SafeDeleteFolderStructure(DirectoryEntry directoryInfo, FileSystemWrapper fileSystem) {
			if(!directoryInfo.Exists) {
				return;
			}

			foreach(FileSystemEntry file in directoryInfo.EnumerateFiles().ToArray()) {
				this.RepeatFileOperation(() => {
					SecureWipe.WipeFile(file.ToOsPath(this.physicalFileSystem), 5, fileSystem);
				});
			}

			foreach(DirectoryEntry subdirectory in directoryInfo.EnumerateDirectories()) {

				this.SafeDeleteFolderStructure(subdirectory, fileSystem);
			}

			this.RepeatFileOperation(() => {
				fileSystem.GetDirectoryEntryUnconditional(directoryInfo.ToOsPath(this.physicalFileSystem)).Delete(true);
			});
		}

		/// <summary>
		///     an unsafe bust fase regular file deletion
		/// </summary>
		/// <param name="directoryInfo"></param>
		/// <param name="fileSystem"></param>
		private void FastDeleteFolderStructure(DirectoryEntry directoryInfo, FileSystemWrapper fileSystem) {
			if(!directoryInfo.Exists) {
				return;
			}

			foreach(FileSystemEntry file in directoryInfo.EnumerateFiles().ToArray()) {
				this.RepeatFileOperation(() => {
					fileSystem.DeleteFile(file.ToOsPath(this.physicalFileSystem));
				});
			}

			foreach(DirectoryEntry subdirectory in directoryInfo.EnumerateDirectories()) {

				this.DeleteFolderStructure(subdirectory, fileSystem);
			}

			this.RepeatFileOperation(() => {
				fileSystem.GetDirectoryEntryUnconditional(directoryInfo.ToOsPath(this.physicalFileSystem)).Delete(true);
			});
		}

		private void ImportFilesystems() {
			if(this.walletSerializationTransaction == null) {
				return;
			}

			// copy directory structure
			DirectoryEntry directoryInfo = this.physicalFileSystem.GetDirectoryEntryUnconditional(this.walletsPath);

			this.Repeat(() => {

				this.fileStatuses.Clear();

				this.CloneDirectory(directoryInfo);

				if(!this.activeFileSystem.IsMemory) {
					return;
				}

				if(!this.activeFileSystem.AllFiles().Any()) {
					throw new ApplicationException("Failed to read wallet files from disk");
				}
			});

		}

		private void CloneDirectory(DirectoryEntry directory) {

			// skip any exclusions
			//TODO: make this stronger with regexes
			if(this.exclusions?.Any(e => string.Equals(e.name, directory.Name, StringComparison.CurrentCultureIgnoreCase)) ?? false) {
				return;
			}

			this.CreateDirectory(directory.ToOsPath(this.physicalFileSystem));

			foreach(FileSystemEntry file in directory.EnumerateFiles().ToArray()) {
				this.Create(file.ToOsPath(this.physicalFileSystem));
				this.fileStatuses.Add(file.ToOsPath(this.physicalFileSystem), FileStatuses.Untouched);
				this.initialFileStructure.Add(file.ToOsPath(this.physicalFileSystem));
			}

			// and its sub directories
			foreach(DirectoryEntry subdirectory in directory.EnumerateDirectories()) {

				this.CloneDirectory(subdirectory);
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
		/// We check If something has changed on the physical file system that may not reflect in the memory one
		/// </summary>
		/// <param name="file"></param>
		public void RefreshFile(string file) {
			string path = this.CompletePath(file);

			//TODO: right now it only adds missing files. should we handle deletes and udpates too?

			bool physicalExists = this.physicalFileSystem.FileExists(path);
			bool activeExists   = this.activeFileSystem.FileExists(path);

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
			string srcpath  = this.CompletePath(src);
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
			if(this.fileStatuses.ContainsKey(path) && this.fileStatuses[path] == FileStatuses.Untouched) {
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

		public async Task OpenWriteAsync(string filename, SafeArrayHandle bytes) {
			// complete the path if it is relative
			string path = this.CompletePath(filename);

			this.CompleteFile(path);

			await FileExtensions.OpenWriteAsync(path, bytes, activeFileSystem).ConfigureAwait(false);

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

			await FileExtensions.OpenWriteAsync(path, text, activeFileSystem).ConfigureAwait(false);

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

			using(this.activeFileSystem.CreateFile(path)) {
				// nothing to do
			}
		}

		private void WaitCleaningTasks() {
			if(this.cleaningTasks.Any()) {
				Task.WaitAll(this.cleaningTasks.ToArray(), TimeSpan.FromSeconds(5));
			}
		}

		protected class FileOperationInfo {
			public FileSystemEntry FileInfo;
			public FileOps         FileOp;
			public long            originalHash;
			public string          originalName;
			public string          temporaryName;
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

				this.WaitCleaningTasks();

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