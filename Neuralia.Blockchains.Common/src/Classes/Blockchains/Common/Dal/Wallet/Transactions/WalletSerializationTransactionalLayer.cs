using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Serilog;
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
		
		private const uint HRFileLocked = 0x80070020;
		private const uint HRPortionOfFileLocked = 0x80070021;

		private readonly List<Task> cleaningTasks = new List<Task>();

		private readonly List<(string name, FilesystemTypes type)> exclusions;

		private readonly Dictionary<string, FileStatuses> fileStatuses = new Dictionary<string, FileStatuses>();
		private readonly HashSet<string> initialFileStructure = new HashSet<string>();

		protected readonly IFileSystem physicalFileSystem;

		private readonly string walletsPath;

		//TODO: replace MockFileSystem by our own implementation. using it for now as it works for basic purposes
		protected IFileSystem activeFileSystem;

		private WalletSerializationTransaction walletSerializationTransaction;

		public WalletSerializationTransactionalLayer(string walletsPath, List<(string name, FilesystemTypes type)> exclusions, IFileSystem fileSystem) {
			// start with a physical filesystem
			this.physicalFileSystem = fileSystem ?? new FileSystem();
			this.activeFileSystem = this.physicalFileSystem;
			this.walletsPath = walletsPath;
			this.exclusions = exclusions;
		}

		public IFileSystem FileSystem => this.activeFileSystem;

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

			return (errorCode == HRFileLocked) || (errorCode == HRPortionOfFileLocked);
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
			this.activeFileSystem = new MockFileSystem();

			this.ImportFilesystems();

			return this.walletSerializationTransaction;
		}

		private void DeleteFolderStructure(IDirectoryInfo directory, IFileSystem memoryFileSystem) {
			if(GlobalSettings.ApplicationSettings.WalletTransactionDeletionMode == AppSettingsBase.WalletTransactionDeletionModes.Safe) {
				this.SafeDeleteFolderStructure(directory, memoryFileSystem);
			} else {
				this.FastDeleteFolderStructure(directory, memoryFileSystem);
			}
		}

		public void CommitTransaction() {

			// wait for any remaining tasks if required
			this.WaitCleaningTasks();

			this.ClearCleaningTasks();

			try {
				// this is important. let's try 3 times before we declare it a fail
				if(!Repeater.Repeat(this.CommitDirectoryChanges, 2, this.WaitCleaningTasks)) {
					this.RollbackTransaction();
				}

			} catch(Exception ex) {
				this.RollbackTransaction();

				throw ex;
			}

			this.walletSerializationTransaction.CommitTransaction();
			this.walletSerializationTransaction = null;

			IFileSystem memoryFileSystem = this.activeFileSystem;

			this.cleaningTasks.Add(new TaskFactory().StartNew(() => this.DeleteFolderStructure(memoryFileSystem.DirectoryInfo.FromDirectoryName(this.walletsPath), memoryFileSystem)));

			this.activeFileSystem = this.physicalFileSystem;
			this.fileStatuses.Clear();
			this.initialFileStructure.Clear();
		}

		public void RollbackTransaction() {
			this.ClearCleaningTasks();

			this.walletSerializationTransaction.RollbackTransaction();
			this.walletSerializationTransaction = null;

			IFileSystem memoryFileSystem = this.activeFileSystem;

			// let all changes go, we release it all
			this.cleaningTasks.Add(new TaskFactory().StartNew(() => this.DeleteFolderStructure(memoryFileSystem.DirectoryInfo.FromDirectoryName(this.walletsPath), memoryFileSystem)));

			this.activeFileSystem = this.physicalFileSystem;
			this.fileStatuses.Clear();
			this.initialFileStructure.Clear();
		}

		private void CommitDirectoryChanges() {
			//TODO: make this bullet proof
			if(this.walletSerializationTransaction == null) {
				return;
			}

			// copy directory structure
			IDirectoryInfo activeDirectoryInfo = this.activeFileSystem.DirectoryInfo.FromDirectoryName(this.walletsPath);
			IDirectoryInfo physicalDirectoryInfo = this.physicalFileSystem.DirectoryInfo.FromDirectoryName(this.walletsPath);
			string backupPath = Path.Combine(this.walletsPath, BACKUP_PATH_NAME);

			var fileDeltas = new List<FileOperationInfo>();

			try {
				this.BuildDelta(activeDirectoryInfo, physicalDirectoryInfo, fileDeltas);

				this.CreateSafetyBackup(backupPath, fileDeltas);

				this.ApplyFileDeltas(fileDeltas);
				
				this.CompleteFileChanges(fileDeltas);

				this.Repeat(() => {

					foreach(var file in Narballer.GetPackageFilesList(backupPath)) {
						IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file);

						if(fileInfo.Exists) {
							this.FullyDeleteFile(fileInfo.FullName, this.physicalFileSystem);
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
					IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file);

					if(fileInfo.Exists) {
						this.FullyDeleteFile(fileInfo.FullName, this.physicalFileSystem);
					}
				}
			});

			foreach(FileOperationInfo file in fileDeltas) {
				if(file.FileOp == FileOps.Deleted) {

					this.Repeat(() => {
						FileInfo fileInfo = new FileInfo(file.originalName);

						string adjustedPath = file.originalName.Replace(this.walletsPath, "");
						string separator1 = Path.DirectorySeparatorChar.ToString();
						string separator2 = Path.AltDirectorySeparatorChar.ToString();

						if(adjustedPath.StartsWith(separator1)) {
							adjustedPath = adjustedPath.Substring(separator1.Length, adjustedPath.Length - separator1.Length);
						}

						if(adjustedPath.StartsWith(separator2)) {
							adjustedPath = adjustedPath.Substring(separator2.Length, adjustedPath.Length - separator2.Length);
						}

						if(fileInfo.Exists) {
							nar.AddFile(fileInfo.FullName);
						}
					});
				}

				if(file.FileOp == FileOps.Modified) {

					this.Repeat(() => {
						FileInfo fileInfo = new FileInfo(file.originalName);

						string adjustedPath = file.originalName.Replace(this.walletsPath, "");
						string separator1 = Path.DirectorySeparatorChar.ToString();
						string separator2 = Path.AltDirectorySeparatorChar.ToString();

						if(adjustedPath.StartsWith(separator1)) {
							adjustedPath = adjustedPath.Substring(separator1.Length, adjustedPath.Length - separator1.Length);
						}

						if(adjustedPath.StartsWith(separator2)) {
							adjustedPath = adjustedPath.Substring(separator2.Length, adjustedPath.Length - separator2.Length);
						}

						if(fileInfo.Exists) {
							nar.AddFile(fileInfo.FullName);
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

						IFileInfo fileInfoDest = this.physicalFileSystem.FileInfo.FromFileName(file.temporaryName);

						if(fileInfoDest.Exists) {
							this.FullyDeleteFile(fileInfoDest.FullName, this.physicalFileSystem);
						}

						IFileInfo fileInfoSource = this.physicalFileSystem.FileInfo.FromFileName(file.originalName);

						fileInfoSource.MoveTo(file.temporaryName);
					});
				}

				if(file.FileOp == FileOps.Created) {

					this.RepeatFileOperation(() => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.temporaryName), this.physicalFileSystem);

						IFileInfo fileInfoSource = this.physicalFileSystem.FileInfo.FromFileName(file.temporaryName);

						if(fileInfoSource.Exists) {
							this.FullyDeleteFile(fileInfoSource.FullName, this.physicalFileSystem);
						}

						this.physicalFileSystem.File.WriteAllBytes(file.temporaryName, this.activeFileSystem.File.ReadAllBytes(file.originalName));
					});
				}

				if(file.FileOp == FileOps.Modified) {

					this.RepeatFileOperation(() => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.temporaryName), this.physicalFileSystem);

						IFileInfo fileInfoDest = this.physicalFileSystem.FileInfo.FromFileName(file.temporaryName);

						if(fileInfoDest.Exists) {
							this.FullyDeleteFile(fileInfoDest.FullName, this.physicalFileSystem);
						}

						IFileInfo fileInfoSource = this.physicalFileSystem.FileInfo.FromFileName(file.originalName);

						fileInfoSource.MoveTo(file.temporaryName);
					});

					this.RepeatFileOperation(() => {
						FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file.originalName), this.physicalFileSystem);

						IFileInfo fileInfoSource = this.physicalFileSystem.FileInfo.FromFileName(file.originalName);

						if(fileInfoSource.Exists) {
							this.FullyDeleteFile(fileInfoSource.FullName, this.physicalFileSystem);
						}

						this.physicalFileSystem.File.WriteAllBytes(file.originalName, this.activeFileSystem.File.ReadAllBytes(file.originalName));
					});
				}
			}
		}

		protected bool UndoFileChanges(string backupPath, List<FileOperationInfo> fileDeltas) {
			
			try {
				this.RepeatFileOperation(() => {

					foreach(FileOperationInfo file in fileDeltas) {
						if(file.FileOp == FileOps.Deleted) {

							IFileInfo fileInfoSource = this.physicalFileSystem.FileInfo.FromFileName(file.originalName);

							this.RepeatFileOperation(() => {

								if(fileInfoSource.Exists) {
									this.FullyDeleteFile(fileInfoSource.FullName, this.physicalFileSystem);
								}
							});

							IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file.temporaryName);

							this.RepeatFileOperation(() => {

								if(fileInfo.Exists) {
									fileInfo.MoveTo(file.originalName);
								}
							});
						}

						if(file.FileOp == FileOps.Created) {

							IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file.temporaryName);

							this.RepeatFileOperation(() => {
								if(fileInfo.Exists) {

									this.FullyDeleteFile(fileInfo.FullName, this.physicalFileSystem);
								}
							});
						}

						if(file.FileOp == FileOps.Modified) {

							IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file.originalName);

							this.RepeatFileOperation(() => {
								if(fileInfo.Exists) {

									this.FullyDeleteFile(fileInfo.FullName, this.physicalFileSystem);
								}
							});

							fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file.temporaryName);

							this.RepeatFileOperation(() => {
								if(fileInfo.Exists) {
									fileInfo.MoveTo(file.originalName);
								}
							});
						}
					}

				});

				// now confirm each original file is there and matches hash
				var missingFileDeltas = new List<FileOperationInfo>();

				foreach(FileOperationInfo file in fileDeltas) {
					if(file.FileOp == FileOps.Deleted) {

						IFileInfo fileInfoSource = this.physicalFileSystem.FileInfo.FromFileName(file.originalName);

						if(!fileInfoSource.Exists || (file.originalHash != this.HashFile(file.originalName))) {
							missingFileDeltas.Add(file);
						}
					}

					if(file.FileOp == FileOps.Modified) {

						IFileInfo fileInfoSource = this.physicalFileSystem.FileInfo.FromFileName(file.originalName);

						if(!fileInfoSource.Exists || (file.originalHash != this.HashFile(file.originalName))) {
							missingFileDeltas.Add(file);
						}
					}
				}

				if(missingFileDeltas.Any()) {
					this.RestoreFromBackup(backupPath, missingFileDeltas);
				}

				this.Repeat(() => {
					foreach(var file in Narballer.GetPackageFilesList(backupPath)) {
						IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file);

						if(fileInfo.Exists) {
							this.FullyDeleteFile(fileInfo.FullName, this.physicalFileSystem);
						}
					}
				});

				// clear remaining zomkbie directories
				this.DeleteInnexistentDirectories(this.physicalFileSystem.DirectoryInfo.FromDirectoryName(this.walletsPath));

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
									IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file);

									if(fileInfo.Exists) {
										this.FullyDeleteFile(fileInfo.FullName, this.physicalFileSystem);
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
		private void DeleteInnexistentDirectories(IDirectoryInfo physicalDirectoryInfo) {

			var directories = physicalDirectoryInfo.GetDirectories().Select(d => d.FullName).ToArray();

			foreach(string directory in directories) {
				if(!this.initialFileStructure.Any(s => s.StartsWith(directory, StringComparison.InvariantCultureIgnoreCase))) {
					IDirectoryInfo directoryInfo = this.physicalFileSystem.DirectoryInfo.FromDirectoryName(directory);

					this.RepeatFileOperation(() => {
						if(directoryInfo.Exists) {
							this.SafeDeleteFolderStructure(directoryInfo, this.physicalFileSystem);
						}
					});
				}
			}

			foreach(IDirectoryInfo subdirectory in physicalDirectoryInfo.GetDirectories()) {

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

					IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file.temporaryName);

					this.RepeatFileOperation(() => {
						if(fileInfo.Exists) {
							this.FullyDeleteFile(fileInfo.FullName, this.physicalFileSystem);
						}
					});
				}

				if(file.FileOp == FileOps.Created) {

					IFileInfo fileInfoSource = this.physicalFileSystem.FileInfo.FromFileName(file.originalName);

					this.RepeatFileOperation(() => {
						if(fileInfoSource.Exists) {
							this.FullyDeleteFile(fileInfoSource.FullName, this.physicalFileSystem);
						}
					});

					IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file.temporaryName);

					this.RepeatFileOperation(() => {
						if(fileInfo.Exists) {
							fileInfo.MoveTo(file.originalName);
						}
					});
				}

				if(file.FileOp == FileOps.Modified) {

					IFileInfo fileInfo = this.physicalFileSystem.FileInfo.FromFileName(file.temporaryName);

					if(fileInfo.Exists) {
						this.FullyDeleteFile(fileInfo.FullName, this.physicalFileSystem);
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
		private void BuildDelta(IDirectoryInfo activeDirectoryInfo, IDirectoryInfo physicalDirectoryInfo, List<FileOperationInfo> fileDeltas) {
			// skip any exclusions
			//TODO: make this more powerful with regexes
			if(this.exclusions?.Any(e => string.Equals(e.name, activeDirectoryInfo.Name, StringComparison.CurrentCultureIgnoreCase)) ?? false) {
				return;
			}

			var activeFiles = activeDirectoryInfo.Exists ? activeDirectoryInfo.GetFiles().ToArray() : new IFileInfo[0];
			var physicalFiles = physicalDirectoryInfo.Exists ? physicalDirectoryInfo.GetFiles().ToArray() : new IFileInfo[0];

			var activeFileNames = activeFiles.Select(f => f.FullName).ToList();
			var physicalFileNames = physicalFiles.Select(f => f.FullName).ToList();

			//  prepare the delta
			var deleteFiles = physicalFiles.Where(f => !activeFileNames.Contains(f.FullName)).ToList();
			var newFiles = activeFiles.Where(f => !physicalFileNames.Contains(f.FullName)).ToList();

			// TODO: this can be made faster by filtering fileStatuses, not query the whole set
			var modifiedFiles = activeFiles.Where(f => physicalFileNames.Contains(f.FullName) && this.fileStatuses.ContainsKey(f.FullName) && (this.fileStatuses[f.FullName] == FileStatuses.Modified)).ToList();

			foreach(IFileInfo file in deleteFiles) {

				string clearableFileName = file.FullName + "-transaction-delete";
				fileDeltas.Add(new FileOperationInfo {temporaryName = clearableFileName, originalName = file.FullName, FileOp = FileOps.Deleted, originalHash = this.HashFile(file.FullName)});
			}

			foreach(IFileInfo file in newFiles) {
				string clearableFileName = file.FullName + "-transaction-new";
				fileDeltas.Add(new FileOperationInfo {temporaryName = clearableFileName, originalName = file.FullName, FileOp = FileOps.Created});
			}

			foreach(IFileInfo file in modifiedFiles) {
				string clearableFileName = file.FullName + "-transaction-modified";
				fileDeltas.Add(new FileOperationInfo {temporaryName = clearableFileName, originalName = file.FullName, FileOp = FileOps.Modified, originalHash = this.HashFile(file.FullName)});
			}

			// and recurse into its sub directories
			foreach(IDirectoryInfo subdirectory in activeDirectoryInfo.GetDirectories()) {

				this.BuildDelta(subdirectory, this.physicalFileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(physicalDirectoryInfo.FullName, subdirectory.Name)), fileDeltas);
			}
		}

		private long HashFile(string filename) {

			return this.HashFile(this.physicalFileSystem.FileInfo.FromFileName(filename));
		}

		private long HashFile(IFileInfo file) {
			if(file.Exists && (file.Length != 0)) {
				return HashingUtils.XxHashFile(file.FullName,  this.physicalFileSystem);
			}

			return 0;
		}

		private void FullyDeleteFile(string fileName, IFileSystem fileSystem) {
			this.RepeatFileOperation(() => {
				if(GlobalSettings.ApplicationSettings.WalletTransactionDeletionMode == AppSettingsBase.WalletTransactionDeletionModes.Safe) {
					SecureWipe.WipeFile(fileName, 5, fileSystem);
				} else {
					fileSystem.File.Delete(fileName);
				}
			});

		}

		/// <summary>
		///     Reasonably safely clear files from the physical disk
		/// </summary>
		/// <param name="directoryInfo"></param>
		private void SafeDeleteFolderStructure(IDirectoryInfo directoryInfo, IFileSystem fileSystem) {
			if(!directoryInfo.Exists) {
				return;
			}

			foreach(IFileInfo file in directoryInfo.GetFiles().ToArray()) {
				this.RepeatFileOperation(() => {
					SecureWipe.WipeFile(file.FullName, 5, fileSystem);
				});
			}

			foreach(IDirectoryInfo subdirectory in directoryInfo.GetDirectories()) {

				this.SafeDeleteFolderStructure(subdirectory, fileSystem);
			}

			this.RepeatFileOperation(() => {
				fileSystem.DirectoryInfo.FromDirectoryName(directoryInfo.FullName).Delete(true);
			});
		}

		/// <summary>
		///     an unsafe bust fase regular file deletion
		/// </summary>
		/// <param name="directoryInfo"></param>
		/// <param name="fileSystem"></param>
		private void FastDeleteFolderStructure(IDirectoryInfo directoryInfo, IFileSystem fileSystem) {
			if(!directoryInfo.Exists) {
				return;
			}

			foreach(IFileInfo file in directoryInfo.GetFiles().ToArray()) {
				this.RepeatFileOperation(() => {
					fileSystem.File.Delete(file.FullName);
				});
			}

			foreach(IDirectoryInfo subdirectory in directoryInfo.GetDirectories()) {

				this.DeleteFolderStructure(subdirectory, fileSystem);
			}

			this.RepeatFileOperation(() => {
				fileSystem.DirectoryInfo.FromDirectoryName(directoryInfo.FullName).Delete(true);
			});
		}

		private void ImportFilesystems() {
			if(this.walletSerializationTransaction == null) {
				return;
			}

			// copy directory structure
			IDirectoryInfo directoryInfo = this.physicalFileSystem.DirectoryInfo.FromDirectoryName(this.walletsPath);

			this.Repeat(() => {

				this.CloneDirectory(directoryInfo);

				if(!(this.activeFileSystem is MockFileSystem mockFileSystem)) {
					return;
				}

				if(!mockFileSystem.AllFiles.Any()) {
					throw new ApplicationException("Failed to read wallet files from disk");
				}
			});

		}

		private void CloneDirectory(IDirectoryInfo directory) {

			// skip any exclusions
			//TODO: make this stronger with regexes
			if(this.exclusions?.Any(e => string.Equals(e.name, directory.Name, StringComparison.CurrentCultureIgnoreCase)) ?? false) {
				return;
			}

			this.CreateDirectory(directory.FullName);

			foreach(IFileInfo file in directory.GetFiles().ToArray()) {
				this.Create(file.FullName);
				this.fileStatuses.Add(file.FullName, FileStatuses.Untouched);
				this.initialFileStructure.Add(file.FullName);
			}

			// and its sub directories
			foreach(IDirectoryInfo subdirectory in directory.GetDirectories()) {

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

			return this.activeFileSystem.Directory.Exists(path);
		}

		public void CreateDirectory(string directory) {
			// complete the path if it is relative
			string path = this.CompletePath(directory);

			this.activeFileSystem.Directory.CreateDirectory(path);
		}

		public void DeleteDirectory(string directory, bool recursive) {

			// complete the path if it is relative
			string path = this.CompletePath(directory);

			this.activeFileSystem.Directory.Delete(path, recursive);
		}

		public bool FileExists(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);

			return this.activeFileSystem.File.Exists(path);
		}

		/// <summary>
		/// We check If something has changed on the physical file system that may not reflect in the memory one
		/// </summary>
		/// <param name="file"></param>
		public void RefreshFile(string file) {
			string path = this.CompletePath(file);

			//TODO: right now it only adds missing files. should we handle deletes and udpates too?
			
			bool physicalExists = this.physicalFileSystem.File.Exists(path);
			bool activeExists = this.activeFileSystem.File.Exists(path);
			
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

			this.activeFileSystem.File.Delete(path);

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

			this.activeFileSystem.File.Move(srcpath, destPath);

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
				this.activeFileSystem.File.WriteAllBytes(path, this.physicalFileSystem.File.ReadAllBytes(path));

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

		public byte[] ReadAllBytes(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);
			this.CompleteFile(path);

			return this.activeFileSystem.File.ReadAllBytes(path);
		}

		public Stream OpenRead(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);
			this.CompleteFile(path);

			return this.activeFileSystem.File.OpenRead(path);
		}

		public Stream Create(string file) {
			// complete the path if it is relative
			string path = this.CompletePath(file);

			FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(file), this.activeFileSystem);
			
			return this.activeFileSystem.File.Create(path);
		}

		private void WaitCleaningTasks() {
			if(this.cleaningTasks.Any()) {
				Task.WaitAll(this.cleaningTasks.ToArray(), TimeSpan.FromSeconds(5));
			}
		}

		protected class FileOperationInfo {
			public IFileInfo FileInfo;
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
					this.RollbackTransaction();
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