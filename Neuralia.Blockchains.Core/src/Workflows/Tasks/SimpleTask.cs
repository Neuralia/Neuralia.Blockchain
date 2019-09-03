namespace Neuralia.Blockchains.Core.Workflows.Tasks {
	public interface ISimpleTask : IBasicTask<object> {
		void TriggerAction();
	}

	public class SimpleTask : BasicTask<object>, ISimpleTask {
		public void TriggerAction() {
			this.TriggerAction(this);
		}
	}
}