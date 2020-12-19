using System;

namespace Neuralia.Blockchains.Core.Network {

	[Flags]
	public enum IPMode:byte {
		Unknown = 0,
		IPv4 = (1 << 0),
		IPv6 = (1 << 1),
		Both = IPv4 | IPv6
	}
}