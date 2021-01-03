using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Extensions.DbSet;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core {
	public static class TestingUtil {
		/// <summary>
		/// Set to true if in unit test mode
		/// </summary>
		public static bool Testing = false;
		public static RecursiveAsyncLock dbLocker = new RecursiveAsyncLock();

	
		public static void ClearContext(DbContext db) {
			if(Testing) {
				db.ChangeTracker.Clear();
			}
		}
	}
}