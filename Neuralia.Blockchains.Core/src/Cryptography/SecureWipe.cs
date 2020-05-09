using System;
using System.IO;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.BouncyCastle.extra.Security;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography {
	public static class SecureWipe {
		/// <summary>
		///     Deletes a file in a secure way by overwriting it with
		///     random garbage data n times.
		/// </summary>
		/// <param name="filename">Full path of the file to be deleted</param>
		/// <param name="timesToWrite">Specifies the number of times the file should be overwritten</param>
		public static async Task WipeFile(string filename, int timesToWrite, FileSystemWrapper fileSystem) {
			try {
				if(fileSystem.FileExists(filename)) {
					// Calculate the total number of sectors in the file.
					decimal sectors = Math.Ceiling(fileSystem.GetFileLength(filename) / 512M);

					// Set the files attributes to normal in case it's read-only.
					fileSystem.SetAttributes(filename, FileAttributes.Normal);

					// Buffer the size of a sector.
					byte[] buffer = new byte[1024];

					SecureRandom random = new BetterSecureRandom();

					await using Stream inputStream = fileSystem.CreateFile(filename);

					for(int currentPass = 0; currentPass < timesToWrite; currentPass++) {

						inputStream.Position = 0;

						// Loop all sectors
						for(int sectorsWritten = 0; sectorsWritten < sectors; sectorsWritten++) {

							// Fill the buffer with random data
							random.NextBytes(buffer);

							await inputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
						}
					}

					// Truncate the file to 0 bytes.
					// This will hide the original file-length  if there are attempts to recover the file.
					inputStream.SetLength(0);

					inputStream.Close();

					// change the dates of the file. original dates will be hidden if there are attempts to recover the file.
					DateTime dt = new DateTime(2037, 1, 1, 0, 0, 0);
					fileSystem.SetCreationTime(filename, dt);
					fileSystem.SetLastAccessTime(filename, dt);
					fileSystem.SetLastWriteTime(filename, dt);

					// delete the file
					fileSystem.DeleteFile(filename);

				}
			} catch(Exception e) {
				throw e;
			}
		}
	}

}