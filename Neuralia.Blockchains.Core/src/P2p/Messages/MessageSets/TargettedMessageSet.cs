using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.P2p.Messages.MessageSets {
	public interface ITargettedMessageSet : INetworkMessageSet {
		new TargettedHeader BaseHeader { get; set; }

		TargettedHeader Header { get; set; }
	}

	public interface ITargettedMessageSet<R> : INetworkMessageSet<R>, ITargettedMessageSet
		where R : IRehydrationFactory {
	}

	public interface ITargettedMessageSet<out T, R> : INetworkMessageSet<T, TargettedHeader, R>, ITargettedMessageSet<R>
		where T : class, INetworkMessage<R>
		where R : IRehydrationFactory {
		new TargettedHeader Header { get; set; }
	}

	public class TargettedMessageSet<T, R> : NetworkMessageSet<T, TargettedHeader, R>, ITargettedMessageSet<T, R>
		where T : class, INetworkMessage<R>
		where R : IRehydrationFactory {
		public new TargettedHeader BaseHeader {
			get => this.Header;
			set => this.Header = value;
		}
	}
}