using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.P2p.Messages.Base {

	public interface IMessageFactory {
	}

	public interface IMessageFactory<R> : IMessageFactory
		where R : IRehydrationFactory {
		ITargettedMessageSet<R> RehydrateMessage(IByteArray data, TargettedHeader header, R rehydrationFactory);
	}

	public abstract class MessageFactory<R> : IMessageFactory<R>
		where R : IRehydrationFactory {

		protected ServiceSet<R> serviceSet;

		public MessageFactory(ServiceSet<R> serviceSet) {
			this.NetworkingService = (INetworkingService<R>) DIService.Instance.GetService<INetworkingService>();
			this.TimeService = serviceSet.TimeService;

			this.serviceSet = serviceSet;
		}

		protected INetworkingService<R> NetworkingService { get; }
		protected ITimeService TimeService { get; }

		protected virtual IMainMessageFactory<R> MainMessageFactory => this.NetworkingService.MessageFactory;

		public abstract ITargettedMessageSet<R> RehydrateMessage(IByteArray data, TargettedHeader header, R rehydrationFactory);
	}
}