using System;
using System.Collections.Generic;
using System.IO;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.Sqlite {
	public class SingleSqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY> : SqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE, SingleDigestChannelBandFileNamingProvider, KEY, INPUT_QUERY_KEY, QUERY_KEY>
		where CHANEL_BANDS : struct, Enum, IConvertible
		where CARD_TYPE : class, IChannelBandSqliteProviderEntry<KEY>, new()
		where KEY : struct, IEquatable<KEY> {

		protected readonly Func<INPUT_QUERY_KEY, QUERY_KEY> convertKeys;
		public Action<ModelBuilder> ModelBuilder { get; set; }

		public SingleSqliteChannelBandIndex(string bandName, string baseFolder, string scopeFolder, CHANEL_BANDS enabledBands, FileSystemWrapper fileSystem, Func<INPUT_QUERY_KEY, QUERY_KEY> convertKeys, Expression<Func<CARD_TYPE, object>> keyDeclaration = null) : base(bandName, baseFolder, scopeFolder, enabledBands, fileSystem, keyDeclaration) {
			this.convertKeys = convertKeys;
		}

		protected override SingleDigestChannelBandFileNamingProvider CreateNamingProvider() {
			return new SingleDigestChannelBandFileNamingProvider();
		}
		
		protected void PrepareDb() {
			string expandedFilename = this.GenerateFullPath(this.Providers[this.BandType].NamingProvider.GeneratedExpandedFileName(this.bandName,this.bandName, this.scopeFolder, new object[] { }));

			this.InterpretationProvider.SetActiveFilename(Path.GetFileName(expandedFilename), Path.GetDirectoryName(expandedFilename));

			if(this.ModelBuilder != null) {
				this.InterpretationProvider.InitModel(this.ModelBuilder);
			}
		}
		
		public override Dictionary<int, SafeArrayHandle> HashFiles(int groupIndex) {
			var results = new Dictionary<int, SafeArrayHandle>();

			string archivedFilename = this.GenerateFullPath(this.Providers[this.BandType].NamingProvider.GeneratedArchivedFileName(this.bandName,this.bandName, this.scopeFolder, new object[] { }));

			results.Add(this.BandType.ToInt32(null), this.HashFile(archivedFilename));

			return results;
		}

		public override List<int> GetFileTypes() {
			var fileTypes = new List<int>();

			fileTypes.Add(this.BandType.ToInt32(null));

			return fileTypes;
		}

		public override SafeArrayHandle GetFileBytes(int fileId, uint partIndex, long offset, int length) {

			string archivedFilename = this.GenerateFullPath(this.Providers[this.BandType].NamingProvider.GeneratedArchivedFileName(this.bandName,this.bandName, this.scopeFolder, new object[] { }));

			return FileExtensions.ReadBytes(archivedFilename, offset, length, this.fileSystem);
		}

		public override void WriteFileBytes(int fileId, uint partIndex, SafeArrayHandle data) {

			string archivedFilename = this.GenerateFullPath(this.Providers[this.BandType].NamingProvider.GeneratedArchivedFileName(this.bandName,this.bandName, this.scopeFolder, new object[] { }));

			FileExtensions.EnsureFileExists(archivedFilename, this.fileSystem);
			FileExtensions.WriteAllBytes(archivedFilename, data, this.fileSystem);
		}
		
		public override DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS> QueryCard(INPUT_QUERY_KEY key) {

			this.PrepareDb();
			
			string extractedFilename = this.EnsureFilesetExtracted();

			this.InterpretationProvider.SetActiveFilename(Path.GetFileName(extractedFilename), Path.GetDirectoryName(extractedFilename));

			var results = new DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS>(this.BandType);
			results[this.BandType] = this.InterpretationProvider.QueryCard(this.convertKeys(key));

			return results;

		}

		public List<CARD_TYPE> QueryCards() {
			
			this.PrepareDb();
			
			string extractedFilename = this.EnsureFilesetExtracted();

			this.InterpretationProvider.SetActiveFilename(Path.GetFileName(extractedFilename), Path.GetDirectoryName(extractedFilename));

			return this.InterpretationProvider.QueryCards();

		}
	}
}