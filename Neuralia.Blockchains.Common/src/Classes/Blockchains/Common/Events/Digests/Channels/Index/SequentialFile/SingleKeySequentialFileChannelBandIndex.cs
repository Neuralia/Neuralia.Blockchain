using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.SequentialFile {

	public abstract class SingleKeySequentialFileChannelBandIndex<CHANEL_BANDS, INPUT_KEY> : SequentialFileChannelBandIndex<CHANEL_BANDS, INPUT_KEY>
		where CHANEL_BANDS : struct, Enum, IConvertible {

		public const int INDEX_FILE_ID = 10000;

		public const string L1_FILE_NAME = "L1Index";

		public const int L1_START_INDEX_SIZE = sizeof(uint);
		public const int L1_LENGTH_INDEX_SIZE = sizeof(ushort);

		public const int L1_INDEX_ENTRY_SIZE = L1_START_INDEX_SIZE + L1_LENGTH_INDEX_SIZE;

		//TODO: in the future, replace the L1 index with a sort of B_Tree
		private readonly int groupSize;

		protected ISequentialChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>> L1IndexProvider;

		public SingleKeySequentialFileChannelBandIndex(string filename, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, IFileSystem fileSystem) : base(filename, baseFolder, scopeFolder, enabledBands, fileSystem) {
			this.groupSize = groupSize;
		}

		public string GetL1ExpandedName(uint index) {
			return this.GenerateFullPath(this.L1IndexProvider.NamingProvider.GeneratedExpandedFileName(this.ChannelBand.ToString().ToLower(), L1_FILE_NAME, this.scopeFolder, new object[] {index}));
		}

		public string GetL1ArchivedName(uint index) {
			return this.GenerateFullPath(this.L1IndexProvider.NamingProvider.GeneratedArchivedFileName(this.ChannelBand.ToString().ToLower(), L1_FILE_NAME, this.scopeFolder, new object[] {index}));
		}

		protected override void CreateIndexProviders() {
			this.L1IndexProvider = new SequentialChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>>(new GroupDigestChannelBandFileNamingProvider<uint>(), this.fileSystem);
		}

		public override Dictionary<int, SafeArrayHandle> HashFiles(int groupIndex) {
			var results = new Dictionary<int, SafeArrayHandle>();

			foreach(CHANEL_BANDS band in this.EnabledBands) {
				string archivedFilename = this.GetArchivedBandName(band, (uint) groupIndex);

				results.Add(band.ToInt32(null), this.HashFile(archivedFilename));
			}

			// now the index
			string indexArchivedFilename = this.GetL1ArchivedName((uint) groupIndex);

			results.Add(INDEX_FILE_ID, this.HashFile(indexArchivedFilename));

			return results;
		}

		public override SafeArrayHandle GetFileBytes(int fileId, uint partIndex, long offset, int length) {

			string archivedFilename = "";

			if(fileId == INDEX_FILE_ID) {
				archivedFilename = this.GetL1ArchivedName(partIndex);
			} else {

				var bands = this.EnabledBands.ToDictionary(b => b.ToInt32(null), b => b);

				if(bands.ContainsKey(fileId)) {
					archivedFilename = this.GetArchivedBandName(bands[fileId], partIndex);
				}
			}

			if(string.IsNullOrWhiteSpace(archivedFilename)) {
				throw new ApplicationException("Failed to find file");
			}

			return FileExtensions.ReadBytes(archivedFilename, offset, length, this.fileSystem);
		}
		
		public override void WriteFileBytes(int fileId, uint partIndex, SafeArrayHandle data) {

			string archivedFilename = "";

			if(fileId == INDEX_FILE_ID) {
				archivedFilename = this.GetL1ArchivedName(partIndex);
			} else {

				var bands = this.EnabledBands.ToDictionary(b => b.ToInt32(null), b => b);

				if(bands.ContainsKey(fileId)) {
					archivedFilename = this.GetArchivedBandName(bands[fileId], partIndex);
				}
			}

			if(string.IsNullOrWhiteSpace(archivedFilename)) {
				throw new ApplicationException("Failed to find file");
			}

			FileExtensions.EnsureFileExists(archivedFilename, this.fileSystem);
			FileExtensions.WriteAllBytes(archivedFilename, data, this.fileSystem);
		}

		protected (uint adjustedAccountId, uint groupIndex) AdjustAccountId(long accountId) {
			// make it 0 based
			accountId -= 1;

			uint index = (uint) (accountId / this.groupSize);

			uint adjustedAccountId = (uint) (accountId - (index * this.groupSize));

			// index is 1 based
			return (adjustedAccountId, index + 1);

		}

		public override List<int> GetFileTypes() {
			var fileTypes = base.GetFileTypes();

			// and now the idnex file
			fileTypes.Add(INDEX_FILE_ID);

			return fileTypes;
		}

		protected override List<string> EnsureIndexFilesetExtracted(uint index) {
			string archived = this.GetL1ArchivedName(index);
			string expanded = this.GetL1ExpandedName(index);
			this.EnsureFileExtracted(expanded, archived);

			return new[] {expanded}.ToList();
		}

		protected (uint offset, ushort length) QueryL1Index(uint adjustedAccountId, uint groupIndex) {
			this.L1IndexProvider.SetActiveFilename(this.GetL1ExpandedName(groupIndex));

			// query L1
			long fileOffset = L1_INDEX_ENTRY_SIZE * adjustedAccountId;
			SafeArrayHandle data = this.L1IndexProvider.QueryCard((uint) fileOffset, L1_INDEX_ENTRY_SIZE);

			if(data.IsEmpty) {
				return (0, 0);
			}

			Span<byte> buffer = stackalloc byte[L1_INDEX_ENTRY_SIZE];

			TypeSerializer.Deserialize(data.Span.Slice(0, L1_START_INDEX_SIZE), out uint offset);
			TypeSerializer.Deserialize(data.Span.Slice(L1_START_INDEX_SIZE, L1_LENGTH_INDEX_SIZE), out ushort length);

			return (offset, length);
		}
	}

	public class SingleKeySequentialFileChannelBandIndex<CHANEL_BANDS> : SingleKeySequentialFileChannelBandIndex<CHANEL_BANDS, long>
		where CHANEL_BANDS : struct, Enum, IConvertible {

		public SingleKeySequentialFileChannelBandIndex(string filename, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, IFileSystem fileSystem) : base(filename, baseFolder, scopeFolder, groupSize, enabledBands, fileSystem) {
		}

		public override DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS> QueryCard(long keySet) {
			(uint adjustedAccountId, uint groupIndex) adjustedKey = this.AdjustAccountId(keySet);

			var expanded = this.EnsureFilesetExtracted(adjustedKey.groupIndex);

			this.L1IndexProvider.SetActiveFilename(this.GetL1ExpandedName(adjustedKey.groupIndex));

			// query L1
			(uint offset, ushort length) l1Offsets = this.QueryL1Index(adjustedKey.adjustedAccountId, adjustedKey.groupIndex);

			if(l1Offsets.length == 0) {
				// this is an empty card
				return null;
			}

			var offsets = new DigestChannelBandEntries<(uint offset, uint length), CHANEL_BANDS>();

			foreach(CHANEL_BANDS band in this.EnabledBands) {

				offsets[band] = l1Offsets;

			}

			return this.QueryFiles(offsets, adjustedKey.groupIndex);
		}
	}
}