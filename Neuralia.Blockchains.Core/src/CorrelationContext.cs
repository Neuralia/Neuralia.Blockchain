using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Core {
	public class CorrelationContext {
		public int CorrelationId { get; private set; }

		public CorrelationContext() {
			this.CorrelationId = GlobalRandom.GetNext();
		}
		
		public CorrelationContext(int correlationId) {
			this.CorrelationId = correlationId;
		}
		
	}
}