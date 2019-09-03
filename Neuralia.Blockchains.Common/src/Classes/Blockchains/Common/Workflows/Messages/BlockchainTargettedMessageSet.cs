using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages {
	public interface IBlockchainTargettedMessageSet : ITargettedMessageSet<IBlockchainEventsRehydrationFactory> {
		new TargettedHeader BaseHeader { get; set; }
	}

	public interface IBlockchainTargettedMessageSet<T> : IBlockchainTargettedMessageSet, ITargettedMessageSet<T, IBlockchainEventsRehydrationFactory>
		where T : NetworkMessage<IBlockchainEventsRehydrationFactory> {
		new TargettedHeader Header { get; set; }
	}

	public class BlockchainTargettedMessageSet<T> : TargettedMessageSet<T, IBlockchainEventsRehydrationFactory>, IBlockchainTargettedMessageSet<T>
		where T : NetworkMessage<IBlockchainEventsRehydrationFactory> {
		public new TargettedHeader BaseHeader {
			get => this.Header;
			set => this.Header = value;
		}
	}
}