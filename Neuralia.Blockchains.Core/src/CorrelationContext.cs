using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Core {
	public struct CorrelationContext {
		public int CorrelationId { get; private set; }
		public bool IsNew => this.CorrelationId == 0;

		public CorrelationContext(int correlationId) {
			this.CorrelationId = correlationId;
		}

		public void InitializeNew() {
			if(this.IsNew) {
				this.CorrelationId = GlobalRandom.GetNext();
			}
		}
	}
}