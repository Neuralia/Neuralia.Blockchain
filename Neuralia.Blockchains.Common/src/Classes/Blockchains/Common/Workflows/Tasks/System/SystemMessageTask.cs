using System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Workflows.Tasks;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.System {
	public class SystemMessageTask : ColoredTask {

		public CorrelationContext? correlationContext;

		public BlockchainSystemEventType message;
		public object[] parameters;
		public DateTime timestamp = DateTimeEx.CurrentTime;

		public SystemMessageTask(BlockchainSystemEventType eventType) {
			this.message = eventType;
		}

		public SystemMessageTask(BlockchainSystemEventType message, object[] parameters, CorrelationContext correlationContext) {
			this.message = message;
			this.correlationContext = correlationContext;
			this.parameters = parameters;
		}
	}
}