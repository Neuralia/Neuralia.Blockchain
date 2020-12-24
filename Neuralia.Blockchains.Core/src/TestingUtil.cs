using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Extensions.DbSet;

namespace Neuralia.Blockchains.Core {
	public static class TestingUtil {
		/// <summary>
		/// Set to true if in unit test mode
		/// </summary>
		public static bool Testing = false;

		public static void ClearContext(DbContext db) {
			if(Testing) {
				db.ChangeTracker.Clear();
			}
		}
	}
}