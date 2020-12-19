using System;

namespace Neuralia.Blockchains.Core.Tools {

	/// <summary>
	///     A special component used to ensure that certain actions are only triggers when other components have been
	///     initialized
	/// </summary>
	public class DelayedTriggerComponent {
		private readonly object locker = new object();
		private int initedComponentsCount;
		private int initedComponentsTotal;

		private bool started;

		public Action TriggerAchived { get; set; }

		public void IncrementInitedComponetsCount() {
			this.initedComponentsCount++;

			this.CheckConditionAchived();
		}

		public void IncrementTotal() {
			this.initedComponentsTotal++;

			this.CheckConditionAchived();
		}

		public void Start() {
			lock(this.locker) {
				if(!this.started) {
					this.started = true;

					this.CheckConditionAchived();
				}
			}
		}

		/// <summary>
		///     make sure we start certain components ONLY when all chain components are ready
		/// </summary>
		private void CheckConditionAchived() {
			lock(this.locker) {

				if(this.started && (this.initedComponentsCount == this.initedComponentsTotal)) {
					if(this.TriggerAchived != null) {
						this.TriggerAchived();
					}
				}
			}
		}
	}
}