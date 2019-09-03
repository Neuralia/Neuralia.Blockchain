using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests {
	public class DigestChannelSetBase<T> {
		public Dictionary<DigestChannelType, T> Channels { get; } = new Dictionary<DigestChannelType, T>();
	}

	public class DigestChannelSet : DigestChannelSetBase<IDigestChannel> {
	}

	public class ValidatingDigestChannelSet : DigestChannelSetBase<IDigestChannelValidator> {
	}
}