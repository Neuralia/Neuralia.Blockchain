using System;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.General {

	public interface IInstantiationFactory<R>
		where R : IRehydrationFactory {
		Func<ServiceSet<R>, IMessagingManager<R>> CreateMessagingManager { get; }
		Func<ServiceSet<R>, IConnectionsManager<R>> CreateConnectionsManager { get; }
	}

	public class InstantiationFactory<R> : IInstantiationFactory<R>
		where R : IRehydrationFactory {
		public virtual Func<ServiceSet<R>, IMessagingManager<R>> CreateMessagingManager => serviceSet => new MessagingManager<R>(serviceSet);
		public virtual Func<ServiceSet<R>, IConnectionsManager<R>> CreateConnectionsManager => serviceSet => new ConnectionsManagerIPCrawler<R>(serviceSet);
	}
}