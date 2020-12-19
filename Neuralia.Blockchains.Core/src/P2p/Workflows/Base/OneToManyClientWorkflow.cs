using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {
	public abstract class OneToManyClientWorkflow<MESSAGE_FACTORY, R> : ClientWorkflow<MESSAGE_FACTORY, R>
		where MESSAGE_FACTORY : IMessageFactory<R>
		where R : IRehydrationFactory {

		protected OneToManyClientWorkflow(ServiceSet<R> serviceSet) : base(serviceSet) {
		}

		/// <summary>
		///     the client does not use the scope, response to all scopes
		/// </summary>
		public override WorkflowId Id => new NetworkWorkflowId(this.ClientId, this.CorrelationId, null);
	}
}