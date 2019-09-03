using System;
using System.Linq.Expressions;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite {

	public interface IChannelBandSqliteProviderDal<ENTRY, KEY> : ISqliteDal<IChannelBandSqliteProviderContext<ENTRY, KEY>>
		where ENTRY : class, IChannelBandSqliteProviderEntry<KEY>
		where KEY : struct {

		void PerformOperation(Action<IChannelBandSqliteProviderContext<ENTRY, KEY>> process);
		T PerformOperation<T>(Func<IChannelBandSqliteProviderContext<ENTRY, KEY>, T> process);
	}

	public class ChannelBandSqliteProviderDal<ENTRY, KEY> : SqliteDal<ChannelBandSqliteProviderContext<ENTRY, KEY>>, IChannelBandSqliteProviderDal<ENTRY, KEY>
		where ENTRY : class, IChannelBandSqliteProviderEntry<KEY>
		where KEY : struct {

		public ChannelBandSqliteProviderDal(string filename, string folderPath, Expression<Func<ENTRY, object>> keyDeclaration = null) : base(folderPath, null, st => new ChannelBandSqliteProviderContext<ENTRY, KEY>(filename, keyDeclaration), AppSettingsBase.SerializationTypes.Master) {

		}

		public void PerformOperation(Action<IChannelBandSqliteProviderContext<ENTRY, KEY>> process) {

			base.PerformOperation(process);
		}

		public T PerformOperation<T>(Func<IChannelBandSqliteProviderContext<ENTRY, KEY>, T> process) {

			return base.PerformOperation(process);
		}
	}
}