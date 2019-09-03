using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {
	public interface IClientWorkflow<R> : INetworkWorkflow<R>
		where R : IRehydrationFactory {
	}

	public interface IClientWorkflow<MESSAGE_FACTORY, R> : IClientWorkflow<R>
		where MESSAGE_FACTORY : IMessageFactory<R>
		where R : IRehydrationFactory {
	}

	public abstract class ClientWorkflow<MESSAGE_FACTORY, R> : NetworkWorkflow<MESSAGE_FACTORY, R>, IClientWorkflow<MESSAGE_FACTORY, R>
		where MESSAGE_FACTORY : IMessageFactory<R>
		where R : IRehydrationFactory {

		protected ClientWorkflow(ServiceSet<R> serviceSet) : base(serviceSet) {

			this.CorrelationId = GlobalRandom.GetNextUInt();
		}

		protected PeerConnection GetNewConnection(NetworkEndPoint endpoint) {
			return this.networkingService.ConnectionStore.GetNewConnection(endpoint);
		}
	}
}