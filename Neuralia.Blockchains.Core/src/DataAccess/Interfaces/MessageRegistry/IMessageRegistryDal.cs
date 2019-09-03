using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry {
	public interface IMessageRegistryDal : IDalInterfaceBase {

		void CleanMessageCache();

		void AddMessageToCache(long xxhash, bool isvalid, bool local);

		void ForwardValidGossipMessage(long xxhash, List<string> activeConnectionIds, Func<List<string>, List<string>> forwardMessageCallback);

		(bool messageInCache, bool messageValid) CheckRecordMessageInCache<R>(long xxhash, MessagingManager<R>.MessageReceivedTask task, bool returnMessageToSender)
			where R : IRehydrationFactory;

		List<bool> CheckMessagesReceived(List<long> hashes, PeerConnection peerConnectionn);

		bool CheckMessageInCache(long messagexxHash, bool validated);

		bool CacheUnvalidatedBlockGossipMessage(long blockId, long xxHash);
		bool GetUnvalidatedBlockGossipMessageCached(long blockId);
		List<long> GetCachedUnvalidatedBlockGossipMessage(long blockId);
		List<(long blockId, long xxHash)> RemoveCachedUnvalidatedBlockGossipMessages(long blockId);
	}
}