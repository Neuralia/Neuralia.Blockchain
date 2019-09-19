using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.P2p.Messages.Base {
	public interface IGossipMessageFactory : IMessageFactory {
		void CopyGossipHeaderHeaderInfo(GossipHeader newHeader, GossipHeader triggerHeader);
	}

	public interface IGossipMessageFactory<R> : IGossipMessageFactory, IMessageFactory<R>
		where R : IRehydrationFactory {
		IGossipMessageSet RehydrateGossipMessage(SafeArrayHandle data, GossipHeader header, R rehydrationFactory);
	}

	public abstract class GossipMessageFactory<R> : MessageFactory<R>, IGossipMessageFactory<R>
		where R : IRehydrationFactory {

		public GossipMessageFactory(ServiceSet<R> serviceSet) : base(serviceSet) {

		}

		public abstract IGossipMessageSet RehydrateGossipMessage(SafeArrayHandle data, GossipHeader header, R rehydrationFactory);

		public void CopyGossipHeaderHeaderInfo(GossipHeader newHeader, GossipHeader triggerHeader) {
			if(triggerHeader != null) {
				newHeader.NetworkOptions.Value = triggerHeader.NetworkOptions.Value;
				newHeader.chainId = triggerHeader.chainId;
				newHeader.ClientId = triggerHeader.ClientId;
				newHeader.SentTime = triggerHeader.SentTime;
				newHeader.Hash = triggerHeader.Hash;
				triggerHeader.Type = triggerHeader.Type;
				triggerHeader.Major = triggerHeader.Major;
				triggerHeader.Minor = triggerHeader.Minor;
			}
		}
	}
}