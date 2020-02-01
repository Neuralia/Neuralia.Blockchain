using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq.Expressions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.Sqlite {

	public class GroupSplitSqliteMultiKeyChannelBandIndex<CHANEL_BANDS, CARD_TYPE, B> : GroupSplitSqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE, ulong, (long accountId, B ordinal), (uint accountId, B ordinal)>
		where CHANEL_BANDS : struct, Enum, IConvertible
		where CARD_TYPE : class, IChannelBandSqliteProviderEntry<ulong>, new() {

		protected readonly Func<(uint id, B ordinal), Tuple<uint, B>> indexer;

		public GroupSplitSqliteMultiKeyChannelBandIndex(string bandName, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, IFileSystem fileSystem) : this(bandName, baseFolder, scopeFolder, groupSize, enabledBands, fileSystem, null, null) {

		}

		public GroupSplitSqliteMultiKeyChannelBandIndex(string bandName, string baseFolder, string scopeFolder, int groupSize, CHANEL_BANDS enabledBands, IFileSystem fileSystem, Expression<Func<CARD_TYPE, object>> keyDeclaration, Func<(uint id, B ordinal), Tuple<uint, B>> indexer) : base(bandName, baseFolder, scopeFolder, groupSize, enabledBands, fileSystem, keyDeclaration) {
			this.indexer = indexer;
		}

		public override void Initialize() {
			base.Initialize();
		}

		protected override void CreateProviders() {
			this.Providers.Add(this.BandType, new SqliteChannelBandFileInterpretationProvider<CARD_TYPE, GroupDigestChannelBandFileNamingProvider<uint>, ulong, (long accountSequenceId, Enums.AccountTypes accountType, B ordinal), Func<(uint id, B ordinal), Tuple<uint, B>>>(this.CreateNamingProvider(), this.fileSystem, this.keyDeclaration, this.indexer));
		}

		public override DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS> QueryCard((long accountId, B ordinal) key) {

			(uint adjustedAccountId, uint groupIndex) adjustedKey = this.AdjustAccountId(key.accountId);

			string extractedFilename = this.EnsureFilesetExtracted(adjustedKey.groupIndex);

			this.InterpretationProvider.SetActiveFilename(Path.GetFileName(extractedFilename), Path.GetDirectoryName(extractedFilename));

			var results = new DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS>(this.BandType);
			results[this.BandType] = this.InterpretationProvider.QueryCard((adjustedKey.adjustedAccountId, key.ordinal));

			return results;
		}
	}
}