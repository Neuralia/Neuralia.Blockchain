using System;

namespace Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry {
	public interface IUnvalidatedBlockGossipMessageCacheEntry {
		int Id { get; set; }
		long BlockId { get; set; }
		long Hash { get; set; }
		DateTime Received { get; set; }
	}
}