using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.P2p.Messages.Base {
	public interface IWorkflowFactory {
	}

	public interface IWorkflowFactory<R> : IWorkflowFactory
		where R : IRehydrationFactory {
	}

	public abstract class WorkflowFactory<R> : IWorkflowFactory<R>
		where R : IRehydrationFactory {

		protected readonly INetworkingService<R> networkingService;

		protected readonly ServiceSet<R> serviceSet;

		public WorkflowFactory(ServiceSet<R> serviceSet) {
			this.networkingService = (INetworkingService<R>) DIService.Instance.GetService<INetworkingService>();

			this.serviceSet = serviceSet;
		}

		protected IMainMessageFactory<R> MessageFactory => this.networkingService.MessageFactory;
	}
}