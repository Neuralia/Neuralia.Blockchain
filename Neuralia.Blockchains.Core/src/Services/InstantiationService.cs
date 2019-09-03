using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.P2p.Workflows;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.Services {

	public interface IInstantiationService {
	}

	public interface IInstantiationService<R> : IInstantiationService
		where R : IRehydrationFactory {
		IClientWorkflowFactory<R> GetClientWorkflowFactory(ServiceSet<R> serviceSet);
		IServerWorkflowFactory<R> GetServerWorkflowFactory(ServiceSet<R> serviceSet);
		IInstantiationFactory<R> GetInstantiationFactory(ServiceSet<R> serviceSet);
		IGossipWorkflowFactory<R> GetGossipWorkflowFactory(ServiceSet<R> serviceSet);
	}

	public class InstantiationService<R> : IInstantiationService<R>
		where R : IRehydrationFactory {
		protected IClientWorkflowFactory<R> clientWorkflowFactory;
		protected IGossipWorkflowFactory<R> gossipWorkflowFactory;

		protected IInstantiationFactory<R> instantiationFactory;
		protected IServerWorkflowFactory<R> serverWorkflowFactory;

		public IInstantiationFactory<R> GetInstantiationFactory(ServiceSet<R> serviceSet) {
			if(this.instantiationFactory == null) {
				this.CreateFactories(serviceSet);
			}

			return this.instantiationFactory;
		}

		public IClientWorkflowFactory<R> GetClientWorkflowFactory(ServiceSet<R> serviceSet) {
			if(this.clientWorkflowFactory == null) {
				this.CreateFactories(serviceSet);
			}

			return this.clientWorkflowFactory;
		}

		public IServerWorkflowFactory<R> GetServerWorkflowFactory(ServiceSet<R> serviceSet) {
			if(this.serverWorkflowFactory == null) {
				this.CreateFactories(serviceSet);
			}

			return this.serverWorkflowFactory;
		}

		public IGossipWorkflowFactory<R> GetGossipWorkflowFactory(ServiceSet<R> serviceSet) {
			if(this.gossipWorkflowFactory == null) {
				this.CreateFactories(serviceSet);
			}

			return this.gossipWorkflowFactory;
		}

		protected virtual void CreateFactories(ServiceSet<R> serviceSet) {
			this.instantiationFactory = new InstantiationFactory<R>();

			this.clientWorkflowFactory = new ClientWorkflowFactory<R>(serviceSet);
			this.serverWorkflowFactory = new ServerWorkflowFactory<R>(serviceSet);
			this.gossipWorkflowFactory = new GossipWorkflowFactory<R>(serviceSet);
		}
	}
}