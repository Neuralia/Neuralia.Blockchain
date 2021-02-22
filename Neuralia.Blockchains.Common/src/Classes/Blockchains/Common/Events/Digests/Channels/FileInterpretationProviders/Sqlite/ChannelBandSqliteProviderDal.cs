using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite {

	public interface IChannelBandSqliteProviderDal<ENTRY, KEY> : ISqliteDal<IChannelBandSqliteProviderContext<ENTRY, KEY>>
		where ENTRY : class, IChannelBandSqliteProviderEntry<KEY>
		where KEY : struct {
		Action<ModelBuilder> ModelBuilder { get; set; }

		void PerformOperation(Action<IChannelBandSqliteProviderContext<ENTRY, KEY>, LockContext> process, LockContext lockContext = null);
		T PerformOperation<T>(Func<IChannelBandSqliteProviderContext<ENTRY, KEY>, LockContext, T> process, LockContext lockContext = null);
	}

	public class ChannelBandSqliteProviderDal<ENTRY, KEY> : SqliteDal<ChannelBandSqliteProviderContext<ENTRY, KEY>>, IChannelBandSqliteProviderDal<ENTRY, KEY>
		where ENTRY : class, IChannelBandSqliteProviderEntry<KEY>
		where KEY : struct {

		public ChannelBandSqliteProviderDal(string filename, string folderPath, SoftwareVersion softwareVersion, Expression<Func<ENTRY, object>> keyDeclaration = null) : base(folderPath, null, softwareVersion, st => new ChannelBandSqliteProviderContext<ENTRY, KEY>(filename, keyDeclaration), AppSettingsBase.SerializationTypes.Main) {

		}

		public Action<ModelBuilder> ModelBuilder { get; set; }

		public void PerformOperation(Action<IChannelBandSqliteProviderContext<ENTRY, KEY>, LockContext> process, LockContext lockContext = null) {

			base.PerformOperation(process, lockContext);
		}

		public T PerformOperation<T>(Func<IChannelBandSqliteProviderContext<ENTRY, KEY>, LockContext, T> process, LockContext lockContext = null) {

			return base.PerformOperation(process, lockContext);
		}

		protected override void PerformCustomMappings(ChannelBandSqliteProviderContext<ENTRY, KEY> db, LockContext lockContext) {
			base.PerformCustomMappings(db, lockContext);

			db.ModelBuilders.Add(modelBuilder => {
				if(this.ModelBuilder != null) {
					this.ModelBuilder(modelBuilder);
				}
			});
		}
	}
}