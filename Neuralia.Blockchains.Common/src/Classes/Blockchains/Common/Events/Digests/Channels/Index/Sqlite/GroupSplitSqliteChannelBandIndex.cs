using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq.Expressions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.Sqlite {

	/// <summary>
	///     base class for all split sqlite operations
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="CHANEL_BANDS"></typeparam>
	/// <typeparam name="CARD_TYPE"></typeparam>
	/// <typeparam name="KEY"></typeparam>
	/// <typeparam name="QUERY_KEY"></typeparam>
	public abstract class GroupSplitSqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY> : SqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE, GroupDigestChannelBandFileNamingProvider<uint>, KEY, INPUT_QUERY_KEY, QUERY_KEY>
		where CHANEL_BANDS : struct, Enum, IConvertible
		where CARD_TYPE : class, IChannelBandSqliteProviderEntry<KEY>, new()
		where KEY : struct, IEquatable<KEY> {

		protected readonly int groupSize;

		public GroupSplitSqliteChannelBandIndex(string bandName, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, IFileSystem fileSystem) : base(bandName, baseFolder, scopeFolder, enabledBands, fileSystem) {
			this.groupSize = groupSize;
		}

		public GroupSplitSqliteChannelBandIndex(string bandName, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, IFileSystem fileSystem, Expression<Func<CARD_TYPE, object>> keyDeclaration = null) : base(bandName, baseFolder, scopeFolder, enabledBands, fileSystem, keyDeclaration) {
			this.groupSize = groupSize;
		}

		protected (uint adjustedAccountId, uint groupIndex) AdjustAccountId(long accountId) {
			// make it 0 based
			accountId -= 1;

			uint index = (uint) (accountId / this.groupSize);

			uint adjustedAccountId = (uint) (accountId - (index * this.groupSize));

			// index is 1 based
			return (adjustedAccountId, index + 1);
		}

		protected override GroupDigestChannelBandFileNamingProvider<uint> CreateNamingProvider() {
			return new GroupDigestChannelBandFileNamingProvider<uint>();
		}

		protected override string EnsureFilesetExtracted() {
			throw new NotImplementedException();
		}

		protected virtual string EnsureFilesetExtracted(uint index) {
			return this.EnsureFilesetExtracted(this.bandName, new object[] {index}, this.BandType).extractedName;
		}

		public override Dictionary<int, SafeArrayHandle> HashFiles(int groupIndex) {
			var results = new Dictionary<int, SafeArrayHandle>();

			string archivedFilename = this.GenerateFullPath(this.Providers[this.BandType].NamingProvider.GeneratedArchivedFileName(this.bandName,this.bandName, this.scopeFolder, new object[] {groupIndex}));

			results.Add(this.BandType.ToInt32(null), this.HashFile(archivedFilename));

			return results;
		}

		public override SafeArrayHandle GetFileBytes(int fileId, uint partIndex, long offset, int length) {

			string archivedFilename = this.GenerateFullPath(this.Providers[this.BandType].NamingProvider.GeneratedArchivedFileName(this.bandName,this.bandName, this.scopeFolder, new object[] {partIndex}));

			return FileExtensions.ReadBytes(archivedFilename, offset, length, this.fileSystem);
		}
		
		public override void WriteFileBytes(int fileId, uint partIndex, SafeArrayHandle data) {

			string archivedFilename = this.GenerateFullPath(this.Providers[this.BandType].NamingProvider.GeneratedArchivedFileName(this.bandName,this.bandName, this.scopeFolder, new object[] {partIndex}));

			FileExtensions.EnsureFileExists(archivedFilename, this.fileSystem);
			FileExtensions.WriteAllBytes(archivedFilename, data, this.fileSystem);
		}

		public override List<int> GetFileTypes() {
			var fileTypes = new List<int>();

			fileTypes.Add(this.BandType.ToInt32(null));

			return fileTypes;
		}
	}

	public class GroupSplitSqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE> : GroupSplitSqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE, long, long, uint>
		where CHANEL_BANDS : struct, Enum, IConvertible
		where CARD_TYPE : class, IChannelBandSqliteProviderEntry<long>, new() {

		public GroupSplitSqliteChannelBandIndex(string filename, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, IFileSystem fileSystem) : base(filename, baseFolder, scopeFolder, groupSize, enabledBands, fileSystem) {
		}

		public override DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS> QueryCard(long key) {

			(uint adjustedAccountId, uint groupIndex) adjustedKey = this.AdjustAccountId(key);

			string extractedFilename = this.EnsureFilesetExtracted(adjustedKey.groupIndex);

			this.InterpretationProvider.SetActiveFilename(Path.GetFileName(extractedFilename), Path.GetDirectoryName(extractedFilename));

			var results = new DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS>(this.BandType);
			results[this.BandType] = this.InterpretationProvider.QueryCard(adjustedKey.adjustedAccountId);

			return results;

		}

		protected override void CreateProviders() {
			this.Providers.Add(this.BandType, new SqliteChannelBandFileInterpretationProvider<CARD_TYPE, GroupDigestChannelBandFileNamingProvider<uint>, long, uint, Func<CARD_TYPE, bool>>(this.CreateNamingProvider(), this.fileSystem));
		}
	}
}