using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Core.Tools {

	/// <summary>
	///     This class ensures a custum "tarball" like format (Neuralia ARchive == 'nar') to ensure very fast and sohrt lived
	///     backups.
	/// </summary>
	/// <remarks>
	///     this file format is not designed for long term backups. It is meant to be very fast and short lived, with a
	///     single restore in a very short time frame in case of emergency restore. This file has NO data corruption recovery!
	/// </remarks>
	public class Narballer {

		public const string EXTENSION = "nar";
		public const string HEADER_FORMAT = "{0}.header";
		public const string BODY_FORMAT = "{0}.body";
		public const string PACKAGE_NAME = "package";

		private readonly string basepath;
		private readonly List<string> files = new List<string>();
		private readonly FileSystemWrapper fileSystem;

		public Narballer(string basepath, FileSystemWrapper fileSystem) {
			this.basepath = Path.GetFullPath(basepath);
			this.fileSystem = fileSystem;
		}

		public void AddFile(string filepath) {

			this.files.Add(Path.GetRelativePath(this.basepath, filepath));
		}

		public bool Package(string outpath) {

			NarballHeader header = new NarballHeader();

			// first we start by nbuilding the header
			foreach(string file in this.files) {

				string filepath = this.MakeFullPath(file);

				if(!this.fileSystem.FileExists(filepath)) {
					continue;
				}

				header.AddFile(file, (int) FileExtensions.FileSize(filepath, this.fileSystem));
			}

			if(!header.Files.Any()) {
				return false;
			}

			(string headerFilePath, string bodyFilePath) = GetPackageFiles(outpath);

			FileExtensions.EnsureFileExists(headerFilePath, this.fileSystem);
			FileExtensions.EnsureFileExists(bodyFilePath, this.fileSystem);

			// ensure the file length
			long fullSize = header.FullSize;

			// now write the body
			long offset = 0;

			using(Stream fs = this.fileSystem.OpenFile(bodyFilePath, FileMode.Open, FileAccess.Write, FileShare.Write)) {

				fs.Seek(fullSize - 1, SeekOrigin.Begin);
				fs.WriteByte(0);
				fs.Seek(0, SeekOrigin.Begin);

				foreach(NarballHeader.NarballHeaderEntry file in header.Files) {

					string filepath = this.MakeFullPath(file.Name);

					SafeArrayHandle fileBytes = FileExtensions.ReadAllBytes(filepath, this.fileSystem);

					file.Offset = offset;
					file.Hash = this.HashBytes(fileBytes);

					fs.Write(fileBytes.Entry.ToArray(), 0, fileBytes.Length);
					offset += fileBytes.Length;
				}
			}

			using IDataDehydrator headerDehydrator = DataSerializationFactory.CreateDehydrator();

			header.Dehydrate(headerDehydrator);

			SafeArrayHandle headerBytes = headerDehydrator.ToArray();

			FileExtensions.WriteAllBytes(headerFilePath, headerBytes, this.fileSystem);

			return true;
		}

		public void Restore(string outpath, string sourcepath, List<string> filter, bool clearSource = true) {

			(string headerFilePath, string bodyFilePath) = GetPackageFiles(sourcepath);

			if(!this.fileSystem.DirectoryExists(sourcepath)) {
				throw new ApplicationException($"Package directory {sourcepath} did not exist.");
			}

			if(!this.fileSystem.FileExists(headerFilePath)) {
				throw new ApplicationException($"Package header file {headerFilePath} was not found. invalid package.");
			}

			if(!this.fileSystem.FileExists(bodyFilePath)) {
				throw new ApplicationException($"Package body file {bodyFilePath} was not found. invalid package.");
			}

			if(clearSource && this.fileSystem.DirectoryExists(outpath)) {
				this.fileSystem.DeleteDirectory(outpath, true);
			}

			FileExtensions.EnsureDirectoryStructure(outpath, this.fileSystem);

			SafeArrayHandle headerBytes = FileExtensions.ReadAllBytes(headerFilePath, this.fileSystem);
			NarballHeader header = new NarballHeader();

			using(IDataRehydrator headerRehydrator = DataSerializationFactory.CreateRehydrator(headerBytes)) {
				header.Rehydrate(headerRehydrator);
			}

			if(!header.Files.Any()) {
				throw new ApplicationException("No files found in the header.");
			}

			using(BinaryReader br = new BinaryReader(this.fileSystem.OpenFile(bodyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))) {

				List<Action> actions = new List<Action>();

				foreach(NarballHeader.NarballHeaderEntry file in header.Files) {

					actions.Add(() => {
						try {

							if((filter == null) || filter.Any(f => f.EndsWith(file.Name))) {
								br.BaseStream.Seek(file.Offset, SeekOrigin.Begin);
								ByteArray bytes = ByteArray.WrapAndOwn(br.ReadBytes(file.Length));

								if(file.Hash != this.HashBytes(bytes)) {
									throw new ApplicationException($"hash for file {file.Name} was different. invalid data.");
								}

								string fullname = Path.Combine(outpath, file.Name);

								try {
									if(this.fileSystem.FileExists(fullname)) {
										this.fileSystem.DeleteFile(fullname);
									}
								} catch {
									// give it a try, but continue if it fails just in case
								}

								FileExtensions.EnsureDirectoryStructure(Path.GetDirectoryName(fullname), this.fileSystem);

								FileExtensions.WriteAllBytes(fullname, bytes, this.fileSystem);
							}
						} catch(Exception ex) {
							NLog.Default.Fatal(ex, $"Failed to restore wallet file {file.Name} from nar package.");

							throw;
						}
					});
				}

				IndependentActionRunner.Run(actions.ToArray());
			}
		}

		public void Clear(string packagePath) {
			if(this.fileSystem.DirectoryExists(packagePath)) {
				this.fileSystem.DeleteDirectory(packagePath, true);
			}
		}

		public static bool PackageFilesValid(string packagePath, FileSystemWrapper fileSystem) {
			(string headerFilePath, string bodyFilePath) = GetPackageFiles(packagePath);

			if(!fileSystem.DirectoryExists(packagePath)) {
				return false;
			}

			if(!fileSystem.FileExists(headerFilePath)) {
				return false;
			}

			if(!fileSystem.FileExists(bodyFilePath)) {
				return false;
			}

			return true;
		}

		public static string[] GetPackageFilesList(string packagePath) {

			(string headerFile, string bodyFile) = GetPackageFiles(packagePath);

			return new[] {headerFile, bodyFile};
		}

		public static (string headerFile, string bodyFile) GetPackageFiles(string packagePath) {
			(string headerFile, string bodyFile) = MakeFileNames(PACKAGE_NAME);

			string headerFilePath = Path.Combine(packagePath, headerFile);
			string bodyFilePath = Path.Combine(packagePath, bodyFile);

			return (headerFilePath, bodyFilePath);
		}

		private int HashBytes(SafeArrayHandle fileBytes) {

			using(HashNodeList nodes = new HashNodeList()) {
				nodes.Add(fileBytes);

				return HashingUtils.HashxxTree32(nodes);
			}
		}

		private static (string header, string body) MakeFileNames(string filename) {
			return ($"{string.Format(HEADER_FORMAT, filename, EXTENSION)}.{EXTENSION}", $"{string.Format(BODY_FORMAT, filename, EXTENSION)}.{EXTENSION}");
		}

		private string MakeFullPath(string file) {
			return Path.Combine(this.basepath, file);
		}

		private class NarballHeader : IVersionable<SimpleUShort>, IBinarySerializable {

			public long FullSize => this.Files.Sum(f => f.Length);

			public DateTime Timestamp { get; private set; } = DateTimeEx.CurrentTime;

			public List<NarballHeaderEntry> Files { get; } = new List<NarballHeaderEntry>();

			public void Rehydrate(IDataRehydrator rehydrator) {

				this.Version.Rehydrate(rehydrator);

				this.Timestamp = rehydrator.ReadDateTime();

				this.Files.Clear();
				int count = rehydrator.ReadUShort();

				for(int i = 0; i < count; i++) {

					NarballHeaderEntry entry = new NarballHeaderEntry();
					entry.Rehydrate(rehydrator);
					this.Files.Add(entry);
				}

				this.RebuildOffsets();
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				this.Version.Dehydrate(dehydrator);
				dehydrator.Write(this.Timestamp);

				dehydrator.Write((ushort) this.Files.Count);

				foreach(NarballHeaderEntry file in this.Files) {

					file.Dehydrate(dehydrator);
				}
			}

			public HashNodeList GetStructuresArray() {
				throw new NotImplementedException();
			}

			public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
				throw new NotImplementedException();
			}

			public ComponentVersion<SimpleUShort> Version { get; } = new ComponentVersion<SimpleUShort>(1, 1, 1);

			public void RebuildOffsets() {
				long offset = 0;

				foreach(NarballHeaderEntry file in this.Files) {

					file.Offset = offset;
					offset += file.Length;
				}
			}

			public void AddFile(string name, int length) {

				this.Files.Add(new NarballHeaderEntry {Name = name, Length = length});
			}

			public class NarballHeaderEntry : IBinarySerializable {

				public string Name { get; set; }
				public long Offset { get; set; }
				public int Length { get; set; }
				public int Hash { get; set; }

				public void Rehydrate(IDataRehydrator rehydrator) {
					this.Name = rehydrator.ReadString();
					this.Length = rehydrator.ReadInt();
					this.Hash = rehydrator.ReadInt();
				}

				public void Dehydrate(IDataDehydrator dehydrator) {

					dehydrator.Write(this.Name);
					dehydrator.Write(this.Length);
					dehydrator.Write(this.Hash);
				}

				public override string ToString() {
					return this.Name;
				}
			}
		}
	}

}