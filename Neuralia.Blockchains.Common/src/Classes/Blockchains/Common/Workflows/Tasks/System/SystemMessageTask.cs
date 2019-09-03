using System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System {
	public class SystemMessageTask : ColoredTask {

		public CorrelationContext correlationContext;

		public BlockchainSystemEventType message;
		public object[] parameters;
		public DateTime timestamp = DateTime.Now;

		public SystemMessageTask(BlockchainSystemEventType eventType) {
			this.message = eventType;
		}

		public SystemMessageTask(BlockchainSystemEventType message, object[] parameters, CorrelationContext correlationContext = default) {
			this.message = message;
			this.correlationContext = correlationContext;
			this.parameters = parameters;
		}
	}
}