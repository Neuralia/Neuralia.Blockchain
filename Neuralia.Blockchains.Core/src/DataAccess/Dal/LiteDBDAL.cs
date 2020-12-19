using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.IO;

namespace Neuralia.Blockchains.Core.DataAccess.Dal {

	
	public class LiteDBDAL: WalletDBDAL {
		
		public static LiteDBDAL GetLiteDBDAL(string filepath) {
			return new LiteDBDAL(filepath);
		}

		public static LiteDBDAL GetLiteDBDAL(RecyclableMemoryStream filedata) {
			filedata.Position = 0;

			return new LiteDBDAL(filedata);
		}

		public LiteDBDAL(string filename) : base(filename) {
		}

		public LiteDBDAL(RecyclableMemoryStream filedata) : base(filedata) {
		}
		
		protected override IFileDatabase GetDatabase() {
			if(!string.IsNullOrWhiteSpace(this.filename)) {
				return new LiteDatabaseWrapper(this.filename);
			}

			if(this.filedata != null) {
				return new LiteDatabaseWrapper(this.filedata);
			}

			throw new ApplicationException("Invalid database creation options");
		}
	}
}