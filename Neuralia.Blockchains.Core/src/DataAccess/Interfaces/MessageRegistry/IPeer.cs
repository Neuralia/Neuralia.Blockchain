using System.Collections.Generic;

namespace Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry {
	public interface IPeer {
		string PeerKey { get; set; }

		IEnumerable<IMessagePeer> MessagesBase { get; }
	}
}