using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq.Expressions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.Sqlite {
	public class SingleSqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY> : SqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE, SingleDigestChannelBandFileNamingProvider, KEY, INPUT_QUERY_KEY, QUERY_KEY>
		where CHANEL_BANDS : struct, Enum, IConvertible
		where CARD_TYPE : class, IChannelBandSqliteProviderEntry<KEY>, new()
		where KEY : struct, IEquatable<KEY> {

		protected readonly Func<INPUT_QUERY_KEY, QUERY_KEY> convertKeys;

		public SingleSqliteChannelBandIndex(string bandName, string baseFolder, string scopeFolder, CHANEL_BANDS enabledBands, IFileSystem fileSystem, Func<INPUT_QUERY_KEY, QUERY_KEY> convertKeys, Expression<Func<CARD_TYPE, object>> keyDeclaration = null) : base(bandName, baseFolder, scopeFolder, enabledBands, fileSystem, keyDeclaration) {
			this.convertKeys = convertKeys;
		}

		protected override SingleDigestChannelBandFileNamingProvider CreateNamingProvider() {
			return new SingleDigestChannelBandFileNamingProvider();
		}

		public override Dictionary<int, IByteArray> HashFiles(int groupIndex) {
			var results = new Dictionary<int, IByteArray>();

			string archivedFilename = this.GenerateFullPath(this.Providers[this.BandType].NamingProvider.GeneratedArchivedFileName(this.bandName, this.scopeFolder, new object[] { }));

			results.Add(this.BandType.ToInt32(null), this.HashFile(archivedFilename));

			return results;
		}

		public override List<int> GetFileTypes() {
			var fileTypes = new List<int>();

			fileTypes.Add(this.BandType.ToInt32(null));

			return fileTypes;
		}

		public override IByteArray GetFileBytes(int fileId, uint partIndex, long offset, int length) {

			string archivedFilename = this.GenerateFullPath(this.Providers[this.BandType].NamingProvider.GeneratedArchivedFileName(this.bandName, this.scopeFolder, new object[] { }));

			return FileExtensions.ReadBytes(archivedFilename, offset, length, this.fileSystem);
		}

		public override DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS> QueryCard(INPUT_QUERY_KEY key) {

			string extractedFilename = this.EnsureFilesetExtracted();

			this.InterpretationProvider.SetActiveFilename(Path.GetFileName(extractedFilename), Path.GetDirectoryName(extractedFilename));

			var results = new DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS>(this.BandType);
			results[this.BandType] = this.InterpretationProvider.QueryCard(this.convertKeys(key));

			return results;

		}

		public List<CARD_TYPE> QueryCards() {

			string extractedFilename = this.EnsureFilesetExtracted();

			this.InterpretationProvider.SetActiveFilename(Path.GetFileName(extractedFilename), Path.GetDirectoryName(extractedFilename));

			return this.InterpretationProvider.QueryCards();

		}
	}
}