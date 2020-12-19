using System;

namespace Neuralia.Blockchains.Core.Workflows.Tasks {
	public interface ISimpleTask : IBasicTask<object> {
		void TriggerAction();
	}

	public class SimpleTask : BasicTask<object>, ISimpleTask {
		
		public SimpleTask() {
			
		}

		public SimpleTask(Action<object> action) : base(action){
		}

		public void TriggerAction() {
			this.TriggerAction(this);
		}
	}
}