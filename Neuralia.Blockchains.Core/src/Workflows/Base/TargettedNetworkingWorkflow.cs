using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.Workflows.Base {
	public interface ITargettedNetworkingWorkflow<R> : INetworkingWorkflow<ITargettedMessageSet<R>, TargettedHeader, R>
		where R : IRehydrationFactory {
	}

	public abstract class TargettedNetworkingWorkflow<R> : NetworkingWorkflow<ITargettedMessageSet<R>, TargettedHeader, R>, INetworkingWorkflow<R>
		where R : IRehydrationFactory {

		protected TargettedNetworkingWorkflow(ServiceSet<R> serviceSet) : base(serviceSet) {
		}
	}
}