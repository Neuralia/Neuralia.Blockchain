using System;
using System.Linq.Expressions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.Sqlite {

	public abstract class SqliteChannelBandIndex<CHANEL_BANDS, CARD_TYPE, NAMING_PROVIDER, KEY, INPUT_QUERY_KEY, QUERY_KEY> : DigestChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY, NAMING_PROVIDER>
		where CHANEL_BANDS : struct, Enum, IConvertible
		where CARD_TYPE : class, IChannelBandSqliteProviderEntry<KEY>, new()
		where KEY : struct, IEquatable<KEY>
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider {

		protected readonly string bandName;

		protected readonly Expression<Func<CARD_TYPE, object>> keyDeclaration;

		public SqliteChannelBandIndex(string bandName, string baseFolder, string scopeFolder, CHANEL_BANDS enabledBands, FileSystemWrapper fileSystem, Expression<Func<CARD_TYPE, object>> keyDeclaration = null) : base("", baseFolder, scopeFolder, enabledBands, fileSystem) {
			this.BandType = enabledBands;
			this.keyDeclaration = keyDeclaration;
			this.bandName = bandName.ToLower();
		}

		public CHANEL_BANDS BandType { get; }

		public SqliteChannelBandFileInterpretationProvider<CARD_TYPE, NAMING_PROVIDER, KEY, QUERY_KEY, Func<CARD_TYPE, bool>> InterpretationProvider {
			get => (SqliteChannelBandFileInterpretationProvider<CARD_TYPE, NAMING_PROVIDER, KEY, QUERY_KEY, Func<CARD_TYPE, bool>>) this.Providers[this.BandType];
			set => this.Providers[this.BandType] = value;
		}

		public override void Initialize() {
			base.Initialize();

			this.CreateProviders();
		}

		protected virtual string EnsureFilesetExtracted() {
			return this.EnsureFilesetExtracted(this.bandName, new object[0], this.BandType).extractedName;
		}

		protected virtual void CreateProviders() {
			this.Providers.Add(this.BandType, new SqliteChannelBandFileInterpretationProvider<CARD_TYPE, NAMING_PROVIDER, KEY, QUERY_KEY, Func<CARD_TYPE, bool>>(this.CreateNamingProvider(), this.fileSystem));
		}

		protected abstract NAMING_PROVIDER CreateNamingProvider();

		public override DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS> QueryCard(INPUT_QUERY_KEY key) {

			//			var adjustedKey = this.AdjustAccountId(key.accountId);
			//
			//			this.EnsureFilesetExtracted(adjustedKey.index);
			//
			//			return this.InterpretationProvider.QueryCard((adjustedKey.adjustedAccountId, key.ordinal));

			return null;
		}
	}
}