using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.SequentialFile {
	public class DualKeySingleKeySequentialFileChannelBandIndex<CHANEL_BANDS> : SingleKeySequentialFileChannelBandIndex<CHANEL_BANDS, (long accountId, byte ordinal)>
		where CHANEL_BANDS : struct, Enum, IConvertible {

		public const int SECOND_INDEX_FILE_ID = 10001;
		public const string L2_FILE_NAME = "L2Index";

		public const int L2_ENTRY_COUNT_SIZE = sizeof(byte);
		public const int L2_START_INDEX_SIZE = sizeof(uint);

		public const int L2_INTRO_SIZE = L2_ENTRY_COUNT_SIZE + L2_START_INDEX_SIZE;

		public const int L2_ENTRY_ORDINAL_SIZE = sizeof(byte);
		public const int L2_ENTRY_LENGTH_SIZE = sizeof(uint);

		public const int L2_ENTRY_SIZE = L2_ENTRY_ORDINAL_SIZE + L2_ENTRY_LENGTH_SIZE;

		protected ISequentialChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>> L2IndexProvider;

		public DualKeySingleKeySequentialFileChannelBandIndex(string filename, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, IFileSystem fileSystem) : base(filename, baseFolder, scopeFolder, groupSize, enabledBands, fileSystem) {
		}

		public string GetL2expandedName(uint index) {
			return this.GenerateFullPath(this.L2IndexProvider.NamingProvider.GeneratedExpandedFileName(L2_FILE_NAME, this.scopeFolder, new object[] {index}));
		}

		public string GetL2archivedName(uint index) {
			return this.GenerateFullPath(this.L2IndexProvider.NamingProvider.GeneratedArchivedFileName(L2_FILE_NAME, this.scopeFolder, new object[] {index}));
		}

		public override List<int> GetFileTypes() {
			var fileTypes = base.GetFileTypes();

			// and now the second level index file
			fileTypes.Add(SECOND_INDEX_FILE_ID);

			return fileTypes;
		}

		public override Dictionary<int, IByteArray> HashFiles(int groupIndex) {
			var results = base.HashFiles(groupIndex);

			// now the L2 index
			string idnexArchivedFilename = this.GetL2archivedName((uint) groupIndex);

			results.Add(SECOND_INDEX_FILE_ID, this.HashFile(idnexArchivedFilename));

			return results;
		}

		public override IByteArray GetFileBytes(int fileId, uint partIndex, long offset, int length) {

			if(fileId == SECOND_INDEX_FILE_ID) {
				return FileExtensions.ReadBytes(this.GetL2archivedName(partIndex), offset, length, this.fileSystem);
			}

			return base.GetFileBytes(fileId, partIndex, offset, length);

		}

		protected override void CreateIndexProviders() {
			base.CreateIndexProviders();

			this.L2IndexProvider = new SequentialChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>>(new GroupDigestChannelBandFileNamingProvider<uint>(), this.fileSystem);
		}

		protected override List<string> EnsureIndexFilesetExtracted(uint index) {
			var results = base.EnsureIndexFilesetExtracted(index);

			string archived = this.GetL2archivedName(index);
			string expanded = this.GetL2expandedName(index);
			this.EnsureFileExtracted(expanded, archived);

			results.Add(expanded);

			return results;
		}

		public override DigestChannelBandEntries<IByteArray, CHANEL_BANDS> QueryCard((long accountId, byte ordinal) keySet) {
			(uint adjustedAccountId, uint index) adjustedKey = this.AdjustAccountId(keySet.accountId);

			var expanded = this.EnsureFilesetExtracted(adjustedKey.index);

			this.L1IndexProvider.SetActiveFilename(this.GetL1expandedName(adjustedKey.index));

			// query L1
			(uint offset, ushort length) l1Offsets = this.QueryL1Index(adjustedKey.adjustedAccountId, adjustedKey.index);

			if(l1Offsets.length == 0) {
				// this is an empty card
				return null;
			}

			// now query in l2
			this.L2IndexProvider.SetActiveFilename(this.GetL2expandedName(adjustedKey.index));

			IByteArray l2data = this.L2IndexProvider.QueryCard(l1Offsets.offset, l1Offsets.length);

			Span<byte> buffer = stackalloc byte[L2_ENTRY_COUNT_SIZE];

			TypeSerializer.Deserialize(l2data.Span.Slice(0, L2_ENTRY_COUNT_SIZE), out byte keyCount);

			buffer = stackalloc byte[L2_START_INDEX_SIZE];
			TypeSerializer.Deserialize(l2data.Span.Slice(L2_ENTRY_COUNT_SIZE, L2_START_INDEX_SIZE), out uint indexStartOffset);

			int offset = L2_INTRO_SIZE;
			uint keyLength = 0;

			bool found = false;

			for(int i = 0; i < keyCount; i++) {
				int length = L2_ENTRY_ORDINAL_SIZE;
				buffer = stackalloc byte[length];
				TypeSerializer.Deserialize(l2data.Span.Slice(offset, length), out byte keyOrdinal);
				offset += length;

				length = L2_ENTRY_LENGTH_SIZE;
				buffer = stackalloc byte[length];
				TypeSerializer.Deserialize(l2data.Span.Slice(offset, length), out keyLength);
				offset += length;

				if(keyOrdinal == keySet.ordinal) {
					// found it
					found = true;

					break;
				}

				indexStartOffset += keyLength;
			}

			if(!found) {
				throw new InvalidOperationException("Ordinal was not found in the index");
			}

			var offsets = new DigestChannelBandEntries<(uint offset, uint length), CHANEL_BANDS>();

			foreach(CHANEL_BANDS band in this.EnabledBands) {

				offsets[band] = (indexStartOffset, keyLength);
			}

			return this.QueryFiles(offsets, adjustedKey.index);
		}

		/// <summary>
		///     return all entries grouped by ordinal
		/// </summary>
		/// <param name="keySet"></param>
		/// <returns></returns>
		public Dictionary<byte, DigestChannelBandEntries<IByteArray, CHANEL_BANDS>> QuerySubCards(long accountId) {
			//TODO: this needs a good refactor with above
			(uint adjustedAccountId, uint index) adjustedKey = this.AdjustAccountId(accountId);

			var expanded = this.EnsureFilesetExtracted(adjustedKey.index);

			this.L1IndexProvider.SetActiveFilename(this.GetL1expandedName(adjustedKey.index));

			// query L1
			(uint offset, ushort length) l1Offsets = this.QueryL1Index(adjustedKey.adjustedAccountId, adjustedKey.index);

			if(l1Offsets.length == 0) {
				// this is an empty card
				return null;
			}

			// now query in l2
			this.L2IndexProvider.SetActiveFilename(this.GetL2expandedName(adjustedKey.index));

			IByteArray l2data = this.L2IndexProvider.QueryCard(l1Offsets.offset, l1Offsets.length);

			Span<byte> buffer = stackalloc byte[L2_ENTRY_COUNT_SIZE];

			TypeSerializer.Deserialize(l2data.Span.Slice(0, L2_ENTRY_COUNT_SIZE), out byte keyCount);

			buffer = stackalloc byte[L2_START_INDEX_SIZE];
			TypeSerializer.Deserialize(l2data.Span.Slice(L2_ENTRY_COUNT_SIZE, L2_START_INDEX_SIZE), out uint indexStartOffset);

			int offset = L2_INTRO_SIZE;
			uint keyLength = 0;

			var results = new Dictionary<byte, DigestChannelBandEntries<IByteArray, CHANEL_BANDS>>();

			bool found = false;

			for(int i = 0; i < keyCount; i++) {
				int length = L2_ENTRY_ORDINAL_SIZE;
				buffer = stackalloc byte[length];
				TypeSerializer.Deserialize(l2data.Span.Slice(offset, length), out byte keyOrdinal);
				offset += length;

				length = L2_ENTRY_LENGTH_SIZE;
				buffer = stackalloc byte[length];
				TypeSerializer.Deserialize(l2data.Span.Slice(offset, length), out keyLength);
				offset += length;

				var subOffsets = new DigestChannelBandEntries<(uint offset, uint length), CHANEL_BANDS>();

				foreach(CHANEL_BANDS band in this.EnabledBands) {

					subOffsets[band] = (indexStartOffset, keyLength);
				}

				results.Add(keyOrdinal, this.QueryFiles(subOffsets, adjustedKey.index));

				indexStartOffset += keyLength;
			}

			return results;
		}
	}
}