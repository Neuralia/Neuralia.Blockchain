using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation {
	public class EventGenerationException : ApplicationException {

		public EventGenerationException() {
		}

		public EventGenerationException(IEnvelope envelope) {
			this.Envelope = envelope;
		}

		public EventGenerationException(string message) : base(message) {
		}

		public EventGenerationException(string message, Exception innerException) : base(message, innerException) {
		}

		public EventGenerationException(string message, IEnvelope envelope) : base(message) {
			this.Envelope = envelope;
		}

		public EventGenerationException(string message, IEnvelope envelope, Exception innerException) : base(message, innerException) {
			this.Envelope = envelope;
		}

		public EventGenerationException(IEnvelope envelope, Exception innerException) : base("", innerException) {
			this.Envelope = envelope;
		}

		public IEnvelope Envelope { get; }
	}
}