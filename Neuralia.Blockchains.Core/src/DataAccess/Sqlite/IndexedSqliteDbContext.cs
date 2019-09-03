namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	public interface IIndexedSqliteDbContext : ISqliteDbContext {
		string GroupRoot { get; }
		void SetGroupFile(string filename);
		void SetGroupIndex(int index);
	}

	public abstract class IndexedSqliteDbContext : SqliteDbContext, IIndexedSqliteDbContext {

		private string filename;

		private int index;

		//TODO: this will need a good refactor in the future. coding this fast

		protected override sealed string DbName => "{0}-{1}.db";

		public abstract string GroupRoot { get; }

		public void SetGroupFile(string filename) {
			this.filename = filename;
		}

		public void SetGroupIndex(int index) {
			this.filename = null;
			this.index = index;
		}

		protected override string FormatFilename() {
			if(!string.IsNullOrWhiteSpace(this.filename)) {
				return this.filename;
			}

			return string.Format(this.DbName, this.GroupRoot, this.index);
		}
	}
}