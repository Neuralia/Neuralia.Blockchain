using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	
	public interface ISplitSqliteDbContext : ISqliteDbContext {
		void SetIndex(long index);
	}

	public abstract class SplitSqliteDbContext : SqliteDbContext, ISplitSqliteDbContext {
		
		
		public const int GROUP_SIZE = 500_000;

		
		protected override sealed string DbName => "{0}-{1}.db";
		public long Index { get; private set; }
		
		public abstract string GroupRoot { get; }
		

		public void SetIndex(long index) {

			this.Index = index;
		}
		
		protected override string FormatFilename() {

			return string.Format(this.DbName, this.GroupRoot, this.Index);
		}
	}
}