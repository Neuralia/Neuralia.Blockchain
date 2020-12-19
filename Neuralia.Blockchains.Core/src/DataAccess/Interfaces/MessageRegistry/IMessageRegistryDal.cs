using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry {
	public interface IMessageRegistryDal : IDalInterfaceBase {

		Task CleanMessageCache();

		Task AddMessageToCache(long xxhash, bool isvalid, bool local);

		Task ForwardValidGossipMessage(long xxhash, List<string> activeConnectionIds, Func<List<string>, List<string>> forwardMessageCallback);

		Task<(bool messageInCache, bool messageValid)> CheckRecordMessageInCache<R>(long xxhash, MessagingManager<R>.MessageReceivedTask task, bool returnMessageToSender)
			where R : IRehydrationFactory;

		Task<List<bool>> CheckMessagesReceived(List<long> hashes, PeerConnection peerConnectionn);

		Task<bool> CheckMessageInCache(long messagexxHash, bool validated);

		Task<bool> CacheUnvalidatedBlockGossipMessage(long blockId, long xxHash);
		Task<bool> GetUnvalidatedBlockGossipMessageCached(long blockId);
		Task<List<long>> GetCachedUnvalidatedBlockGossipMessage(long blockId);
		Task<List<(long blockId, long xxHash)>> RemoveCachedUnvalidatedBlockGossipMessages(long blockId);
	}
}