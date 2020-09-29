using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {
	public interface IMessageEnvelope  :IEnvelope<IDehydratedBlockchainMessage> {
		Guid ID { get; }
	}
}