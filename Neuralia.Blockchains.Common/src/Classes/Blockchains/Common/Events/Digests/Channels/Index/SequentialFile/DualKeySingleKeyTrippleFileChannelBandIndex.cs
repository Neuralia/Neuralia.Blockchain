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
	
	/// <summary>
	/// Dual level index with the first being a triple level index pointing into the second single file index.
	/// </summary>
	/// <typeparam name="CHANEL_BANDS"></typeparam>
	public class DualKeySingleKeyTrippleFileChannelBandIndex<CHANEL_BANDS> : SingleKeyTrippleFileChannelBandIndex<CHANEL_BANDS, (long accountId, byte ordinal)>
		where CHANEL_BANDS : struct, Enum, IConvertible {

		public const int INDEX_BAND_FILE_ID = 11001;
		public const int SECOND_INDEX_FILE_ID = 11000;
		
		public const string L2_FILE_NAME = "L2Index";

		public const int L2_ENTRY_COUNT_SIZE = sizeof(byte);
		public const int L2_START_INDEX_SIZE = sizeof(uint);

		public const int L2_INTRO_SIZE = L2_ENTRY_COUNT_SIZE + L2_START_INDEX_SIZE;

		public const int L2_ENTRY_ORDINAL_SIZE = sizeof(byte);
		public const int L2_ENTRY_LENGTH_SIZE = sizeof(uint);

		public const int L2_ENTRY_SIZE = L2_ENTRY_ORDINAL_SIZE + L2_ENTRY_LENGTH_SIZE;

		protected ISequentialChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>> L2IndexProvider;

		private const string L1File_NAME = "{0}-L1Index";

		private string GetL1BandFilename(CHANEL_BANDS band) {
			return string.Format(L1File_NAME, band.ToString().ToLower());
		}
		

		public string GetExpandedL1BandName(CHANEL_BANDS band, uint index) {
			return this.GenerateFullPath(this.Providers[band].NamingProvider.GeneratedExpandedFileName(band.ToString().ToLower(),this.GetL1BandFilename(band), this.scopeFolder, new object[] {index}));

		}

		public string GetArchivedL1BandName(CHANEL_BANDS band, uint index) {
			return this.GenerateFullPath(this.Providers[band].NamingProvider.GeneratedArchivedFileName(band.ToString().ToLower(),this.GetL1BandFilename(band), this.scopeFolder, new object[] {index}));
		}

		public DualKeySingleKeyTrippleFileChannelBandIndex(string filename, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, IFileSystem fileSystem) : base(filename, baseFolder, scopeFolder, groupSize, enabledBands, fileSystem) {
		}

		public string GetL2expandedName(uint index) {
			return this.GenerateFullPath(this.L2IndexProvider.NamingProvider.GeneratedExpandedFileName(this.ChannelBand.ToString().ToLower(), L2_FILE_NAME, this.scopeFolder, new object[] {index}));
		}

		public string GetL2archivedName(uint index) {
			return this.GenerateFullPath(this.L2IndexProvider.NamingProvider.GeneratedArchivedFileName(this.ChannelBand.ToString().ToLower(), L2_FILE_NAME, this.scopeFolder, new object[] {index}));
		}

		public override List<int> GetFileTypes() {
			var fileTypes = base.GetFileTypes();

			// and now the second level index file
			fileTypes.Add(INDEX_BAND_FILE_ID);
			fileTypes.Add(SECOND_INDEX_FILE_ID);

			return fileTypes;
		}

		public override Dictionary<int, SafeArrayHandle> HashFiles(int groupIndex) {
			var results = base.HashFiles(groupIndex);

			// now the L2 index
			
			string idnexArchivedFilename = this.GetArchivedL1BandName(this.ChannelBand, (uint)groupIndex);
			
			results.Add(INDEX_BAND_FILE_ID, this.HashFile(idnexArchivedFilename));

			idnexArchivedFilename = this.GetL2archivedName((uint) groupIndex);

			results.Add(SECOND_INDEX_FILE_ID, this.HashFile(idnexArchivedFilename));

			return results;
		}

		public override SafeArrayHandle GetFileBytes(int fileId, uint partIndex, long offset, int length) {

			if(fileId == INDEX_BAND_FILE_ID) {
				return FileExtensions.ReadBytes(this.GetArchivedL1BandName(this.ChannelBand, partIndex), offset, length, this.fileSystem);
			}
			else if(fileId == SECOND_INDEX_FILE_ID) {
				return FileExtensions.ReadBytes(this.GetL2archivedName(partIndex), offset, length, this.fileSystem);
			}

			return base.GetFileBytes(fileId, partIndex, offset, length);
		}
		
		public override void WriteFileBytes(int fileId, uint partIndex, SafeArrayHandle data) {

			if(fileId == INDEX_BAND_FILE_ID) {
				string archivedName = this.GetArchivedL1BandName(this.ChannelBand, partIndex);
				FileExtensions.EnsureFileExists(archivedName, this.fileSystem);
				FileExtensions.WriteAllBytes(archivedName, data, this.fileSystem);

				return;
			}
			else if(fileId == SECOND_INDEX_FILE_ID) {
				string archivedName = this.GetL2archivedName(partIndex);
				FileExtensions.EnsureFileExists(archivedName, this.fileSystem);
				FileExtensions.WriteAllBytes(archivedName, data, this.fileSystem);

				return;
			}

			base.WriteFileBytes(fileId, partIndex, data);
		}

		protected override void CreateIndexProviders() {
			base.CreateIndexProviders();

			this.L2IndexProvider = new SequentialChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>>(new GroupDigestChannelBandFileNamingProvider<uint>(), this.fileSystem);
		}

		protected override void ResetL1Provider(uint adjustedAccountId, uint groupIndex) {
			this.L1IndexProvider.ResetAllFileSpecs(this.GetExpandedL1BandName(this.ChannelBand, groupIndex), adjustedAccountId, (groupIndex,((groupIndex-1) * this.groupSize)));
		}

		
		protected override List<string> EnsureIndexFilesetExtracted(uint index) {
			var results = base.EnsureIndexFilesetExtracted(index);

			string archived = this.GetArchivedL1BandName(this.ChannelBand, index);
			string expanded = this.GetExpandedL1BandName(this.ChannelBand, index);
			this.EnsureFileExtracted(expanded, archived);
			
			archived = this.GetL2archivedName(index);
			expanded = this.GetL2expandedName(index);
			this.EnsureFileExtracted(expanded, archived);

			results.Add(expanded);

			return results;
		}

		/// <summary>
		/// Query the card
		/// </summary>
		/// <param name="keySet"></param>
		/// <returns></returns>
		/// <remarks>The account id is 0 based, but NOT the key because it is an ordinal ID, so first key is 1</remarks>
		/// <exception cref="InvalidOperationException"></exception>
		public override DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS> QueryCard((long accountId, byte ordinal) keySet) {
			(uint adjustedAccountId, uint groupIndex) adjustedKey = this.AdjustAccountId(keySet.accountId);

			var expanded = this.EnsureFilesetExtracted(adjustedKey.groupIndex);

			// query L1
			var l1Data = this.QueryL1Index(adjustedKey.adjustedAccountId, adjustedKey.groupIndex);

			if(l1Data.Length == 0) {
				// this is an empty card
				return null;
			}

			
			TypeSerializer.Deserialize(l1Data.Span.Slice(0, sizeof(uint)), out uint l2Offset);
			TypeSerializer.Deserialize(l1Data.Span.Slice( sizeof(uint), sizeof(ushort)), out ushort l2OLength);

			
			// now query in l2
			this.L2IndexProvider.SetActiveFilename(this.GetL2expandedName(adjustedKey.groupIndex));

			SafeArrayHandle l2data = this.L2IndexProvider.QueryCard(l2Offset, l2OLength);

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

			return this.QueryFiles(offsets, adjustedKey.groupIndex);
		}

		/// <summary>
		///     return all entries grouped by ordinal
		/// </summary>
		/// <param name="keySet"></param>
		/// <returns></returns>
		public Dictionary<byte, DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS>> QuerySubCards(long accountId) {
			//TODO: this needs a good refactor with above
			(uint adjustedAccountId, uint groupIndex) adjustedKey = this.AdjustAccountId(accountId);

			var expanded = this.EnsureFilesetExtracted(adjustedKey.groupIndex);
			
			// query L1
			var l1data = this.QueryL1Index(adjustedKey.adjustedAccountId, adjustedKey.groupIndex);

			if(l1data.Length == 0) {
				// this is an empty card
				return null;
			}
			
			TypeSerializer.Deserialize(l1data.Span.Slice(0, sizeof(uint)), out uint l2Offset);
			TypeSerializer.Deserialize(l1data.Span.Slice( sizeof(uint), sizeof(ushort)), out ushort l2OLength);

			// now query in l2
			this.L2IndexProvider.SetActiveFilename(this.GetL2expandedName(adjustedKey.groupIndex));

			SafeArrayHandle l2data = this.L2IndexProvider.QueryCard(l2Offset, l2OLength);

			Span<byte> buffer = stackalloc byte[L2_ENTRY_COUNT_SIZE];

			TypeSerializer.Deserialize(l2data.Span.Slice(0, L2_ENTRY_COUNT_SIZE), out byte keyCount);

			buffer = stackalloc byte[L2_START_INDEX_SIZE];
			TypeSerializer.Deserialize(l2data.Span.Slice(L2_ENTRY_COUNT_SIZE, L2_START_INDEX_SIZE), out uint indexStartOffset);

			int offset = L2_INTRO_SIZE;
			uint keyLength = 0;

			var results = new Dictionary<byte, DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS>>();

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

				results.Add(keyOrdinal, this.QueryFiles(subOffsets, adjustedKey.groupIndex));

				indexStartOffset += keyLength;
			}

			return results;
		}
	}
}