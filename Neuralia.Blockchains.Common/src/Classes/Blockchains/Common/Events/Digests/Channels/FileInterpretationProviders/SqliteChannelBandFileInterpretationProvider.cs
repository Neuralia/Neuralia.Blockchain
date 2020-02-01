using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Core.Configuration;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders {

	public class SqliteChannelBandFileInterpretationProvider<CARD_TYPE, NAMING_PROVIDER, KEY, QUERY_KEY, KEY_SELECTOR> : DigestChannelBandFileInterpretationProvider<CARD_TYPE, NAMING_PROVIDER>
		where CARD_TYPE : class, IChannelBandSqliteProviderEntry<KEY>
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
		
		public void InitModel(Action<ModelBuilder> builder) {
			this.ChannelBandSqliteProviderDal.ModelBuilder = builder;
		}


		protected ChannelBandSqliteProviderDal<CARD_TYPE, KEY> channelBandSqliteProviderDal = null;


		protected ChannelBandSqliteProviderDal<CARD_TYPE, KEY> ChannelBandSqliteProviderDal {
			get {
				if(this.channelBandSqliteProviderDal == null) {
					this.channelBandSqliteProviderDal = new ChannelBandSqliteProviderDal<CARD_TYPE, KEY>(this.ActiveFilename, this.ActiveFolder, GlobalSettings.SoftwareVersion, this.keyDeclaration);
				}

				return this.channelBandSqliteProviderDal;
			}
		}

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