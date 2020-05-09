using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.SequentialFile {

	/// <summary>
	///     A special index with 3 levels to reduce space requirements.
	/// </summary>
	/// <typeparam name="CHANEL_BANDS"></typeparam>
	/// <typeparam name="INPUT_KEY"></typeparam>
	public abstract class SingleKeyTrippleFileChannelBandIndex<CHANEL_BANDS, INPUT_KEY> : SequentialFileChannelBandIndex<CHANEL_BANDS, INPUT_KEY>
		where CHANEL_BANDS : struct, Enum, IConvertible {

		public const int INDEX_FILE1_ID = 10001;
		public const int INDEX_FILE2_ID = 10002;
		public const int INDEX_FILE3_ID = 10003;

		//TODO: in the future, replace the L1 index with a sort of B_Tree
		protected readonly int groupSize;

		protected ITrippleChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>, uint> L1IndexProvider;

		public SingleKeyTrippleFileChannelBandIndex(string filename, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, FileSystemWrapper fileSystem) : base(filename, baseFolder, scopeFolder, enabledBands, fileSystem) {
			this.groupSize = groupSize;
		}

		public string GetL1ExpandedL1Name(uint index) {
			return this.L1IndexProvider.GetExpandedL1IndexFile(this.ChannelBand.ToString(), (index, 0));
		}

		public string GetL2ExpandedL1Name(uint index) {
			return this.L1IndexProvider.GetExpandedL2IndexFile(this.ChannelBand.ToString(), (index, 0));
		}

		public string GetL3ExpandedL1Name(uint index) {
			return this.L1IndexProvider.GetExpandedL3IndexFile(this.ChannelBand.ToString(), (index, 0));
		}

		public string GetL1ArchivedL1Name(uint index) {
			return this.L1IndexProvider.GetArchivedL1IndexFile(this.ChannelBand.ToString(), (index, 0));
		}

		public string GetL2ArchivedL1Name(uint index) {
			return this.L1IndexProvider.GetArchivedL2IndexFile(this.ChannelBand.ToString(), (index, 0));
		}

		public string GetL3ArchivedL1Name(uint index) {
			return this.L1IndexProvider.GetArchivedL3IndexFile(this.ChannelBand.ToString(), (index, 0));
		}

		protected override void CreateIndexProviders() {
			this.L1IndexProvider = new TrippleChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>, uint>(new GroupDigestChannelBandFileNamingProvider<uint>(), this.baseFolder, this.scopeFolder, this.ChannelBand.ToString(), this.fileSystem);
		}

		public override Dictionary<int, SafeArrayHandle> HashFiles(int groupIndex) {
			Dictionary<int, SafeArrayHandle> results = new Dictionary<int, SafeArrayHandle>();

			foreach(CHANEL_BANDS band in this.EnabledBands) {
				string archivedFilename = this.GetArchivedBandName(band, (uint) groupIndex);

				results.Add(band.ToInt32(null), this.HashFile(archivedFilename));
			}

			// now the index
			string indexArchivedL1Filename = this.GetL1ArchivedL1Name((uint) groupIndex);

			results.Add(INDEX_FILE1_ID, this.HashFile(indexArchivedL1Filename));

			string indexArchivedL2Filename = this.GetL2ArchivedL1Name((uint) groupIndex);

			results.Add(INDEX_FILE2_ID, this.HashFile(indexArchivedL2Filename));

			string indexArchivedL3Filename = this.GetL3ArchivedL1Name((uint) groupIndex);

			results.Add(INDEX_FILE3_ID, this.HashFile(indexArchivedL3Filename));

			return results;
		}

		public override SafeArrayHandle GetFileBytes(int fileId, uint partIndex, long offset, int length) {

			string archivedFilename = "";

			if(fileId == INDEX_FILE1_ID) {
				archivedFilename = this.GetL1ArchivedL1Name(partIndex);
			} else if(fileId == INDEX_FILE2_ID) {
				archivedFilename = this.GetL2ArchivedL1Name(partIndex);
			} else if(fileId == INDEX_FILE3_ID) {
				archivedFilename = this.GetL3ArchivedL1Name(partIndex);
			} else {

				Dictionary<int, CHANEL_BANDS> bands = this.EnabledBands.ToDictionary(b => b.ToInt32(null), b => b);

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

			if(fileId == INDEX_FILE1_ID) {
				archivedFilename = this.GetL1ArchivedL1Name(partIndex);
			} else if(fileId == INDEX_FILE2_ID) {
				archivedFilename = this.GetL2ArchivedL1Name(partIndex);
			} else if(fileId == INDEX_FILE3_ID) {
				archivedFilename = this.GetL3ArchivedL1Name(partIndex);
			} else {

				Dictionary<int, CHANEL_BANDS> bands = this.EnabledBands.ToDictionary(b => b.ToInt32(null), b => b);

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
			List<int> fileTypes = base.GetFileTypes();

			// and now the idnex file
			fileTypes.Add(INDEX_FILE1_ID);
			fileTypes.Add(INDEX_FILE2_ID);
			fileTypes.Add(INDEX_FILE3_ID);

			return fileTypes;
		}

		protected override List<string> EnsureIndexFilesetExtracted(uint index) {
			string archived1 = this.GetL1ArchivedL1Name(index);
			string expanded1 = this.GetL1ExpandedL1Name(index);
			this.EnsureFileExtracted(expanded1, archived1);

			string archived2 = this.GetL2ArchivedL1Name(index);
			string expanded2 = this.GetL2ExpandedL1Name(index);
			this.EnsureFileExtracted(expanded2, archived2);

			string archived3 = this.GetL3ArchivedL1Name(index);
			string expanded3 = this.GetL3ExpandedL1Name(index);
			this.EnsureFileExtracted(expanded3, archived3);

			return new[] {expanded1, expanded2, expanded3}.ToList();
		}

		protected virtual void ResetL1Provider(uint adjustedAccountId, uint groupIndex) {
			this.L1IndexProvider.ResetAllFileSpecs(this.GetExpandedBandName(this.ChannelBand, groupIndex), adjustedAccountId, (groupIndex, (groupIndex - 1) * this.groupSize));
		}

		protected SafeArrayHandle QueryL1Index(uint adjustedAccountId, uint groupIndex) {

			this.ResetL1Provider(adjustedAccountId, groupIndex);

			// query L1
			return this.L1IndexProvider.QueryCard(adjustedAccountId);

		}
	}

	public class SingleKeyTrippleFileChannelBandIndex<CHANEL_BANDS> : SingleKeyTrippleFileChannelBandIndex<CHANEL_BANDS, long>
		where CHANEL_BANDS : struct, Enum, IConvertible {

		public SingleKeyTrippleFileChannelBandIndex(string filename, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, FileSystemWrapper fileSystem) : base(filename, baseFolder, scopeFolder, groupSize, enabledBands, fileSystem) {
		}

		public override DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS> QueryCard(long keySet) {

			(uint adjustedAccountId, uint groupIndex) adjustedKey = this.AdjustAccountId(keySet);

			List<string> expanded = this.EnsureFilesetExtracted(adjustedKey.groupIndex);

			this.ResetL1Provider(adjustedKey.adjustedAccountId, adjustedKey.groupIndex);

			// query L1
			SafeArrayHandle data = this.L1IndexProvider.QueryCard(adjustedKey.adjustedAccountId);

			DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS> results = new DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS>(this.enabledBands);

			results[this.ChannelBand] = data;

			return results;
		}
	}
}