using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	public class NodeActivityInfo {
		public NodeActivityInfo(NodeAddressInfo node, bool shareable) {
			this.Node = node;
			this.Shareable = shareable;
		}

		public NodeActivityInfo(IgnoreNodeActivityInfo inai) : this((NodeActivityInfo) inai) {

		}

		protected NodeActivityInfo(NodeActivityInfo nai) : this(nai.Node, nai.Shareable) {

			this.Timestamp = nai.Timestamp;

			foreach(NodeActivityEvent entry in nai.Events) {
				this.Events.Enqueue(entry);
			}

			this.RebuildReliabilityIndex();
		}

		public NodeAddressInfo Node { get; }
		public bool Shareable { get; }
		public DateTime Timestamp { get; } = DateTimeEx.CurrentTime;

		private Queue<NodeActivityEvent> Events { get; } = new Queue<NodeActivityEvent>();

		public int ReliabilityIndex { get; private set; }

		public void AddEvent(NodeActivityEvent entry) {

			this.Events.Enqueue(entry);

			this.RebuildReliabilityIndex();
		}

		private void RebuildReliabilityIndex() {

			ImmutableList<NodeActivityEvent> events = this.Events.ToImmutableList();

			this.ReliabilityIndex = 0;
		}

		public struct NodeActivityEvent {

			public enum NodeActivityEventTypes {
				Success,
				Failure
			}

			public NodeActivityEvent(NodeActivityEventTypes type) : this() {
				this.Type = type;
				this.Timestamp = DateTimeEx.CurrentTime;
			}

			public DateTime Timestamp { get; }
			public NodeActivityEventTypes Type { get; }
		}
	}

	public class IgnoreNodeActivityInfo : NodeActivityInfo {

		public IgnoreNodeActivityInfo(NodeActivityInfo nai) : base(nai) {

		}

		public IgnoreNodeActivityInfo(NodeAddressInfo node, bool shareable) : base(node, shareable) {
		}

		public DateTime IgnoreTimestamp { get; } = DateTimeEx.CurrentTime;
		public int IgnoreCounts { get; set; } = 1;
		public bool MaxReached => (this.IgnoreCounts >= 3) || (this.IgnoreTimestamp < DateTimeEx.CurrentTime.AddMinutes(30));
	}
}