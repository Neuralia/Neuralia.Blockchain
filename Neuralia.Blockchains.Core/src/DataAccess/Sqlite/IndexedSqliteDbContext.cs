namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	public interface IIndexedSqliteDbContext : ISqliteDbContext {
		string GroupRoot { get; }
		void SetGroupFile(string filename);
		void SetGroupIndex(long index, long groupSize);
		
		string Filename { get; }

		long Index { get; }
		long GroupSize { get; }
		
		(long start, long end) IndexRange { get; }
	}

	public abstract class IndexedSqliteDbContext : SqliteDbContext, IIndexedSqliteDbContext {

		public string Filename { get; private set; }

		public long Index { get; private set; }
		public long GroupSize { get; private set; }
		
		public (long start, long end) IndexRange { get; private set; }


		//TODO: this will need a good refactor in the future. coding this fast

		protected override sealed string DbName => "{0}-{1}.db";

		public abstract string GroupRoot { get; }

		public void SetGroupFile(string filename) {
			this.Filename = filename;
		}

		public void SetGroupIndex(long index, long groupSize) {
			this.Filename = null;
			this.Index = index;
			this.GroupSize = groupSize;
			
			this.IndexRange = (groupSize * (index-1)+1, groupSize * index);

		}

		protected override string FormatFilename() {
			if(!string.IsNullOrWhiteSpace(this.Filename)) {
				return this.Filename;
			}

			return string.Format(this.DbName, this.GroupRoot, this.Index);
		}
	}
}