using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.P2p.Messages {

	/// <summary>
	///     an entity that will route network messages to their destination
	/// </summary>
	public interface INetworkRouter {
		void RouteNetworkMessage(IRoutingHeader header, SafeArrayHandle data, PeerConnection connection);

		void RouteNetworkGossipMessage(IGossipMessageSet gossipMessageSet, PeerConnection connection);
	}
}