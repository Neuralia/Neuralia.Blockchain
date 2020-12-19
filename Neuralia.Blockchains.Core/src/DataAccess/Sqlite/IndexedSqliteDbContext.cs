using System.IO;
using Neuralia.Blockchains.Core.Extensions;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite {

	public interface IIndexedSqliteDbContext : ISqliteDbContext {
		string GroupRoot { get; }

		string Filename { get; }

		IndexedSqliteDbContext.IndexSet Index { get; }
		long GroupSize { get; }

		(long start, long end) IndexRange { get; }
		void SetGroupFile(string filename);
		void SetGroupIndex(IndexedSqliteDbContext.IndexSet index, long groupSize);
	}

	public abstract class IndexedSqliteDbContext : SqliteDbContext, IIndexedSqliteDbContext {

		public struct IndexSet {
			public long index;
			public string divider;

			public IndexSet(long index, string divider) {
				this.index = index;
				this.divider = divider;
			}

			public IndexSet(long index) {
				this.index = index;
				this.divider = "";
			}
		}

		//TODO: this will need a good refactor in the future. coding this fast

		protected override sealed string DbName => "{0}-{1}.db";

		public string Filename { get; private set; }

		public IndexSet Index { get; private set; }
		public long GroupSize { get; private set; }

		public (long start, long end) IndexRange { get; private set; }

		public abstract string GroupRoot { get; }

		public void SetGroupFile(string filename) {
			this.Filename = filename;
		}


		public void SetGroupIndex(IndexSet index, long groupSize) {
			this.Filename = null;
			this.Index = index;
			this.GroupSize = groupSize;

			this.IndexRange = ((groupSize * (index.index - 1)) + 1, groupSize * index.index);

		}

		public override void EnsureCreated() {

			FileExtensions.EnsureDirectoryStructure(this.RefinedFolderPath);

			base.EnsureCreated();
		}

		private string RefinedFolderPath{
			get {
				string refinement = this.PathRefinement;
				if(!string.IsNullOrWhiteSpace(refinement)) {
					return Path.Combine(this.FolderPath, refinement);
				}

				return this.FolderPath;
			}
		}
		
		private string PathRefinement {
			get{
				if(!string.IsNullOrWhiteSpace(this.Index.divider)) {
					return this.Index.divider;
				}

				return "";
			}
		}

		protected override string FormatFilename() {
			if(!string.IsNullOrWhiteSpace(this.Filename)) {
				return this.Filename;
			}

			string group = this.GroupRoot;
			string path = "";
			string refinement = this.PathRefinement;
			if(!string.IsNullOrWhiteSpace(refinement)) {
				path = Path.Combine(refinement, string.Format(this.DbName, $"{group}-{this.Index.divider}", this.Index.index));
			} else {
				path = string.Format(this.DbName, group, this.Index.index);
			}

			return path;
		}
	}
}