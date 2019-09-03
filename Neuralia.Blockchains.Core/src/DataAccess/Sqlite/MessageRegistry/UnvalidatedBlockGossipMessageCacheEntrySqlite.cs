using System;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry {

	public class UnvalidatedBlockGossipMessageCacheEntrySqlite : IUnvalidatedBlockGossipMessageCacheEntry {

		public int Id { get; set; }
		public long BlockId { get; set; }
		public long Hash { get; set; }
		public DateTime Received { get; set; }
	}
}