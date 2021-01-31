using System;
using System.IO;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Extensions {

	public static class FileExtensions {

		public static long FileSize(string filename, FileSystemWrapper fileSystem) {
			return fileSystem.GetFileLength(filename);
		}

		/// <summary>
		///     Using File.OpenWrite actually writes at the begining of a file, but does not actually truncate the file, if the
		///     file was bigger than the new write.
		///     this can create all sorts of bugs. better to use this version, which will truncate if the file exists.
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="bytes"></param>
		public static void OpenWrite(string filename, SafeArrayHandle bytes) {
			using(Stream fileStream = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes.Bytes, bytes.Offset, bytes.Length);
			}
		}

		public static async Task OpenWriteAsync(string filename, SafeArrayHandle bytes) {
			await using(Stream fileStream = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				await fileStream.WriteAsync(bytes.Bytes, bytes.Offset, bytes.Length).ConfigureAwait(false);
			}
		}

		public static void OpenWrite(string filename, SafeArrayHandle bytes, long offset, int length, FileSystemWrapper fileSystem) {
			using(Stream fileStream = fileSystem.OpenFile(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Seek(offset, SeekOrigin.Begin);
				fileStream.Write(bytes.Bytes, bytes.Offset, length);
			}
		}

		public static async Task OpenWriteAsync(string filename, SafeArrayHandle bytes, long offset, int length, FileSystemWrapper fileSystem) {
			await using(Stream fileStream = fileSystem.OpenFile(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Seek(offset, SeekOrigin.Begin);
				await fileStream.WriteAsync(bytes.Bytes, bytes.Offset, length).ConfigureAwait(false);
			}
		}

		public static void OpenWrite(string filename, SafeArrayHandle bytes, FileSystemWrapper fileSystem) {
			OpenWrite(filename, bytes, 0, bytes.Length, fileSystem);
		}

		public static Task OpenWriteAsync(string filename, SafeArrayHandle bytes, FileSystemWrapper fileSystem) {
			return OpenWriteAsync(filename, bytes, 0, bytes.Length, fileSystem);
		}
		
		public static async Task OpenWriteAsync(string filename, SafeArrayHandle[] bytesSet, FileSystemWrapper fileSystem) {
			long offset = 0;
			await using(Stream fileStream = fileSystem.OpenFile(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Seek(offset, SeekOrigin.Begin);
				foreach(var bytes in bytesSet) {
					await fileStream.WriteAsync(bytes.Bytes, bytes.Offset, bytes.Length).ConfigureAwait(false);
				}
			}
		}

		public static void OpenWrite(string filename, SafeArrayHandle bytes, long offset, FileSystemWrapper fileSystem) {
			OpenWrite(filename, bytes, offset, bytes.Length, fileSystem);
		}

		public static Task OpenWriteAsync(string filename, SafeArrayHandle bytes, long offset, FileSystemWrapper fileSystem) {
			return OpenWriteAsync(filename, bytes, offset, bytes.Length, fileSystem);
		}

		public static void OpenWrite(string filename, string text, FileSystemWrapper fileSystem) {
			fileSystem.WriteAllText(filename, text);
		}

		public static Task OpenWriteAsync(string filename, string text, FileSystemWrapper fileSystem) {
			return fileSystem.WriteAllTextAsync(filename, text);
		}

		public static void OpenWrite(string filename, in Span<byte> bytes) {
			using(Stream fileStream = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {

				fileStream.Write(bytes);
			}
		}

		public static void OpenWrite(string filename, in Span<byte> bytes, FileSystemWrapper fileSystem) {
			using(Stream fileStream = fileSystem.OpenFile(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {

				fileStream.Write(bytes);
			}
		}

		public static void OpenWrite(string filename, in Span<byte> bytes, int offset, FileSystemWrapper fileSystem) {
			using(Stream fileStream = fileSystem.OpenFile(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes.ToArray(), offset, bytes.Length);
			}
		}

		public static void OpenWrite(string filename, in Span<byte> bytes, int offset, int length, FileSystemWrapper fileSystem) {
			using(Stream fileStream = fileSystem.OpenFile(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes.ToArray(), offset, length);
			}
		}

		public static void WriteAllText(string filename, string text, FileSystemWrapper fileSystem) {

			fileSystem.WriteAllText(filename, text);
		}

		public static void WriteAllBytes(string filename, SafeArrayHandle data) {
			using FileSystemWrapper fileSystem = FileSystemWrapper.CreatePhysical();
			WriteAllBytes(filename, data, fileSystem);
		}

		public static void WriteAllBytes(string filename, SafeArrayHandle data, FileSystemWrapper fileSystem) {

			fileSystem.WriteAllBytes(filename, data.ToExactByteArray());
		}

		public static void WriteAllBytes(string filename, in Span<byte> data, FileSystemWrapper fileSystem) {

			fileSystem.WriteAllBytes(filename, data.ToArray());
		}

		public static Task WriteAllBytesAsync(string filename, in Span<byte> data, FileSystemWrapper fileSystem) {

			return fileSystem.WriteAllBytesAsync(filename, data.ToArray());
		}

		public static void OpenAppend(string filename, SafeArrayHandle bytes, FileSystemWrapper fileSystem) {
			using(Stream fileStream = fileSystem.OpenFile(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes.Bytes, bytes.Offset, bytes.Length);
			}
		}

		public static void OpenAppend(string filename, in Span<byte> bytes, FileSystemWrapper fileSystem) {
			using(Stream fileStream = fileSystem.OpenFile(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes);
			}
		}

		/// <summary>
		///     trucate a file to a certain length
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="length"></param>
		/// <param name="fileSystem"></param>
		public static void Truncate(string filename, long length, FileSystemWrapper fileSystem) {
			using(Stream fs = fileSystem.OpenFile(filename, FileMode.Open, FileAccess.Write, FileShare.Write)) {

				fs.SetLength(Math.Max(0, length));
			}
		}

		public static SafeArrayHandle ReadBytes(string filename, long start, int count, FileSystemWrapper fileSystem) {
			using(BinaryReader br = new BinaryReader(fileSystem.OpenFile(filename, FileMode.Open, FileAccess.Read, FileShare.Read))) {
				br.BaseStream.Seek(start, SeekOrigin.Begin);

				return SafeArrayHandle.WrapAndOwn(br.ReadBytes(count));
			}
		}

		public static async Task<SafeArrayHandle> ReadBytesAsync(string filename, long start, int count, FileSystemWrapper fileSystem) {
			await using(Stream stream = fileSystem.OpenFile(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				stream.Seek(start, SeekOrigin.Begin);

				SafeArrayHandle bytes = SafeArrayHandle.Create(count);
				await stream.ReadAsync(bytes.Memory).ConfigureAwait(false);

				return bytes;
			}
		}

		public static SafeArrayHandle ReadAllBytes(string filename, FileSystemWrapper fileSystem) {
			return SafeArrayHandle.WrapAndOwn(fileSystem.ReadAllBytes(filename));
		}

		/// <summary>
		///     fastest known implementation of the activity
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="fileSystem"></param>
		/// <returns></returns>
		public static async Task<SafeArrayHandle> ReadAllBytesFast(string filename, FileSystemWrapper fileSystem) {
			await using(Stream fs = fileSystem.OpenFile(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				await using(BufferedStream bs = new BufferedStream(fs)) {

					SafeArrayHandle buffer = SafeArrayHandle.Create((int) bs.Length);
					long bytesLeft = buffer.Length;
					int offset = 0;

					while(bytesLeft > 0) {
						// Read may return anything from 0 to numBytesToRead.
						int bytesRead = await bs.ReadAsync(buffer.Bytes, buffer.Offset + offset, 4096).ConfigureAwait(false);

						// The end of the file is reached.
						if(bytesRead == 0) {
							break;
						}

						offset += bytesRead;
						bytesLeft -= bytesRead;
					}

					return buffer;
				}
			}
		}

		public static async Task<SafeArrayHandle> ReadAllBytesFastAsync(string filename, FileSystemWrapper fileSystem) {
			await using(Stream fs = fileSystem.OpenFile(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				await using(BufferedStream bs = new BufferedStream(fs)) {

					SafeArrayHandle buffer = SafeArrayHandle.Create((int) bs.Length);
					long bytesLeft = buffer.Length;
					int offset = 0;

					while(bytesLeft > 0) {
						// Read may return anything from 0 to numBytesToRead.
						int bytesRead = await bs.ReadAsync(buffer.Bytes, buffer.Offset + offset, 4096).ConfigureAwait(false);

						// The end of the file is reached.
						if(bytesRead == 0) {
							break;
						}

						offset += bytesRead;
						bytesLeft -= bytesRead;
					}

					return buffer;
				}
			}
		}

		public static string ReadAllText(string filename, FileSystemWrapper fileSystem) {
			return fileSystem.ReadAllText(filename);
		}

		public static async Task CopyAsync(string source, string destination)
		{
			const FileOptions fileOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;
			const int bufferSize = 4096;

			await using Stream sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, fileOptions);
			await using Stream destinationStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, fileOptions);

			await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);
		}
		
		public static async Task MoveAsync(string source, string destination) {
			await CopyAsync(source, destination).ConfigureAwait(false);
			
			File.Delete(source);
		}
		
		public static void EnsureDirectoryStructure(string directoryName) {

			using FileSystemWrapper fileSystem = FileSystemWrapper.CreatePhysical();
			EnsureDirectoryStructure(directoryName, fileSystem);
		}

		public static void EnsureDirectoryStructure(string directoryName, FileSystemWrapper fileSystem) {

			if(!fileSystem.DirectoryExists(directoryName)) {
				fileSystem.CreateDirectory(directoryName);
			}
		}

		public static void EnsureFileExists(string filename) {

			using FileSystemWrapper fileSystem = FileSystemWrapper.CreatePhysical();
			EnsureFileExists(filename, fileSystem);
		}

		public static void EnsureFileExists(string filename, FileSystemWrapper fileSystem) {
			string directory = Path.GetDirectoryName(filename);

			EnsureDirectoryStructure(directory, fileSystem);

			if(!fileSystem.FileExists(filename)) {
				fileSystem.CreateEmptyFile(filename);
			}
		}
	}
}