using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Threading;
using Zio;

namespace Neuralia.Blockchains.Core.Cryptography {
	public static class SecureWipe {

		/// <summary>
		///     Deletes a file in a secure way by overwriting it with
		///     random garbage data n times.
		/// </summary>
		/// <param name="filename">Full path of the file to be deleted</param>
		/// <param name="fileSystem"></param>
		/// <param name="timesToWrite">Specifies the number of times the file should be overwritten</param>
		public static Task WipeFile(string filename, FileSystemWrapper fileSystem, int timesToWrite = 5) {
			if(!fileSystem.FileExists(filename)) {
				return Task.CompletedTask;
			}
			
			return WipeFile(fileSystem.GetFileEntry(filename), fileSystem, timesToWrite);
		}

		/// <summary>
		///     Deletes a file in a secure way by overwriting it with
		///     random garbage data n times.
		/// </summary>
		/// <param name="file"></param>
		/// <param name="fileSystem"></param>
		/// <param name="timesToWrite">Specifies the number of times the file should be overwritten</param>
		/// <param name="filename">Full path of the file to be deleted</param>
		public static async Task WipeFile(FileEntry file, FileSystemWrapper fileSystem, int timesToWrite = 5) {
			try {
				if(file.Exists) {
					// Calculate the total number of sectors in the file.
					decimal sectors = Math.Ceiling(file.Length / 512M);

					// Set the files attributes to normal in case it's read-only.

					string path = file.ToOsPath(fileSystem);
					fileSystem.SetAttributes(path, FileAttributes.Normal);

					// Buffer the size of a sector.
					byte[] buffer = new byte[1024];
					
					await using Stream inputStream = file.Open(FileMode.Open, FileAccess.Write, FileShare.None);

					for(int currentPass = 0; currentPass < timesToWrite; currentPass++) {

						inputStream.Position = 0;

						// Loop all sectors
						for(int sectorsWritten = 0; sectorsWritten < sectors; sectorsWritten++) {

							// Fill the buffer with random data
							GlobalRandom.GetNextBytes(buffer);

							await inputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
						}
					}

					// Truncate the file to 0 bytes.
					// This will hide the original file-length  if there are attempts to recover the file.
					inputStream.SetLength(0);

					inputStream.Close();

					// change the dates of the file. original dates will be hidden if there are attempts to recover the file.
					DateTime dt = new DateTime(2037, 1, 1, 0, 0, 0);
					fileSystem.SetCreationTime(path, dt);
					fileSystem.SetLastAccessTime(path, dt);
					fileSystem.SetLastWriteTime(path, dt);

					// delete the file
					fileSystem.DeleteFile(path);

				}
			} catch(Exception e) {
				throw e;
			}
		}

		/// <summary>
		/// securely wipe an entire directory
		/// </summary>
		/// <param name="directoryPath"></param>
		/// <param name="fileSystem"></param>
		/// <param name="timesToWrite"></param>
		/// <returns></returns>
		public static Task WipeDirectory(string directoryPath, FileSystemWrapper fileSystem, int timesToWrite = 5) {
			if(!fileSystem.DirectoryExists(directoryPath)) {
				return Task.CompletedTask;
			}

			var directoryInfo = fileSystem.GetDirectoryEntryUnconditional(directoryPath);

			return WipeDirectory(directoryInfo, fileSystem, timesToWrite);
		}

		public static async Task WipeDirectory(DirectoryEntry directory, FileSystemWrapper fileSystem, int timesToWrite = 5, bool parallel = false) {

			var files = directory.EnumerateFiles().ToArray();
			if(parallel) {
				await ParallelAsync.ForEach(files, entry => {
					var file = entry.entry;
					return WipeFile(file, fileSystem, timesToWrite);
				}).ConfigureAwait(false);
			} else {
				foreach(FileEntry file in files) {
					await WipeFile(file, fileSystem, timesToWrite).ConfigureAwait(false);
				}
			}

			foreach(DirectoryEntry subdirectory in directory.EnumerateDirectories()) {

				await WipeDirectory(subdirectory, fileSystem, timesToWrite, parallel).ConfigureAwait(false);
			}

			fileSystem.GetDirectoryEntryUnconditional(directory.ToOsPath(fileSystem)).Delete(true);
		}
	}

}