using System;
using System.IO;
using System.IO.Abstractions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Extensions {

	public static class FileExtensions {

		public static long FileSize(string filename, IFileSystem fileSystem) {
			return fileSystem.FileInfo.FromFileName(filename).Length;
		}

		/// <summary>
		///     Using File.OpenWrite actually writes at the begining of a file, but does not actually truncate the file, if the
		///     file was bigger than the new write.
		///     this can create all sorts of bugs. better to use this version, which will truncate if the flie exists.
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="bytes"></param>
		public static void OpenWrite(string filename, IByteArray bytes) {
			using(Stream fileStream = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes.Bytes, bytes.Offset, bytes.Length);
			}
		}

		public static void OpenWrite(string filename, IByteArray bytes, long offset, int length, IFileSystem fileSystem) {
			using(Stream fileStream = fileSystem.File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Seek(offset, SeekOrigin.Begin);
				fileStream.Write(bytes.Bytes, bytes.Offset, length);
			}
		}

		public static void OpenWrite(string filename, IByteArray bytes, IFileSystem fileSystem) {
			OpenWrite(filename, bytes, 0, bytes.Length, fileSystem);
		}

		public static void OpenWrite(string filename, IByteArray bytes, long offset, IFileSystem fileSystem) {
			OpenWrite(filename, bytes, offset, bytes.Length, fileSystem);
		}

		public static void OpenWrite(string filename, string text, IFileSystem fileSystem) {
			fileSystem.File.WriteAllText(filename, text);
		}

		public static void OpenWrite(string filename, in Span<byte> bytes) {
			using(Stream fileStream = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
#if (NETSTANDARD2_0)
				fileStream.Write(bytes.ToArray(), 0, bytes.Length);

#elif (NETCOREAPP2_2)
				fileStream.Write(bytes);

#else
	throw new NotImplementedException();
#endif
			}
		}

		public static void OpenWrite(string filename, in Span<byte> bytes, IFileSystem fileSystem) {
			using(Stream fileStream = fileSystem.File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
#if (NETSTANDARD2_0)
				fileStream.Write(bytes.ToArray(), 0, bytes.Length);

#elif (NETCOREAPP2_2)
				fileStream.Write(bytes);

#else
	throw new NotImplementedException();
#endif
			}
		}

		public static void OpenWrite(string filename, in Span<byte> bytes, int offset, IFileSystem fileSystem) {
			using(Stream fileStream = fileSystem.File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes.ToArray(), offset, bytes.Length);
			}
		}

		public static void OpenWrite(string filename, in Span<byte> bytes, int offset, int length, IFileSystem fileSystem) {
			using(Stream fileStream = fileSystem.File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes.ToArray(), offset, length);
			}
		}

		public static void WriteAllText(string filename, string text, IFileSystem fileSystem) {

			fileSystem.File.WriteAllText(filename, text);
		}

		public static void WriteAllBytes(string filename, IByteArray data, IFileSystem fileSystem) {

			fileSystem.File.WriteAllBytes(filename, data.ToExactByteArray());
		}

		public static void WriteAllBytes(string filename, in Span<byte> data, IFileSystem fileSystem) {

			fileSystem.File.WriteAllBytes(filename, data.ToArray());
		}

		public static void OpenAppend(string filename, IByteArray bytes) {
			using(Stream fileStream = File.Open(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes.Bytes, bytes.Offset, bytes.Length);
			}
		}

		public static void OpenAppend(string filename, IByteArray bytes, IFileSystem fileSystem) {
			using(Stream fileStream = fileSystem.File.Open(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
				fileStream.Write(bytes.Bytes, bytes.Offset, bytes.Length);
			}
		}

		public static void OpenAppend(string filename, in Span<byte> bytes) {
			using(Stream fileStream = File.Open(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
#if (NETSTANDARD2_0)
				fileStream.Write(bytes.ToArray(), 0, bytes.Length);

#elif (NETCOREAPP2_2)
				fileStream.Write(bytes);

#else
	throw new NotImplementedException();
#endif

			}
		}

		public static void OpenAppend(string filename, in Span<byte> bytes, IFileSystem fileSystem) {
			using(Stream fileStream = fileSystem.File.Open(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
#if (NETSTANDARD2_0)
				fileStream.Write(bytes.ToArray(), 0, bytes.Length);

#elif (NETCOREAPP2_2)
				fileStream.Write(bytes);

#else
	throw new NotImplementedException();
#endif
			}
		}

		/// <summary>
		///     trucate a file to a certain length
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="length"></param>
		/// <param name="fileSystem"></param>
		public static void Truncate(string filename, long length, IFileSystem fileSystem) {
			using(Stream fs = fileSystem.File.Open(filename, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) {

				fs.SetLength(Math.Max(0, length));
			}
		}

		public static IByteArray ReadBytes(string filename, long start, int count, IFileSystem fileSystem) {
			using(BinaryReader br = new BinaryReader(fileSystem.File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
				br.BaseStream.Seek(start, SeekOrigin.Begin);

				return (ByteArray) br.ReadBytes(count);
			}
		}

		public static IByteArray ReadAllBytes(string filename, IFileSystem fileSystem) {
			return (ByteArray) fileSystem.File.ReadAllBytes(filename);
		}

		public static string ReadAllText(string filename, IFileSystem fileSystem) {
			return fileSystem.File.ReadAllText(filename);
		}

		public static void EnsureDirectoryStructure(string directoryName, IFileSystem fileSystem) {

			if(!fileSystem.Directory.Exists(directoryName)) {
				fileSystem.Directory.CreateDirectory(directoryName);
			}
		}

		public static void EnsureFileExists(string filename, IFileSystem fileSystem) {
			string directory = fileSystem.Path.GetDirectoryName(filename);

			EnsureDirectoryStructure(directory, fileSystem);

			if(!fileSystem.File.Exists(filename)) {
				using(fileSystem.File.Create(filename)) {
					// nothing to do
				}
			}
		}
	}
}