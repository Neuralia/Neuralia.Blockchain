using System.Collections.Generic;
using LiteDB;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.DataAccess.Dal {
	public interface IFileDatabase : IDisposableExtended {

		IEnumerable<string> GetCollectionNames();
		bool CollectionExists(string tableName);
		ILiteCollection<T> GetCollection<T>(string tablename);
		ILiteCollection<T> GetCollection<T>();
	}
}