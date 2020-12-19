using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {
	public abstract class GossipWorkflow<R> : Workflow<R>
		where R : IRehydrationFactory {
		protected GossipWorkflow(ServiceSet<R> serviceSet) : base(serviceSet) {
		}
	}
}