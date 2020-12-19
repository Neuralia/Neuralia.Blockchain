using System;
using System.Collections.Generic;

namespace Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry {
	public interface IMessageEntry {

		long Hash { get; set; }

		DateTime Received { get; set; }

		bool Valid { get; set; }

		bool Local { get; set; }

		int Echos { get; set; }

		IEnumerable<IMessagePeer> PeersBase { get; }
	}
}