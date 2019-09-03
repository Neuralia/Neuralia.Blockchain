using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Linq.Expressions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders {

	public class SqliteChannelBandFileInterpretationProvider<CARD_TYPE, NAMING_PROVIDER, KEY, QUERY_KEY, KEY_SELECTOR> : DigestChannelBandFileInterpretationProvider<CARD_TYPE, NAMING_PROVIDER>
		where CARD_TYPE : class, IChannelBandSqliteProviderEntry<KEY>, new()
		where KEY : struct, IEquatable<KEY>
		where KEY_SELECTOR : Delegate
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider {
		protected readonly KEY_SELECTOR indexer;

		protected readonly Expression<Func<CARD_TYPE, object>> keyDeclaration;

		public SqliteChannelBandFileInterpretationProvider(NAMING_PROVIDER namingProvider, IFileSystem fileSystem) : this(namingProvider, fileSystem, null, null) {

		}

		public SqliteChannelBandFileInterpretationProvider(NAMING_PROVIDER namingProvider, IFileSystem fileSystem, Expression<Func<CARD_TYPE, object>> keyDeclaration, KEY_SELECTOR indexer) : base(namingProvider, fileSystem) {

			this.keyDeclaration = keyDeclaration;
			this.indexer = indexer;

		}

		protected ChannelBandSqliteProviderDal<CARD_TYPE, KEY> ChannelBandSqliteProviderDal => new ChannelBandSqliteProviderDal<CARD_TYPE, KEY>(this.ActiveFilename, this.ActiveFolder, this.keyDeclaration);

		protected string ExtractedFileName => "";

		public CARD_TYPE QueryCard(QUERY_KEY value) {

			//Func<CARD_TYPE, object[]>
			//			
			return this.ChannelBandSqliteProviderDal.PerformOperation(db => {

				return db.ChannelBandCards.Single(d => d.Id.Equals(value));
			});
		}

		public List<CARD_TYPE> QueryCards() {

			return this.ChannelBandSqliteProviderDal.PerformOperation(db => db.ChannelBandCards.ToList());
		}

		public CARD_TYPE QueryCard(QUERY_KEY key, Func<CARD_TYPE, bool> selector) {

			return this.ChannelBandSqliteProviderDal.PerformOperation(db => {

				if(this.keyDeclaration != null) {

					CARD_TYPE res = db.ChannelBandCards.SingleOrDefault(selector);

					return res;
				}

				return null;
			});
		}
	}
}