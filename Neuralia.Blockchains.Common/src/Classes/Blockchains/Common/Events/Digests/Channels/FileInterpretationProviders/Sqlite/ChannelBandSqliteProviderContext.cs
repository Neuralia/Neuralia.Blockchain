using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite {
	public interface IChannelBandSqliteProviderContext<MODEL_SNAPSHOT, KEY> : ISqliteDbContext
		where MODEL_SNAPSHOT : class, IChannelBandSqliteProviderEntry<KEY>
		where KEY : struct {

		DbSet<MODEL_SNAPSHOT> ChannelBandCards { get; set; }
	}

	public class ChannelBandSqliteProviderContext<MODEL_SNAPSHOT, KEY> : SqliteDbContext, IChannelBandSqliteProviderContext<MODEL_SNAPSHOT, KEY>
		where MODEL_SNAPSHOT : class, IChannelBandSqliteProviderEntry<KEY>
		where KEY : struct {

		protected readonly Expression<Func<MODEL_SNAPSHOT, object>> keyDeclaration;

		public ChannelBandSqliteProviderContext(string filename, AppSettingsBase.SerializationTypes serializationType) : this(filename, null) {

		}

		public ChannelBandSqliteProviderContext(string filename, Expression<Func<MODEL_SNAPSHOT, object>> keyDeclaration) {
			this.DbName = filename;
			this.keyDeclaration = keyDeclaration;
		}

		protected override string DbName { get; }

		public DbSet<MODEL_SNAPSHOT> ChannelBandCards { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<MODEL_SNAPSHOT>(eb => {

				if(this.keyDeclaration == null) {
					eb.HasKey(c => c.Id);
					eb.Property(b => b.Id).ValueGeneratedNever();
				} else {
					eb.HasKey(this.keyDeclaration);
				}

				eb.ToTable("ChannelBand");
			});

			base.OnModelCreating(modelBuilder);
		}
	}
}