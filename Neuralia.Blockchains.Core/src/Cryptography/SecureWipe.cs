using System;
using System.IO;
using System.IO.Abstractions;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Core.Cryptography {
	public static class SecureWipe {
		/// <summary>
		///     Deletes a file in a secure way by overwriting it with
		///     random garbage data n times.
		/// </summary>
		/// <param name="filename">Full path of the file to be deleted</param>
		/// <param name="timesToWrite">Specifies the number of times the file should be overwritten</param>
		public static void WipeFile(string filename, int timesToWrite, IFileSystem fileSystem) {
			try {
				if(fileSystem.File.Exists(filename)) {
					// Calculate the total number of sectors in the file.
					decimal sectors = Math.Ceiling(fileSystem.FileInfo.FromFileName(filename).Length / 512M);

					// Set the files attributes to normal in case it's read-only.
					fileSystem.File.SetAttributes(filename, FileAttributes.Normal);

					// Buffer the size of a sector.
					var buffer = new byte[1024];

					SecureRandom random = new SecureRandom();

					Stream inputStream = fileSystem.FileStream.Create(filename, FileMode.Open);

					for(int currentPass = 0; currentPass < timesToWrite; currentPass++) {

						inputStream.Position = 0;

						// Loop all sectors
						for(int sectorsWritten = 0; sectorsWritten < sectors; sectorsWritten++) {

							// Fill the buffer with random data
							random.NextBytes(buffer);

							inputStream.Write(buffer, 0, buffer.Length);
						}
					}

					// Truncate the file to 0 bytes.
					// This will hide the original file-length  if there are attempts to recover the file.
					inputStream.SetLength(0);

					inputStream.Close();

					// change the dates of the file. original dates will be hidden if there are attempts to recover the file.
					DateTime dt = new DateTime(2037, 1, 1, 0, 0, 0);
					fileSystem.File.SetCreationTime(filename, dt);
					fileSystem.File.SetLastAccessTime(filename, dt);
					fileSystem.File.SetLastWriteTime(filename, dt);

					fileSystem.File.SetCreationTimeUtc(filename, dt);
					fileSystem.File.SetLastAccessTimeUtc(filename, dt);
					fileSystem.File.SetLastWriteTimeUtc(filename, dt);

					// delete the file
					fileSystem.File.Delete(filename);

				}
			} catch(Exception e) {
				throw e;
			}
		}
	}

}